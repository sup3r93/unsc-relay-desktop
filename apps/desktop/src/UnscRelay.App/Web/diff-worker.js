'use strict';
importScripts('vendor/diff.js');
self.onmessage=e=>{try{const parts=Diff.diffLines(e.data.old,e.data.next,{timeout:1500,maxEditLength:8000});self.postMessage(parts?{parts}:{error:'Confronto troppo complesso per la vista integrata. Usa l’editor esterno.'})}catch(error){self.postMessage({error:error.message})}};
