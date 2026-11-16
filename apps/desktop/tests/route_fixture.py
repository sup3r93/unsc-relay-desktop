"""Two loopback-only SSH bastions and one target; no personal server or key."""
import select
import socket
import subprocess
import tempfile
import threading
from pathlib import Path

import paramiko
from ssh_fixture import Authentication, Files, ROOT


def main():
    done = threading.Event()
    transports = []
    listeners = []
    channels = []

    def relay(channel, destination):
        with channel, socket.create_connection(destination, timeout=10) as peer:
            while not done.is_set():
                ready, _, _ = select.select([channel, peer], [], [], .2)
                for source in ready:
                    data = source.recv(65536)
                    if not data:
                        return
                    (peer if source is channel else channel).sendall(data)

    class Bastion(Authentication):
        def __init__(self, allowed_port):
            self.allowed_port = allowed_port
            self.destinations = {}

        def check_channel_direct_tcpip_request(self, channel_id, origin, destination):
            if destination[0] not in ("localhost", "127.0.0.1") or destination[1] != self.allowed_port:
                return paramiko.OPEN_FAILED_ADMINISTRATIVELY_PROHIBITED
            self.destinations[channel_id] = ("127.0.0.1", destination[1])
            return paramiko.OPEN_SUCCEEDED

    def start(directory, destination=None):
        key = paramiko.RSAKey.generate(2048)
        listener = socket.socket()
        listener.bind(("127.0.0.1", 0))
        listener.listen(8)
        listener.settimeout(.2)
        listeners.append(listener)

        def serve(peer):
            transport = paramiko.Transport(peer)
            transports.append(transport)
            transport.add_server_key(key)
            transport.set_subsystem_handler("sftp", paramiko.SFTPServer, Files, directory=directory)
            server = Bastion(destination) if destination else Authentication()
            try:
                transport.start_server(server=server)
                while transport.is_active() and not done.is_set():
                    channel = transport.accept(.2)
                    if channel:
                        channels.append(channel)
                    if channel and isinstance(server, Bastion) and channel.get_id() in server.destinations:
                        threading.Thread(target=relay, args=(channel, server.destinations[channel.get_id()]), daemon=True).start()
            except (OSError, EOFError, paramiko.SSHException):
                pass

        def accept():
            while not done.is_set():
                try:
                    peer, _ = listener.accept()
                    threading.Thread(target=serve, args=(peer,), daemon=True).start()
                except socket.timeout:
                    pass
                except OSError:
                    return

        threading.Thread(target=accept, daemon=True).start()
        return listener.getsockname()[1]

    with tempfile.TemporaryDirectory(prefix="relay-route-") as directory:
        target = start(directory)
        jump = start(directory, target)
        root = start(directory, jump)
        executable = ROOT / "tests/UnscRelay.Tests/bin/Release/net10.0/UnscRelay.Tests.exe"
        try:
            return subprocess.run([str(executable), "--routes-only", str(root), str(jump), str(target)], timeout=55).returncode
        finally:
            done.set()
            for transport in transports:
                transport.close()
            for listener in listeners:
                listener.close()


if __name__ == "__main__":
    raise SystemExit(main())
