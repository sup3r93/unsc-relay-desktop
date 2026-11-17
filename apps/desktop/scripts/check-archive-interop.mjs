import {spawn} from 'node:child_process';
import {createInterface} from 'node:readline';
import {pathToFileURL} from 'node:url';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const [modulePath,dllPath]=process.argv.slice(2);if(!modulePath||!dllPath)throw Error('Usage: node check-archive-interop.mjs web-archive-module desktop-tests-dll');
const {generateSharingDevice,wrapArchiveKey,unwrapArchiveKey}=await import(pathToFileURL(resolve(modulePath)).href);
const browser=await generateSharingDevice();assert.equal(browser.privateKey.extractable,false);
const archiveId=crypto.randomUUID(),deviceId=crypto.randomUUID(),archiveOwner='A'.repeat(64),accountOwner='B'.repeat(64),key=crypto.getRandomValues(new Uint8Array(32));
const child=spawn('dotnet',[resolve(dllPath),'--archive-key-interop'],{stdio:['pipe','pipe','pipe'],windowsHide:true});let errors='';child.stderr.on('data',chunk=>errors+=chunk);const lines=createInterface({input:child.stdout})[Symbol.asyncIterator]();const deadline=setTimeout(()=>child.kill(),25000);deadline.unref();
try{
 child.stdin.write(JSON.stringify({archiveId,deviceId,archiveOwner,accountOwner,publicKey:browser.publicKey,key:Buffer.from(key).toString('base64')})+'\n');
 const first=await lines.next();if(first.done)throw Error(errors||'Native interoperability process stopped');const native=JSON.parse(first.value);
 const opened=await unwrapArchiveKey(native.grant,archiveId,archiveOwner,accountOwner,deviceId,browser.privateKey);try{assert.deepEqual(opened,key)}finally{opened.fill(0)}
 await assert.rejects(unwrapArchiveKey({...native.grant,accountOwner:'C'.repeat(64)},archiveId,archiveOwner,'C'.repeat(64),deviceId,browser.privateKey));
 const grant=await wrapArchiveKey(archiveId,archiveOwner,accountOwner,deviceId,native.publicKey,key);child.stdin.end(JSON.stringify(grant)+'\n');const result=await lines.next();assert.equal(result.value,'PASS cross-runtime archive key interoperability');console.log(result.value);console.log('PASS browser private keys stay non-exportable and changed recipient bindings are rejected');
}finally{key.fill(0);clearTimeout(deadline);child.kill()}
