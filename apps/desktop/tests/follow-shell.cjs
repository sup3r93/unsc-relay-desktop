const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),{spawnSync}=require('node:child_process');
const source=fs.readFileSync(path.join(__dirname,'../src/UnscRelay.App/Web/file-follow.js'),'utf8');
const literal=source.match(/const command=("(?:\\.|[^"\\])*");/);
if(!literal)throw Error('Missing JSON command literal');
// Execute a pinned command only after comparing the production literal. Never evaluate repository text.
const command="__relay_cwd(){ local p=\"$PWD\"; p=\"${p//%/%25}\"; p=\"${p// /%20}\"; p=\"${p//#/%23}\"; p=\"${p//\\?/%3F}\"; printf '\\033]7;file://%s%s\\007' \"$HOSTNAME\" \"$p\"; }; PROMPT_COMMAND+=(__relay_cwd); __relay_cwd\r";
assert.equal(JSON.parse(literal[1]),command,'Production Bash integration must match the reviewed command');
if(process.argv[2]==='--validate-only'){console.log('PASS pinned Bash command matches production without code evaluation');process.exit(0);}
const bundled=path.resolve(__dirname,'../src/UnscRelay.App/Tools/Cygwin/bin/bash.exe');
const candidates=process.platform==='win32'?[bundled]:['/bin/bash','/usr/bin/bash'];
const requested=process.argv[2]&&path.resolve(process.argv[2]);
const bash=candidates.find(candidate=>(!requested||path.resolve(candidate)===requested)&&fs.existsSync(candidate));
if(!bash)throw Error('Use the bundled Bash or the system Bash executable');
const directory=fs.mkdtempSync(path.join(require('node:os').tmpdir(),'relay-follow-shell-'));
try{
 const cwd=path.join(directory,'follow shell #percent%');fs.mkdirSync(cwd);
 const result=spawnSync(bash,['--noprofile','--norc','-c',command.replace(/\r$/,'\n')],{cwd,encoding:'utf8',timeout:15000,env:{...process.env,BASH_ENV:'',ENV:'',SHELLOPTS:'',BASHOPTS:''}});
 if(result.error)throw result.error;
 if(result.status!==0||!result.stdout.includes('\x1b]7;file://')||!result.stdout.includes('follow%20shell%20%23percent%25')||!result.stdout.includes('\x07'))throw Error('Bash directory reporting failed');
 console.log('PASS Bash reports OSC7 with escaped spaces, hash and percent without editing startup files');
}finally{fs.rmSync(directory,{recursive:true,force:true});}
