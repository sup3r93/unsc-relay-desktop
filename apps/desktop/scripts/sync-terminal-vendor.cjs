'use strict';
// Preserve upstream bundles and apply only the documented window-message guard.
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'..'),check=process.argv.includes('--check');
const files=[['@xterm/xterm','xterm.js'],['@xterm/addon-search','addon-search.js']];
const expected=JSON.parse(fs.readFileSync(path.join(root,'package.json'),'utf8')).dependencies;
for(const [name,file]of files){
 const packageRoot=path.join(root,'node_modules',name),version=JSON.parse(fs.readFileSync(path.join(packageRoot,'package.json'),'utf8')).version;
 if(version!==expected[name])throw new Error('Pinned terminal version mismatch: '+name);
 const source=fs.readFileSync(path.join(packageRoot,'lib',file),'utf8').replace(/^\uFEFF/,'').replace(/\r\n/g,'\n');
 const pattern=/([A-Za-z_$][\w$]*)\.addEventListener\("message",\(([A-Za-z_$][\w$]*)=>\{if\(\2\.data&&\2\.data\.vscodeScheduleAsyncWork\)/g;
 let count=0;const patched=source.replace(pattern,(match,host,event)=>{count++;return host+'.addEventListener("message",('+event+'=>{if('+event+'.source!=='+host+'||'+event+'.origin!=='+host+'.location.origin)return;if('+event+'.data&&'+event+'.data.vscodeScheduleAsyncWork)'});
 if(count!==1)throw new Error('Terminal scheduler changed; review required: '+file);
 const target=path.join(root,'src/UnscRelay.App/Web/vendor',file);
 if(check){if(fs.readFileSync(target,'utf8').replace(/^\uFEFF/,'').replace(/\r\n/g,'\n')!==patched)throw new Error('Terminal patch missing or unexpected bundle changes: '+file);}
 else fs.writeFileSync(target,patched);
 console.log((check?'Verified':'Updated')+' terminal message guard: '+file+' '+version);
}
