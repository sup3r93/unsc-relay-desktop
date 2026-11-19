
'use strict';
function installAccountMenu(){
 const trigger=$('account-btn'),menu=document.createElement('div');menu.id='account-menu';menu.className='account-dropdown';menu.setAttribute('popover','auto');interfaceAttribute(menu, 'aria-label', 'Menu account');
 trigger.setAttribute('popovertarget',menu.id);trigger.dataset.interfaceLabel='Apri menu account';trigger.dataset.interfaceTitle='Menu account';interfaceAttribute(trigger, 'aria-label', 'Apri menu account');interfaceAttribute(trigger, 'title', 'Menu account');trigger.replaceChildren();const avatar=document.createElement('span');avatar.className='account-avatar';avatar.textContent='U';avatar.setAttribute('aria-hidden','true');trigger.append(avatar);
 const identity=document.createElement('div');identity.className='account-dropdown-identity';const name=document.createElement('strong'),email=document.createElement('small');identity.append(name,email);menu.append(identity);
 menu.append(localizedButton('Cambia account',()=>{menu.hidePopover();accountChooserDialog()},'account-switch'));
 menu.append(localizedButton('Personalizza account',()=>{menu.hidePopover();customizeCurrentAccount()}));
 menu.append(localizedButton('Il tuo account',()=>{menu.hidePopover();accountProfileDialog()}));
 const settings=$('settings-btn');interfaceLabel(settings,'Impostazioni');settings.dataset.interfaceLabel='Impostazioni';interfaceAttribute(settings, 'aria-label', 'Impostazioni');menu.append(settings,$('music-btn'));
 menu.append(localizedButton('Assistenza e segnalazioni',()=>{menu.hidePopover();supportDialog()}));
 const presentation=installPresentationMode({selectors:'#profiles,#connection-views,#profile-search,#tabs,#stage,#status-detail,#toasts,.account-dropdown-identity,.account-avatar,#modal-body,#modal-title,dialog .attachment-preview-body,dialog header h2',text:uiText,nativeMask:async enabled=>{const result=await rpc('presentationMode',{enabled});if(result?.enabled!==enabled)throw new Error(uiText('Operazione annullata.'))}});menu.append(presentation.button);
 menu.append(localizedButton('Notifiche',()=>{menu.hidePopover();notificationsDialog()}));
 menu.append(localizedButton('Centro operazioni',()=>{menu.hidePopover();operationsDialog()}));
 menu.append(localizedButton('Dati e privacy',()=>{menu.hidePopover();privacyDialog()}));
 menu.append(localizedButton('Guida rapida',()=>{menu.hidePopover();quickGuideDialog()}));
 menu.append(localizedButton('Centro sincronizzazione',()=>{menu.hidePopover();synchronizationCenterDialog()}));
 menu.append(localizedButton('Archivi collegati',()=>{menu.hidePopover();archivesDialog()}));
 menu.append(localizedButton('Esci dall’account',()=>{menu.hidePopover();logoutAccount()},'danger'));
 document.body.append(menu);trigger.onclick=()=>{};
 async function refresh(){const account=state?.account??{};let saved;try{saved=(await rpc('workspaceList')).find(a=>a.current)}catch{}const displayName=saved?.alias||account.name||account.email;if(displayName){delete name.dataset.interfaceKey;name.textContent=displayName}else interfaceLabel(name,'Account personale');avatar.dataset.color=saved?.color||'olive';if(account.email){delete email.dataset.interfaceKey;email.textContent=account.email}else interfaceLabel(email,'Seleziona un account');avatar.textContent=Array.from(account.name||account.email||'U')[0].toLocaleUpperCase(document.documentElement.lang);}
 menu.addEventListener('beforetoggle',event=>{if(event.newState==='open'){refresh();const r=trigger.getBoundingClientRect();menu.style.top=(r.bottom+8)+'px';menu.style.right=Math.max(12,innerWidth-r.right)+'px';menu.style.left='auto'}});
 menu.addEventListener('click',event=>{if(event.target.closest('#settings-btn')){menu.hidePopover();modal.addEventListener('close',()=>trigger.focus(),{once:true})}});
 menu.onkeydown=e=>{const items=[...menu.querySelectorAll('button:not(:disabled)')],i=items.indexOf(document.activeElement);if(e.key==='ArrowDown'||e.key==='ArrowUp'){e.preventDefault();items[(i+(e.key==='ArrowDown'?1:items.length-1)+items.length)%items.length]?.focus()}else if(e.key==='Home'){e.preventDefault();items[0]?.focus()}else if(e.key==='End'){e.preventDefault();items.at(-1)?.focus()}};
 refresh();
}
installAccountMenu();
async function selectStartupAccount(){
 const saved=await rpc('workspaceList');return new Promise(resolve=>{
  const gate=accessGate('Seleziona un account','UNSC RELAY / ACCOUNT');gate.classList.add('access-login');gate.removeAttribute('role');gate.prepend(createAccessScene());const card=document.createElement('section');card.className='access-card account-start';const title=document.createElement('h2');interfaceLabel(title,'Seleziona un account');card.append(title);createLanguageSelector(card);const status=document.createElement('p');status.setAttribute('role','status');
  const accounts=saved.length?saved:[{id:null,current:true,email:state.account?.email||'',name:'',connected:!!state.account?.linked}];
  for(const account of accounts){const row=button('',async()=>{row.disabled=true;try{if(account.current){resolve();return}await rpc('workspaceSwitch',{id:account.id});row.disabled=false}catch(e){status.textContent=e.message;row.disabled=false}},'account-choice');const name=document.createElement('strong');const displayName=account.alias||account.email||account.name;if(displayName)name.textContent=displayName;else interfaceLabel(name,'Account su questo PC');row.dataset.accountColor=account.color||'olive';const detail=document.createElement('span');if(account.alias)detail.append(document.createTextNode(account.email+' · '));detail.append(interfaceLabel(document.createElement('span'),account.connected?'Seleziona per sbloccare':'Accedi per collegare questo account'));row.append(name,detail);card.append(row)}
  const form=document.createElement('form');form.hidden=true;const label=document.createElement('label');interfaceLabel(label,'Email del tuo account');const email=document.createElement('input');email.type='email';email.required=true;email.autocomplete='email';email.maxLength=254;label.append(email);const submit=localizedButton('Continua',()=>{} ,'primary');submit.type='submit';form.append(label,submit);form.onsubmit=async e=>{e.preventDefault();submit.disabled=true;try{await rpc('workspaceAdd',{email:email.value.trim()})}catch(error){status.textContent=error.message}finally{submit.disabled=false}};
  card.append(localizedButton('Usa un altro account',()=>{form.hidden=false;email.focus()}),form,status,accessUpdateButton(),localizedButton('Esci da Relay',()=>rpc('applicationExit')));gate.append(card);card.querySelector('button')?.focus();
 });
}

