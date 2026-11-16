'use strict';
function waitForAccountActivation(){
 return new Promise(resolve=>{
  const gate=accessGate('COLLEGA IL TUO ACCOUNT','UNSC RELAY / ATTIVAZIONE DISPOSITIVO');gate.classList.add('access-login');gate.removeAttribute('role');gate.prepend(createAccessScene());
  const card=document.createElement('section');card.className='access-card';const h=document.createElement('h2');h.textContent='La tua console, su tutti i dispositivi.';card.append(h);
  const text=document.createElement('p');text.textContent='Il primo accesso richiede un account registrato e abilitato sul sito UNSC. I dati già presenti su questo PC rimangono intatti. Dopo l’accesso puoi abilitare manualmente Windows Hello per usare l’app offline.';card.append(text);
  const status=document.createElement('p');status.setAttribute('role','status');
  const login=button('Accedi al tuo account',async()=>{login.disabled=true;status.textContent='Completa l’accesso nella schermata protetta di Relay.';try{update(await rpc('accountLogin'));if(state.account?.requiresLink)throw Error('Attivazione non completata.');resolve()}catch(e){status.textContent=e.message;login.disabled=false}},'primary access-auth-button');login.prepend(accessIcon('password'));
  const register=button('Crea account',async()=>{register.disabled=true;try{update(await rpc('accountRegister'));if(!state.account?.requiresLink)resolve()}catch(e){status.textContent=e.message}finally{register.disabled=false}},'access-auth-button');register.disabled=true;register.title='Verifica disponibilità della registrazione';
  rpc('accountAvailability').then(v=>{register.disabled=!v.registrationAllowed;register.title=v.registrationAllowed?'Crea il tuo account personale':'Registrazioni temporaneamente chiuse';}).catch(()=>{register.title='Registrazione non disponibile offline';});
  const foot=document.createElement('p');foot.className='access-footnote';foot.textContent='La sessione di Relay è indipendente dal browser. Gli account amministrativi sono riservati al sito.';
  card.append(login,register,status,foot,accessUpdateButton(),button('Esci da Relay',()=>rpc('applicationExit')));gate.append(card);
 });
}
function cloudSetupDialog(){
 return new Promise(resolve=>{
  const body=showModal('Collega l’archivio online','ACCOUNT / CIFRATURA SUL DISPOSITIVO');
  notice(body,'Account: '+(state.account?.name||'UNSC')+'. Prima di trasferire i dati verrà salvata una copia locale di recupero. Le chiavi restano cifrate prima dell’invio a OpenBao.');
  notice(body,'Se questo è il primo dispositivo, scegli una password per l’archivio online e ripetila. Se l’archivio esiste già, inserisci la sua password: eventuali differenze con il PC richiederanno una scelta. Può coincidere con la password locale, ma è distinta dalla password dell’account sul sito.');
  const form=document.createElement('form');const password=field(form,'Password archivio online','password','','password');password.autocomplete='new-password';password.required=true;password.minLength=12;password.maxLength=4096;
  const repeat=field(form,'Ripeti password (per la prima creazione)','confirmation','','password');repeat.autocomplete='new-password';repeat.maxLength=4096;
  const status=document.createElement('p');status.setAttribute('role','status');form.append(status);const actions=document.createElement('div');actions.className='form-actions';const save=document.createElement('button');save.type='submit';save.className='primary';save.textContent='Collega e sincronizza';
  let finished=false;const finish=()=>{if(finished)return;finished=true;modal.removeEventListener('close',finish);password.value='';repeat.value='';resolve()};modal.addEventListener('close',finish,{once:true});
  actions.append(button('Più tardi',()=>modal.close()),save);form.append(actions);form.onsubmit=async e=>{e.preventDefault();save.disabled=true;status.textContent='Cifratura e collegamento in corso…';try{const p=password.value,c=repeat.value;password.value='';repeat.value='';update(await rpc('accountSetup',{password:p,confirmation:c}));modal.close();if(state.account.conflict)settingsDialog('account');else toast(state.account.message)}catch(e){status.textContent=e.message;save.disabled=false}};body.append(form);password.focus();
 });
}
function mountAccountSettings(section){
 const h=document.createElement('h4');h.textContent='Dispositivo e archivio personale';section.append(h);
 const name=document.createElement('p'),status=document.createElement('p');status.setAttribute('role','status');status.setAttribute('aria-live','polite');section.append(name,status);
 const actions=document.createElement('div');actions.className='settings-actions';
 const login=button('Accedi di nuovo nell’app',async()=>{try{update(await call('accountLogin'));render()}catch{}});
 const setup=button('Collega / sblocca archivio online',async()=>{if(!state.vault.unlocked&&!await requestVaultUnlock('Sblocca l’archivio locale prima di collegarlo a quello online.'))return;await cloudSetupDialog();});
 const sync=button('Sincronizza adesso',async()=>{try{update(await call('accountSync'));render()}catch{}});
 const site=button('Apri area riservata',()=>call('accountSite'));actions.append(button('Profilo utente',()=>accountProfileDialog()),login,setup,sync,site,button('Esci dall’account',()=>logoutAccount(),'danger'));section.append(actions);
 const conflict=document.createElement('section');conflict.className='settings-card';const title=document.createElement('h4');title.textContent='Due copie modificate';const info=document.createElement('p');info.textContent='La copia locale e quella online hanno modifiche diverse. Prima di sostituire i dati verrà conservata una copia di recupero. Puoi anche rimandare la scelta e continuare offline.';conflict.append(title,info);
 const choices=document.createElement('div');choices.className='settings-actions';conflict.append(choices);
 for(const [value,label]of [['online','Usa la copia online su questo PC'],['local','Usa la copia di questo PC online']])choices.append(button(label,async()=>{try{update(await call('accountSync',{resolution:value}));render()}catch{}}));section.append(conflict);
 const note=document.createElement('p');note.textContent='Controllo automatico ogni 30 secondi, quando l’archivio è sbloccato. In assenza di rete le modifiche restano sul PC e ripartono al collegamento successivo. Windows Hello, token GitHub, editor, audio, percorsi locali e registrazioni restano specifici del dispositivo.';section.append(note);
 function render(){const a=state.account||{};name.textContent='Account collegato: '+(a.name||'Nessuno')+' · '+(a.online?'Accesso online verificato':'Accesso online da verificare / copia locale disponibile');status.textContent=a.message||'Collega l’account per iniziare.';conflict.hidden=!a.conflict;sync.disabled=!a.ready||a.busy;setup.disabled=!a.linked||a.busy;login.disabled=!!a.busy;}
 render();const listener=()=>{if(section.isConnected)render();else window.removeEventListener('relay-account',listener)};window.addEventListener('relay-account',listener);
}
if(native){const observer=new MutationObserver(()=>{if(state?.account?.linked)rpc('accountUiBusy',{busy:modal.open}).catch(()=>{})});observer.observe(modal,{attributes:true,attributeFilter:['open']});}

