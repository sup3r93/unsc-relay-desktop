"""Loopback-only integration fixture using Paramiko, never a user's SSH server."""
import errno
import os
from pathlib import Path
import socket
import sys
import subprocess
import tempfile
import threading
import logging
import paramiko

ROOT = Path(__file__).resolve().parents[1]
logging.getLogger("paramiko.transport").setLevel(logging.CRITICAL)


class Authentication(paramiko.ServerInterface):
    def __init__(self, directory=None):
        self.directory = directory

    def check_channel_exec_request(self, channel, command):
        text = command.decode('utf-8')
        if '--app-native' in sys.argv and text == 'echo NATIVE-MACRO-FIXTURE\nexec "${SHELL:-/bin/sh}" -l':
            (Path(self.directory) / 'native-macro.txt').write_bytes(command)
            return self.check_channel_shell_request(channel)
        if '--app-recovery' in sys.argv and text=='fixture-ok':
            with open(Path(self.directory) / 'hook-count.txt', 'a') as trace: trace.write('hook\n')
        if text in ('fixture-sftp', 'sudo -n fixture-sftp'):
            with open(Path(self.directory) / 'exec-count.txt', 'a') as stream:
                stream.write(text + '\n')
            handler = FixtureSFTPServer(channel, 'sftp', self, Files, directory=self.directory)
            handler.start()
            return True
        if text in ('fixture-ok', 'fixture-fail', 'fixture-wait'):
            def execute():
                import time
                time.sleep(.05)
                try:
                    if text == 'fixture-wait': time.sleep(3)
                    channel.sendall(b'fixture hook output')
                    channel.send_stderr(b'fixture hook diagnostic')
                    channel.send_exit_status(7 if text == 'fixture-fail' else 0)
                    channel.close()
                except (OSError, EOFError): pass
            threading.Thread(target=execute, daemon=True).start()
            return True
        return False

    def check_auth_password(self, username, password):
        return paramiko.AUTH_SUCCESSFUL if username in ("test", "password-only") and password == "relay-test-password" else paramiko.AUTH_FAILED

    def get_allowed_auths(self, username):
        return "password,publickey"

    def check_auth_publickey(self, username, key):
        if username == "test-cert":
            kind = getattr(getattr(key, 'public_blob', None), 'key_type', '')
            if self.directory:
                with open(Path(self.directory) / 'certificate-auth.txt', 'a') as trace:
                    trace.write(kind + '\n')
            return paramiko.AUTH_SUCCESSFUL if kind.endswith('-cert-v01@openssh.com') else paramiko.AUTH_FAILED
        return paramiko.AUTH_SUCCESSFUL if username == "test" else paramiko.AUTH_FAILED

    def check_channel_request(self, kind, channel_id):
        return paramiko.OPEN_SUCCEEDED if kind == "session" else paramiko.OPEN_FAILED_ADMINISTRATIVELY_PROHIBITED

    def check_channel_pty_request(self, *args):
        return True

    def check_channel_window_change_request(self, *args):
        return True

    def check_channel_shell_request(self, channel):
        channel.get_transport().relay_shell = True
        def echo():
            try:
                channel.send(b"UNSC loopback fixture\r\n")
                while True:
                    data = channel.recv(65536)
                    if not data:
                        return
                    if '--app-access' in sys.argv:
                        with open(Path(self.directory) / 'shell-input.bin', 'ab') as trace:
                            trace.write(data)
                    channel.sendall(data)
            except (OSError, EOFError):
                pass
        threading.Thread(target=echo, daemon=True).start()
        return True


class FixtureSFTPServer(paramiko.SFTPServer):
    def _send_server_version(self):
        if "--app-access" not in sys.argv and "--app-recovery" not in sys.argv and "--files-only" not in sys.argv and "--app-files" not in sys.argv and "--packaged-files" not in sys.argv:
            return super()._send_server_version()
        from paramiko.sftp import CMD_INIT, CMD_VERSION
        packet_type, data = self._read_packet()
        if packet_type != CMD_INIT:
            raise RuntimeError("Expected SFTP init")
        message = paramiko.Message()
        message.add_int(3)
        message.add_string("posix-rename@openssh.com")
        message.add_string("1")
        self._send_packet(CMD_VERSION, message)
        return 3


