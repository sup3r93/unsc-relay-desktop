'use strict';
// This module is only a dedicated worker; it never receives window commands.
if(typeof DedicatedWorkerGlobalScope==='undefined'||!(self instanceof DedicatedWorkerGlobalScope))throw new Error('Dedicated worker required.');
importScripts('vendor/diff.js');
self.onmessage=event=>{
 // Worker messages normally have an empty origin. Reject any supplied foreign origin.
 if(event.origin&&event.origin!==self.location.origin)return;
 const value=event.data;
 if(!value||typeof value.old!=='string'||typeof value.next!=='string'||value.old.length>2097152||value.next.length>2097152){self.postMessage({error:'File non valido o oltre il limite di 2 MiB.'});return;}
 const encoder=new TextEncoder();if(encoder.encode(value.old).byteLength>2097152||encoder.encode(value.next).byteLength>2097152){self.postMessage({error:'File non valido o oltre il limite di 2 MiB.'});return;}
 try{const parts=Diff.diffLines(value.old,value.next,{timeout:1500,maxEditLength:8000});self.postMessage(parts?{parts}:{error:'Confronto troppo complesso per la vista integrata. Usa l’editor esterno.'});}
 catch{self.postMessage({error:'Confronto non riuscito.'});}
};
