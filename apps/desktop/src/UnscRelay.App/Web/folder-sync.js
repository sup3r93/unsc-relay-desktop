'use strict';
async function folderSyncDialog(pane){
 if(!pane.files)return toast('Apri una sessione SFTP connessa.',true);
 const body=showModal('Confronta e sincronizza cartelle','SFTP / ANTEPRIMA PRIMA DEI TRASFERIMENTI',null,true);
 modal.classList.add('sync-modal');
 const roots=document.createElement('p');roots.className='sync-roots';roots.textContent='PC: '+pane.localPath+'\nServer '+pane.profile.host+': '+pane.remotePath;body.append(roots);
 notice(body,'Confronto ricorsivo SHA-256 dei contenuti, fino a 1000 elementi per lato e 512 MiB totali. Nessuna cancellazione automatica. I collegamenti e i conflitti fra nomi/tipi vengono esclusi.');
 const scanId=crypto.randomUUID();let scanning=true,plan=null,active=true;const chosen=new Set();
 const cancel=()=>{active=false;if(scanning)rpc('folderCancel',{scanId}).catch(()=>{});modal.removeEventListener('close',cancel)};modal.addEventListener('close',cancel,{once:true});
 const status=document.createElement('p');status.setAttribute('role','status');status.textContent='Lettura cartelle e verifica dei contenuti…';
 const stop=button('Annulla confronto',()=>{cancel();modal.close()});body.append(status,stop);
 try{plan=await call('folderCompare',{sessionId:pane.id,scanId,localDirectory:pane.localPath,remoteDirectory:pane.remotePath});scanning=false;if(!active||!body.contains(status))return;stop.remove();status.textContent=plan.items.length+' elementi confrontati · '+plan.skippedLinks+' collegamenti esclusi';
 }catch(error){scanning=false;if(active){status.textContent=error.message;stop.textContent='Chiudi'}return}
 const tools=document.createElement('div');tools.className='sync-tools';
 const direction=field(tools,'Direzione dei trasferimenti','sync-direction','upload','text',[['upload','PC → Server'],['download','Server → PC']]);
 const filter=field(tools,'Filtra percorso','sync-filter','');filter.type='search';
 const identical=document.createElement('label');identical.className='check-label';const showAll=document.createElement('input');showAll.type='checkbox';identical.append(showAll,'Mostra anche elementi identici');tools.append(identical);body.append(tools);
 const list=document.createElement('div');list.className='sync-list';list.tabIndex=0;list.setAttribute('aria-label','Differenze nelle cartelle');body.append(list);
 const summary=document.createElement('p');summary.setAttribute('aria-live','polite');
 const apply=button('Trasferisci selezionati',async()=>{if(!chosen.size)return;apply.disabled=true;try{const result=await call('folderSync',{id:plan.id,sessionId:pane.id,direction:direction.value,paths:[...chosen]});if(result.cancelled){apply.disabled=false;return}modal.close();toast(result.queued+' elementi aggiunti alla coda');fileQueue=await call('fileQueue');renderFileQueues()}catch(error){toast(error.message,true);apply.disabled=false}},'primary');
 function eligible(item){return item.transferable&&!!item[direction.value==='upload'?'local':'remote']}
 function totals(){let bytes=0;for(const item of plan.items)if(chosen.has(item.relative))bytes+=item[direction.value==='upload'?'local':'remote']?.size||0;summary.textContent=chosen.size+' selezionati · '+formatSize(bytes)+' · I file diversi selezionati saranno sostituiti';apply.disabled=!chosen.size}
 function render(){list.replaceChildren();for(const item of plan.items){if(!showAll.checked&&['Identico','Cartella presente'].includes(item.status)||!item.relative.toLowerCase().includes(filter.value.toLowerCase()))continue;const row=document.createElement('label');row.className='sync-row';const check=document.createElement('input');check.type='checkbox';check.checked=chosen.has(item.relative);check.disabled=!eligible(item);check.setAttribute('aria-label','Trasferisci '+item.relative);check.onchange=()=>{if(check.checked)chosen.add(item.relative);else chosen.delete(item.relative);totals()};const name=document.createElement('span');name.className='sync-path';name.textContent=item.relative;const info=document.createElement('small');info.textContent=item.status;const stamp=entry=>!entry?'Assente':entry.link?'Collegamento':entry.directory?'Cartella':formatSize(entry.size)+' · '+new Date(entry.modified).toLocaleString('it-IT');const sides=document.createElement('small');sides.className='sync-stamps';sides.textContent='PC: '+stamp(item.local)+' | Server: '+stamp(item.remote);row.append(check,name,info,sides);list.append(row)}if(!list.children.length)list.textContent='Nessun elemento corrisponde ai filtri.';totals()}
 function defaults(){chosen.clear();for(const item of plan.items)if(eligible(item))chosen.add(item.relative);render()}
 direction.onchange=defaults;filter.oninput=render;showAll.onchange=render;
 const actions=document.createElement('div');actions.className='form-actions';actions.append(button('Seleziona differenze',defaults),button('Deseleziona tutti',()=>{chosen.clear();render()}),button('Annulla',()=>modal.close()),apply);body.append(summary,actions);defaults();
}