class FileHandle(paramiko.SFTPHandle):
    def stat(self):
        stream = getattr(self, "readfile", None) or self.writefile
        return paramiko.SFTPAttributes.from_stat(os.fstat(stream.fileno()))


class Files(paramiko.SFTPServerInterface):
    def __init__(self, server, *, directory):
        super().__init__(server)
        self.directory = Path(directory).resolve()

    def local(self, path):
        result = (self.directory / path.lstrip("/")).resolve()
        if not result.is_relative_to(self.directory):
            raise PermissionError(errno.EACCES, "Outside fixture directory")
        return result

    def list_folder(self, path):
        result = []
        for file in self.local(path).iterdir():
            item = paramiko.SFTPAttributes.from_stat(file.stat())
            item.filename = file.name
            result.append(item)
        return result

    def stat(self, path):
        try:
            return paramiko.SFTPAttributes.from_stat(self.local(path).stat())
        except OSError as error:
            return paramiko.SFTPServer.convert_errno(error.errno)

    lstat = stat

    def open(self, path, flags, attributes):
        try:
            descriptor = os.open(self.local(path), flags | getattr(os, "O_BINARY", 0), 0o600)
            mode = "r+b" if flags & os.O_RDWR else "wb" if flags & os.O_WRONLY else "rb"
            stream = os.fdopen(descriptor, mode)
            handle = FileHandle(flags)
            handle.readfile = stream
            handle.writefile = stream
            return handle
        except OSError as error:
            return paramiko.SFTPServer.convert_errno(error.errno)

    def rename(self, old, new):
        try:
            os.rename(self.local(old), self.local(new))
            return paramiko.SFTP_OK
        except OSError as error:
            return paramiko.SFTPServer.convert_errno(error.errno)

    def posix_rename(self, old, new):
        try:
            os.replace(self.local(old), self.local(new))
            return paramiko.SFTP_OK
        except OSError as error:
            return paramiko.SFTPServer.convert_errno(error.errno)

    def mkdir(self, path, attributes):
        try:
            self.local(path).mkdir()
            return paramiko.SFTP_OK
        except OSError as error:
            return paramiko.SFTPServer.convert_errno(error.errno)

    def rmdir(self, path):
        try:
            self.local(path).rmdir()
            return paramiko.SFTP_OK
        except OSError as error:
            return paramiko.SFTPServer.convert_errno(error.errno)

    def chattr(self, path, attributes):
        try:
            if attributes.st_mode is not None:
                os.chmod(self.local(path), attributes.st_mode)
            return paramiko.SFTP_OK
        except OSError as error:
            return paramiko.SFTPServer.convert_errno(error.errno)

    def remove(self, path):
        try:
            self.local(path).unlink()
            return paramiko.SFTP_OK
        except OSError as error:
            return paramiko.SFTPServer.convert_errno(error.errno)


