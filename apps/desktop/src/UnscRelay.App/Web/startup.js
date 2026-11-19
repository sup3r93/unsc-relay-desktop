'use strict';
let vaultPromptPromise=null;
function requestVaultUnlock(reason='Sblocca l’archivio per usare le chiavi e le password salvate.'){
 if(state.vault.unlocked)return Promise.resolve(true);if(vaultPromptPromise)return vaultPromptPromise;
 window.vaultPromptActive=true;vaultPromptPromise=new Promise(resolve=>{
  const body=showModal(state.vault.exists?'Sblocca archivio':'Configura archivio','ACCESSO LOCALE');notice(body,reason);notice(body,uiText('Accedi online con il tuo account e la 2FA oppure usa Windows Hello, se lo hai abilitato manualmente su questo PC. Puoi continuare senza sbloccare e farlo prima di una connessione che usa credenziali salvate.'));let finished=false;
  const finish=value=>{if(finished)return;finished=true;modal.removeEventListener('close',closed);modal.close();vaultPromptPromise=null;window.vaultPromptActive=false;resolve(value)};const closed=()=>finish(false);modal.addEventListener('close',closed,{once:true});const actions=document.createElement('div');actions.className='form-actions';
  async function unlock(action){for(const b of actions.children)b.disabled=true;try{update(await call(action));if(state.vault.unlocked)finish(true)}catch{}finally{for(const b of actions.children)b.disabled=false}}
  if(state.vault.windowsHello)actions.append(localizedButton('Sblocca con Windows Hello',()=>unlock('helloUnlock'),'primary'));actions.append(localizedButton('Accedi con il tuo account',()=>unlock('vaultUnlock'),state.vault.windowsHello?'':'primary'));actions.append(localizedButton('Più tardi',()=>finish(false)));body.append(actions);
 });return vaultPromptPromise
}
function ensureConnectionVault(profile){if(profile.protocol==='local'||profile.protocol==='native'&&!profile.keyId&&!profile.passwordId)return Promise.resolve(true);const seen=new Set();let current=profile;while(current&&!seen.has(current.id)){seen.add(current.id);const mode=current.authentication==='auto'?(current.keyId?'key':'password'):current.authentication;if((mode==='key'||mode==='password'&&current.passwordId)&&!state.vault.unlocked)return requestVaultUnlock('La connessione «'+current.name+'» usa credenziali nell’archivio bloccato. Sbloccalo per continuare.');current=current.jumpHost?state.data.profiles.find(p=>p.id===current.jumpHost):null;}return Promise.resolve(true)}
function startupVaultPrompt(){if(!state.diagnostic&&state.vault.exists&&!state.vault.unlocked)return requestVaultUnlock('Benvenuto in UNSC Relay. Sblocca l’archivio delle chiavi per iniziare.');return Promise.resolve(true)}
