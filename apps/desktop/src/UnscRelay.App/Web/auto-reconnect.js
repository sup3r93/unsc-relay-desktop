 'use strict';
const recoveryGroups=new Map();
function groupPanes(profileId){return [...panes.values()].filter(p=>p.profile.id===profileId&&!p.editor&&(p.fileWorkspace||p.profile.protocol==='ssh'))}
function recoveryGroup(pane){let g=recoveryGroups.get(pane.profile.id);if(!g){g={enabled:false,attempts:0,timer:null,running:null};recoveryGroups.set(pane.profile.id,g)}return g}
function resetAutoReconnect(pane){const g=recoveryGroup(pane);clearTimeout(g.timer);g.timer=null;g.attempts=0;pane.connectError=null}
function cancelAutoReconnect(pane){if(!['ssh','sftp'].includes(pane.profile.protocol))return;const others=groupPanes(pane.profile.id).filter(p=>p!==pane);if(others.length)return;const g=recoveryGroups.get(pane.profile.id);if(g){clearTimeout(g.timer);g.enabled=false;recoveryGroups.delete(pane.profile.id)}rpc('recoveryPolicy',{profileId:pane.profile.id,enabled:false}).catch(()=>{})}
function recoveryEnabled(profileId,enabled){for(const p of groupPanes(profileId)){p.autoEnabled=enabled;if(p.autoCheckbox)p.autoCheckbox.checked=enabled}}
function wireAutoReconnect(pane){
 const g=recoveryGroup(pane);pane.autoEnabled=g.enabled;
 const label=document.createElement('label');label.className='auto-reconnect check-label';const c=document.createElement('input');c.type='checkbox';c.checked=g.enabled;c.setAttribute('aria-label','Riconnessione automatica '+pane.profile.name);label.append(c,'Auto SSH/SFTP');label.title='Un solo ripristino per profilo, con 3 tentativi a 10, 30 e 60 secondi. Riprende soltanto la coda interrotta dalla rete. Nessun comando o hook viene reinviato.';pane.container.querySelector('.pane-header').append(label);pane.autoCheckbox=c;
 c.onchange=async()=>{const value=c.checked;try{await call('recoveryPolicy',{profileId:pane.profile.id,enabled:value});g.enabled=value;resetAutoReconnect(pane);recoveryEnabled(pane.profile.id,value);if(value&&!pane.hadConnection)toast('Riconnetti manualmente una volta: Auto interverrà sulle successive interruzioni.')}catch{c.checked=g.enabled}};
}
function autoReconnectStatus(pane,status){if(status.connected===false&&status.retryable===true)recoveryNeeded({profileId:pane.profile.id,retryable:true,text:status.text})}
function recoveryNeeded(message){
 const members=groupPanes(message.profileId);if(!members.length)return;const g=recoveryGroup(members[0]);
 for(const p of members){if(!p.connecting)p.status.textContent=message.text||'Connessione interrotta: coda in attesa.';p.reconnect.hidden=false;}
 if(message.retryable&&g.enabled&&members.some(p=>p.hadConnection))scheduleAutoReconnect(members[0]);
}
function stopRecovery(pane,text){const g=recoveryGroup(pane);clearTimeout(g.timer);g.timer=null;g.enabled=false;recoveryEnabled(pane.profile.id,false);rpc('recoveryPolicy',{profileId:pane.profile.id,enabled:false}).catch(()=>{});for(const p of groupPanes(pane.profile.id))p.status.textContent='Auto sospesa: '+text;toast(pane.profile.name+': '+text,true)}
function scheduleAutoReconnect(pane){
 const g=recoveryGroup(pane);if(!g.enabled||g.timer||g.running||!groupPanes(pane.profile.id).length)return;
 if(g.attempts>=3){stopRecovery(pane,'tentativi esauriti. Riconnetti manualmente.');return}
 const seconds=[10,30,60][g.attempts];for(const p of groupPanes(pane.profile.id))p.status.textContent='Ripristino SSH/SFTP tra '+seconds+'s ('+(g.attempts+1)+'/3)';
 g.timer=setTimeout(async()=>{g.timer=null;if(!g.enabled||!groupPanes(pane.profile.id).length)return;g.attempts++;try{const result=await recoverProfilePanes(pane,true);if(!result.success){if(result.retryable)scheduleAutoReconnect(pane);else stopRecovery(pane,result.error||'Riconnetti manualmente.')}}catch(e){if(e.retryable)scheduleAutoReconnect(pane);else stopRecovery(pane,e.message)}},seconds*1000);
}
async function recoverProfilePanes(pane,automatic=false){
 const g=recoveryGroup(pane);if(g.running)return g.running;if(!automatic)resetAutoReconnect(pane);
 const run=async()=>{
  const members=groupPanes(pane.profile.id),saved=state.data.profiles.find(p=>p.id===pane.profile.id);if(!saved)throw Error('Profilo eliminato: apri una nuova connessione.');
  if(automatic&&JSON.stringify(saved)!==JSON.stringify(pane.profile))throw Error('Profilo modificato: apri una nuova connessione.');
  if(!automatic&&!await ensureConnectionVault(saved))return{success:false,retryable:false,error:'Archivio bloccato.'};
  for(const p of members){p.reconnecting=true;p.reconnect.disabled=true;p.status.textContent='Ripristino coordinato SSH/SFTP…'}
  try{
   const result=await call('recoverProfile',{profileId:pane.profile.id,automatic,cols:pane.term.cols||100,rows:pane.term.rows||30});
   for(const item of result.sessions){const p=panes.get(item.sessionId);if(!p)continue;p.connected=item.connected;p.files=item.filesAvailable;p.hadConnection||=item.connected;p.reconnect.hidden=item.connected&&item.filesAvailable;p.status.textContent=item.connected?(p.fileWorkspace?'SFTP ripristinato':'Connesso'):'Non connesso';if(item.terminalRestarted&&!p.fileWorkspace)p.term.writeln('\r\n\x1b[32m--- NUOVA SESSIONE SSH · nessun comando reinviato ---\x1b[0m');if(p.fileWorkspace&&p.files)await p.refreshFiles(true);else if(p.connected){p.fit.fit();}}
   fileQueue=await call('fileQueue');window.renderFileQueues?.();if(result.success){g.attempts=0;clearTimeout(g.timer);g.timer=null;}else{for(const p of members)p.status.textContent='Ripristino non completato: '+result.error;if(!automatic)toast(result.error||'Riconnetti manualmente.',true)}return result;
  }finally{for(const p of members){p.reconnecting=false;p.reconnect.disabled=false}}
 };
 g.running=run();try{return await g.running}finally{g.running=null}
}
