const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const file=fs.readFileSync('.github/workflows/community.yml','utf8').replace(/\r\n/g,'\n');
assert(!file.includes('actions/checkout'));
assert(!file.includes('${{'));
const expectedScript="const pr = context.payload.pull_request;\nconst owner = context.payload.repository.owner;\nconst allowed = pr.base.ref === 'dev' || (pr.base.ref === 'main' && pr.user.id === owner.id);\nif (!allowed) {\n  await github.rest.issues.createComment({...context.repo, issue_number: pr.number,\n    body: 'Community contributions must target **dev**. Please reopen this PR against `dev`. Only the repository owner may propose changes to `main`.\\n\\nI contributi della community devono essere diretti a **dev**. Riapri la PR verso `dev`: solo il titolare può proporre modifiche a `main`.'});\n  await github.rest.pulls.update({...context.repo, pull_number: pr.number, state: 'closed'});\n  core.setFailed('Community pull requests must target dev.');\n  return;\n}\ncore.info(`Accepted target: ${pr.base.ref}`);\n";
const actualScript=file.split('          script: |\n')[1].split('\n').map(line=>line.replace(/^            /,'')).join('\n');
assert.equal(actualScript,expectedScript,'Review the pinned policy test before accepting workflow code changes.');
async function run(base,author){
 const calls=[];const context={repo:{owner:'owner',repo:'desktop'},payload:{repository:{owner:{id:1}},pull_request:{number:5,base:{ref:base},user:{id:author}}}};
 const github={rest:{issues:{createComment:async x=>calls.push(['comment',x])},pulls:{update:async x=>calls.push(['update',x])}}};
 const core={setFailed:x=>calls.push(['failed',x]),info:()=>{}};
 await vm.runInNewContext('(async()=>{'+expectedScript+'})()', {context,github,core});return calls;
}
(async()=>{
 assert.equal((await run('dev',2)).length,0);
 assert.equal((await run('main',1)).length,0);
 const denied=await run('main',2);assert.deepEqual(denied.map(x=>x[0]),['comment','update','failed']);assert.equal(denied[1][1].state,'closed');
 assert((await run('unexpected',2)).some(x=>x[0]==='failed'));
 console.log('4 community target checks passed; no PR code checkout or context interpolation.');
})().catch(e=>{console.error(e);process.exit(1)});