async function customizeCurrentAccount(){
 const account=(await rpc('workspaceList')).find(a=>a.current);if(!account){toast(uiText('Collega prima il tuo account.'));return}
 const body=showModal(uiText('Personalizza account'),'IDENTITÀ VISIVA / SOLO SU QUESTO PC',null,true);notice(body,uiText('Il nome personale e il colore aiutano a distinguere gli account. Non cambiano il nome, l’email o i permessi dell’identità.'));
 const form=document.createElement('form'),alias=field(form,uiText('Nome personale'),'alias',account.alias||'');alias.maxLength=80;interfaceAttribute(alias, 'placeholder', 'Personale, Lavoro, Test…');const color=field(form,uiText('Colore'),'color',account.color||'olive','text',[['olive','Oliva'],['blue','Blu'],['purple','Viola'],['orange','Arancione'],['teal','Petrolio'],['rose','Rosa']].map(([value,label])=>[value,uiText(label)]));const status=document.createElement('p');status.setAttribute('role','status');const save=localizedButton('Salva personalizzazione',()=>{},'primary');save.type='submit';form.append(save,status);body.append(form);form.onsubmit=async e=>{e.preventDefault();save.disabled=true;try{await rpc('workspaceCustomize',{id:account.id,alias:alias.value.trim(),color:color.value});status.textContent=uiText('Personalizzazione salvata.')}catch(error){status.textContent=error.message}finally{save.disabled=false}};alias.focus();
}
