"""Loopback-only FTP/S3 integration. Synthetic credentials and temporary files only."""
import os, sys, socket, tempfile, pathlib, subprocess, threading, time, shutil
import ssl, http.server, datetime, ipaddress
from cryptography import x509
from cryptography.x509.oid import NameOID
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from pyftpdlib.authorizers import DummyAuthorizer
from pyftpdlib.handlers import FTPHandler, ThrottledDTPHandler
from pyftpdlib.servers import FTPServer
from pyftpdlib.filesystems import AbstractedFS

def port():
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0)); return s.getsockname()[1]

def run(exe):
    temp=pathlib.Path(tempfile.mkdtemp(prefix="relay-storage-fixture-")); server=None; process=None; tls_server=None
    try:
        ftp=temp/"ftp"; s3=temp/"s3"; bucket=s3/"fixture"
        for directory in (ftp,bucket):
            (directory/"folder").mkdir(parents=True); (directory/"existing.txt").write_text("existing"); (directory/"folder"/"nested.txt").write_text("nested")
        (ftp/"large.bin").write_bytes(os.urandom(2*1024*1024))
        authorizer=DummyAuthorizer(); authorizer.add_user("fixture","fixture-secret",str(ftp),perm="elradfmwMT")
        class SlowData(ThrottledDTPHandler):
            write_limit=32768
        class PosixRenameFS(AbstractedFS):
            def rename(self,src,dst):os.replace(src,dst) # Model POSIX FTP server replacement on Windows.
        class Handler(FTPHandler):
            dtp_handler=SlowData
            abstracted_fs=PosixRenameFS
        Handler.authorizer=authorizer; ftpport=port(); server=FTPServer(("127.0.0.1",ftpport),Handler)
        threading.Thread(target=server.serve_forever,kwargs={"timeout":.1,"handle_exit":False},daemon=True).start()
        s3port=port(); env=os.environ.copy();env["RCLONE_AUTH_KEY"]='"fixture-access,fixture-secret"'
        process=subprocess.Popen([exe,"serve","s3",str(s3),"--addr",f"127.0.0.1:{s3port}"],env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
        for _ in range(100):
            try:
                with socket.create_connection(("127.0.0.1",s3port),.1):break
            except OSError:time.sleep(.1)
        else:raise RuntimeError("S3 fixture did not start")
        tests=pathlib.Path(__file__).parent/"UnscRelay.Tests/bin/Release/net10.0/UnscRelay.Tests.exe"
        # An untrusted loopback HTTPS server must never receive an authenticated request.
        key=rsa.generate_private_key(public_exponent=65537,key_size=2048)
        name=x509.Name([x509.NameAttribute(NameOID.COMMON_NAME,"Relay fixture")])
        now=datetime.datetime.now(datetime.timezone.utc)
        cert=(x509.CertificateBuilder().subject_name(name).issuer_name(name).public_key(key.public_key())
              .serial_number(x509.random_serial_number()).not_valid_before(now-datetime.timedelta(minutes=1))
              .not_valid_after(now+datetime.timedelta(hours=1))
              .add_extension(x509.SubjectAlternativeName([x509.IPAddress(ipaddress.ip_address("127.0.0.1"))]),critical=False)
              .sign(key,hashes.SHA256()))
        (temp/"tls.crt").write_bytes(cert.public_bytes(serialization.Encoding.PEM))
        (temp/"tls.key").write_bytes(key.private_bytes(serialization.Encoding.PEM,serialization.PrivateFormat.PKCS8,serialization.NoEncryption()))
        requests=[]
        class RejectHandler(http.server.BaseHTTPRequestHandler):
            def do_GET(self):requests.append(True);self.send_response(200);self.end_headers();self.wfile.write(b"[]")
            def log_message(self,*args):pass
        tls_server=http.server.ThreadingHTTPServer(("127.0.0.1",0),RejectHandler)
        context=ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER);context.load_cert_chain(temp/"tls.crt",temp/"tls.key")
        tls_server.socket=context.wrap_socket(tls_server.socket,server_side=True)
        threading.Thread(target=tls_server.serve_forever,daemon=True).start()
        subprocess.run([str(tests),"--storage-untrusted-tls",exe,str(tls_server.server_port)],check=True,timeout=60)
        if requests:raise AssertionError("Credentials reached an untrusted TLS endpoint")
        for kind,p in [("ftp",ftpport),("s3",s3port)]:
            local=temp/(kind+"-local");local.mkdir()
            subprocess.run([str(tests),"--storage-files",exe,kind,str(p),str(local)],check=True,timeout=120)
    finally:
        if tls_server:tls_server.shutdown();tls_server.server_close()
        if server:server.close_all()
        if process:
            process.terminate()
            try:process.wait(5)
            except subprocess.TimeoutExpired:process.kill();process.wait()
        # Only the exact, newly-created fixture directory is removed.
        if temp.parent==pathlib.Path(tempfile.gettempdir()) and temp.name.startswith("relay-storage-fixture-"):shutil.rmtree(temp)
if __name__=="__main__":run(str(pathlib.Path(sys.argv[1]).resolve(strict=True)))