async function logoutAccount(){
 try{const snapshot=await call('accountLogout');if(!snapshot.account?.requiresLink)return;modal.close();
  for(const pane of panes.values()){window.cancelAutoReconnect?.(pane);pane.term?.dispose();pane.container?.remove();pane.tab?.remove();}panes.clear();activeId=null;refreshStage();
  update(snapshot);document.body.classList.add('startup-covered');stopAppMusic();await finishApplicationStartup();startAppMusic();
 }catch{}
}
async function accountProfileDialog(){
 const body=showModal('Il tuo account','IDENTITÀ PERSONALE / UNSC RELAY');modal.classList.add('account-profile-modal');
 const intro=document.createElement('p');intro.textContent='Questo account è usato soltanto da Relay. Puoi mantenere un altro accesso nel browser.';body.append(intro);
 const status=document.createElement('p');status.setAttribute('role','status');status.textContent='Caricamento del profilo…';body.append(status);
 const details=document.createElement('dl');details.className='account-profile-details';body.append(details);
 const actions=document.createElement('div');actions.className='account-security-grid';body.append(actions);
 async function load(){try{const p=await call('accountProfile');details.replaceChildren();for(const[label,value]of[['Nome utente',p.username],['Nome',[p.firstName,p.lastName].filter(Boolean).join(' ')||'Non indicato'],['Email',p.email||'Non indicata'],['Verifica email',p.emailVerified?'Verificata':'Da verificare']]){const row=document.createElement('div'),dt=document.createElement('dt'),dd=document.createElement('dd');dt.textContent=label;dd.textContent=value;row.append(dt,dd);details.append(row);}status.textContent='Account personale collegato. La password dell’archivio resta separata.';}catch{status.textContent='Profilo online non disponibile. I dati cifrati sul PC restano conservati.';}}
 for(const[action,title,description]of [['profile','Dati personali','Modifica nome e indirizzo email.'],['password','Password','Cambia la password dell’account.'],['otp','Autenticazione a due fattori','Configura i codici di un’app di autenticazione.'],['passkey','Passkey','Registra una passkey disponibile sul dispositivo.'],['security-key','Chiave FIDO2','Aggiungi una chiave di sicurezza compatibile.']]){
  const card=document.createElement('section'),heading=document.createElement('h3'),text=document.createElement('p');card.className='account-security-card';heading.textContent=title;text.textContent=description;
  const actionButton=button('Gestisci '+title.toLowerCase(),async()=>{actionButton.disabled=true;try{update(await call('accountSecurity',{action}));await load();}finally{actionButton.disabled=false}});card.append(heading,text,actionButton);actions.append(card);
 }
 const logout=button('Esci dall’account',()=>logoutAccount(),'danger');body.append(logout);await load();
}
$('account-btn')?.addEventListener('click',()=>accountProfileDialog());
