import sys,io,json,hashlib,urllib.request,tarfile,zipfile,concurrent.futures,shutil
from pathlib import Path,PurePosixPath
sys.path.insert(0,str(Path('artifacts/build-python').resolve()))
import zstandard
root=Path('src/UnscRelay.App/Tools');cache=Path('artifacts/tool-cache');cache.mkdir(exist_ok=True)
source=Path('artifacts/native-sources');source.mkdir(exist_ok=True)
def download(url,target,digest,algorithm='sha256'):
 if not target.exists() or hashlib.new(algorithm,target.read_bytes()).hexdigest()!=digest:
  with urllib.request.urlopen(url,timeout=90) as response:content=response.read()
  if hashlib.new(algorithm,content).hexdigest()!=digest:raise Exception('Digest mismatch '+url)
  target.write_bytes(content)
 return target
packages=json.loads(Path('scripts/cygwin-packages.lock.json').read_text())
def package(p):
 for key in ['install','source']:
  if not p[key]:continue
  path,size,digest=p[key].split();target=(cache if key=='install' else source)/PurePosixPath(path).name
  download('https://cygwin.com/ftp/cygwin/'+path,target,digest,'sha512')
 return p
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
 for p in pool.map(package,packages):print('Verified '+p['name'],flush=True)
cyg=root/'Cygwin';cyg.mkdir(exist_ok=True)
for p in packages:
 if not p['install']:continue
 file=cache/PurePosixPath(p['install'].split()[0]).name
 content=file.read_bytes()
 if file.suffix=='.zst':
  with zstandard.ZstdDecompressor().stream_reader(io.BytesIO(content)) as r:content=r.read()
 with tarfile.open(fileobj=io.BytesIO(content),mode='r:*') as tar:
  members={m.name:m for m in tar.getmembers()}
  for member in members.values():
   path=PurePosixPath(member.name)
   if path.is_absolute() or '..' in path.parts:raise Exception('Unsafe tar entry')
   if not member.isfile():continue
   name=str(path)
   if any(any(c in part for c in '<>:"|?*') for part in path.parts):continue
   if name.startswith('usr/bin/'):name='bin/'+name[len('usr/bin')+1:]
   if name.startswith('usr/lib/'):name='lib/'+name[len('usr/lib/'):]
   if not (name.startswith('bin/') or name.startswith('lib/') or name.startswith('usr/share/') or name.startswith('etc/')):continue
   # Never distribute service/server/privileged setup helpers.
   if name.startswith(('etc/postinstall/','etc/preremove/')) or path.name in ['sshd.exe','cygrunsrv.exe','ssh-host-config','ssh-user-config','cygserver.exe','inetd.exe','rshd.exe','rlogind.exe','telnetd.exe','ftpd.exe','mosh-server.exe']:continue
   target=cyg/name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(tar.extractfile(member).read())
  # Resolve only file symlinks inside the archive, without creating filesystem links.
  for member in members.values():
   if not member.issym():continue
   name=member.name
   if name.startswith('usr/bin/'):name='bin/'+name[len('usr/bin/'):]
   if not name.startswith('bin/'):continue
   link=member.linkname
   if '/' in link or link not in [PurePosixPath(m).name for m in members]:continue
   src=cyg/'bin'/link
   if src.is_file():shutil.copyfile(src,cyg/name)
extras=[('OpenSSH','https://github.com/PowerShell/Win32-OpenSSH/releases/download/10.4.0.0p1-Preview/OpenSSH-Win64.zip','b67ff8becd203502190ec42ac7787c24c2cd3325ed7700485fe7a1c83d2a7957'),('Rclone','https://github.com/rclone/rclone/releases/download/v1.76.0/rclone-v1.76.0-windows-amd64.zip','8b4fd2737ade4e70c057ef4a49deb7cffe94b0fb9d7587c189d149823f21a1dc'),('Step','https://github.com/smallstep/cli/releases/download/v0.31.0/step_windows_amd64.zip','f9388aaf4c190d1bb7b5606594a8ad0924d296d4753c2475cc811a4777913b80')]
extras.append(('PowerShell','https://github.com/PowerShell/PowerShell/releases/download/v7.7.0/PowerShell-7.7.0-win-x64.zip','7c74e03b20a75b59715847439d046a6214b1863ee79b187872980f139bb4a05f'))
for name,url,digest in extras:
 file=download(url,cache/(name+'.zip'),digest)
 with zipfile.ZipFile(file) as z:
  for member in z.infolist():
   path=PurePosixPath(member.filename)
   if path.is_absolute() or '..' in path.parts:raise Exception('Unsafe zip entry')
   if member.is_dir():continue
   if name=='OpenSSH' and (path.name in ['sshd.exe','ssh-agent.exe','sshd-auth.exe','sshd-session.exe','sftp-server.exe','sshd_config_default'] or path.suffix.lower() in ['.ps1','.psm1']):continue
   target=root/name/(str(path) if name=='PowerShell' else path.name);target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(z.read(member))
 print('Verified '+name,flush=True)
download('https://the.earth.li/~sgtatham/putty/0.85/w64/plink.exe',cache/'plink.exe','969f36879d5716aa1a9811f43a6a6510e8f08372dbeb9695b810b9c776f39c75')
(root/'PuTTY').mkdir(exist_ok=True);shutil.copyfile(cache/'plink.exe',root/'PuTTY'/'plink.exe')
license_file=download('https://raw.githubusercontent.com/rclone/rclone/v1.76.0/COPYING',cache/'rclone-COPYING','8cd2e9e750b90a04b7d82dbbca3930c696ae0309d7c10464f90a44f45754cd04')
shutil.copyfile(license_file,root/'Rclone'/'COPYING')
shutil.copyfile(next((root/'PuTTYgen').glob('*LICENSE*')),root/'PuTTY'/'LICENSE.txt')
for folder in ['tmp','home','var/log']:
 d=root/'Cygwin'/folder;d.mkdir(exist_ok=True,parents=True);(d/'.keep').write_text('UNSC Relay portable runtime directory.\n')
manifest={str(p.relative_to(root)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for directory in ['Cygwin','OpenSSH','Step','Rclone','PuTTY','PowerShell'] for p in (root/directory).rglob('*') if p.is_file()}
(root/'bundled-manifest.json').write_text(json.dumps(manifest,indent=2))
with zipfile.ZipFile('artifacts/UNSC-Relay-Native-Sources.zip','w',compression=zipfile.ZIP_STORED) as z:
 for p in source.iterdir():z.write(p,p.name)
 z.write('scripts/cygwin-packages.lock.json','cygwin-packages.lock.json')
print('Bundled files: '+str(len(manifest)),flush=True)
