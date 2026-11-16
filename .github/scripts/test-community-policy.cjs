const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const file=fs.readFileSync('.github/workflows/community.yml','utf8');
assert(!file.includes('actions/checkout'));
assert(!file.includes('${{'));
const script=file.split('          script: |\n')[1].split('\n').map(line=>line.replace(/^            /,'')).join('\n');
async function run(base,author){
 const calls=[];const context={repo:{owner:'owner',repo:'desktop'},payload:{repository:{owner:{id:1}},pull_request:{number:5,base:{ref:base},user:{id:author}}}};
 const github={rest:{issues:{createComment:async x=>calls.push(['comment',x])},pulls:{update:async x=>calls.push(['update',x])}}};
 const core={setFailed:x=>calls.push(['failed',x]),info:()=>{}};
 await vm.runInNewContext('(async()=>{'+script+'})()', {context,github,core});return calls;
}
(async()=>{
 assert.equal((await run('dev',2)).length,0);
 assert.equal((await run('main',1)).length,0);
 const denied=await run('main',2);assert.deepEqual(denied.map(x=>x[0]),['comment','update','failed']);assert.equal(denied[1][1].state,'closed');
 assert((await run('unexpected',2)).some(x=>x[0]==='failed'));
 console.log('4 community target checks passed; no PR code checkout or context interpolation.');
})().catch(e=>{console.error(e);process.exit(1)});