def main():
    if "--packaged-access" in sys.argv: sys.argv.append("--app-access")
    if "--packaged-native" in sys.argv: sys.argv.append("--app-native")
    key = paramiko.RSAKey.generate(2048)
    with tempfile.TemporaryDirectory(prefix="relay-ssh-") as directory, socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        listener.listen(8)
        transports = []
        channels = []
        done = threading.Event()
        offline_until = [0.0]
        def outages():
            import time
            while not done.wait(.03):
                for name, only_sftp in [('drop-network.request', False), ('drop-sftp.request', True)]:
                    trigger = Path(directory) / name
                    if trigger.exists():
                        trigger.unlink()
                        if not only_sftp: offline_until[0] = time.monotonic() + .4
                        for transport in list(transports):
                            if not only_sftp or not getattr(transport, 'relay_shell', False): transport.close()
        if '--app-recovery' in sys.argv: threading.Thread(target=outages, daemon=True).start()

        def serve(client):
            transport = paramiko.Transport(client)
            options = transport.get_security_options()
            options.kex = tuple(k for k in options.kex if not k.startswith(("mlkem", "sntrup")))
            transports.append(transport)
            transport.add_server_key(key)
            transport.set_subsystem_handler("sftp", FixtureSFTPServer, Files, directory=directory)
            try:
                transport.start_server(server=Authentication(directory))
                while transport.is_active() and not done.is_set():
                    channel = transport.accept(.2)
                    if channel:
                        channels.append(channel)
            except (OSError, EOFError, paramiko.SSHException):
                pass

        def accept():
            listener.settimeout(.2)
            while not done.is_set():
                try:
                    client, address = listener.accept()
                    import time
                    if time.monotonic() < offline_until[0]:
                        client.close()
                        continue
                    threading.Thread(target=serve, args=(client,), daemon=True).start()
                except socket.timeout:
                    pass
                except OSError:
                    return

        threading.Thread(target=accept, daemon=True).start()
        executable = ROOT / "tests/UnscRelay.Tests/bin/Release/net10.0/UnscRelay.Tests.exe"
        try:
            options = ["--large-rsa-only"] if "--large-rsa-only" in sys.argv else ["--files-only"] if "--files-only" in sys.argv else []
            if "--terminal-only" in sys.argv: options=["--terminal-only"]
            if "--native-integration-only" in sys.argv:
                options = ["--native-integration-only", str(ROOT / "src/UnscRelay.App/Tools"), str(ROOT / "src/UnscRelay.App/bin/Release/net10.0-windows10.0.19041.0/UNSC Relay.exe")]
            environment = dict(os.environ, RELAY_FIXTURE_ROOT=directory, RELAY_FIXTURE_HOST_PUBLIC=key.get_name()+" "+key.get_base64())
            if "--app-access" in sys.argv or "--app-recovery" in sys.argv or "--app-native" in sys.argv or "--app-files" in sys.argv or "--packaged-files" in sys.argv or "--app-windows" in sys.argv or "--packaged-windows" in sys.argv:
                executable = ROOT / "src/UnscRelay.App/bin/Release/net10.0-windows10.0.19041.0/UNSC Relay.exe"
                environment["RELAY_HOOK_EXE"] = str(ROOT / "tests/UnscRelay.Tests/bin/Release/net10.0/UnscRelay.Tests.exe")
                if "--packaged-files" in sys.argv or "--packaged-windows" in sys.argv or "--packaged-access" in sys.argv or "--packaged-native" in sys.argv:
                    executable = ROOT / "artifacts/distribution/Librerie/UNSC Relay.exe"
                environment["RELAY_FILE_PORT"] = str(listener.getsockname()[1])
                startup = subprocess.STARTUPINFO()
                startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
                startup.wShowWindow = 0
                result = subprocess.run([str(executable), "--access-smoke-test" if "--app-access" in sys.argv else "--recovery-smoke-test" if "--app-recovery" in sys.argv else "--native-integration-smoke-test" if "--app-native" in sys.argv else "--windows-explorer-smoke-test" if "--app-windows" in sys.argv or "--packaged-windows" in sys.argv else "--files-smoke-test"], timeout=220 if environment.get("RELAY_DRAG_PREVIEW")=="1" or environment.get("RELAY_VERIFY_LOGIN_WAIT")=="1" else 90, env=environment, startupinfo=startup)
            else:
                result = subprocess.run([str(executable), *options, str(listener.getsockname()[1])], timeout=140, env=environment)
            return result.returncode
        finally:
            done.set()
            for transport in transports:
                transport.close()


if __name__ == "__main__":
    raise SystemExit(main())
