const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),{spawnSync}=require('node:child_process');
const bash=process.argv[2];if(!bash)throw Error('Pass the local Bash executable path');
const source=fs.readFileSync(path.join(__dirname,'../src/UnscRelay.App/Web/file-follow.js'),'utf8');
const literal=source.match(/const command=(`[^`]+`);/)[1];const command=vm.runInNewContext(literal).replace(/\r$/,'\n');
const directory=path.join(__dirname,'../artifacts/follow shell #percent%');fs.mkdirSync(directory,{recursive:true});
const result=spawnSync(bash,['--noprofile','--norc','-c',command],{cwd:directory,encoding:'utf8',env:{...process.env,BASH_ENV:''}});
if(result.status!==0||!result.stdout.includes('\x1b]7;file://')||!result.stdout.includes('follow%20shell%20%23percent%25')||!result.stdout.includes('\x07'))throw Error('Bash directory reporting failed');
console.log('PASS Bash reports OSC7 with escaped spaces, hash and percent without editing startup files');
