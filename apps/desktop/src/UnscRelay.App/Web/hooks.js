 'use strict';
function renderHookEditor(parent,original,localOnly=false){
 const fields=[];
 for(const[phase,kind,title]of [['before-connect','local','Prima connessione / locale'],['after-connect','local','Dopo connessione / locale'],['after-connect','remote','Dopo connessione / remoto'],['before-disconnect','remote','Prima disconnessione / remoto'],['before-disconnect','local','Prima disconnessione / locale'],['after-disconnect','local','Dopo disconnessione / locale']]){
 if(localOnly&&kind!=='local')continue;
 const prior=original.find(h=>h.phase===phase&&h.kind===kind)||{phase,kind,command:'',arguments:[],enabled:false,timeoutSeconds:15};const section=document.createElement('section');section.className='hook-card';const label=document.createElement('label');label.className='check-label';const enabled=document.createElement('input');enabled.type='checkbox';enabled.checked=prior.enabled;label.append(enabled,title);section.append(label);const command=field(section,kind==='local'?'Percorso EXE completo':'Comando remoto','hook-'+phase+'-'+kind,prior.command);const args=kind==='local'?field(section,uiText('Argomenti (uno per riga, senza virgolette aggiuntive)'),'hook-args-'+phase+'-'+kind,(prior.arguments||[]).join('\n'),'textarea'):null;const timeout=field(section,uiText('Timeout (secondi)'),'hook-timeout-'+phase+'-'+kind,prior.timeoutSeconds||15,'number');timeout.min=1;timeout.max=30;fields.push({phase,kind,command,args,enabled,timeout});parent.append(section);
 }
 return()=>fields.map(f=>({phase:f.phase,kind:f.kind,command:f.command.value.trim(),arguments:f.args?f.args.value.split(/\r?\n/).filter(arg=>arg.length>0):[],enabled:f.enabled.checked,timeoutSeconds:Number(f.timeout.value)})).filter(h=>h.enabled||h.command.length);
}
