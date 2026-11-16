 'use strict';
function accessGate(title,message){let gate=$('access-gate');if(!gate){gate=document.createElement('section');gate.id='access-gate';gate.setAttribute('role','status');document.body.append(gate)}gate.replaceChildren();const ring=document.createElement('div');ring.className='access-ring';const logo=document.createElement('img');logo.src='brand/relay-mark.svg';logo.alt='UNSC Relay';ring.append(logo);const h=document.createElement('h1');h.textContent=title;const p=document.createElement('p');p.textContent=message;gate.append(ring,h,p);return gate}
function accessIcon(kind){const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');svg.setAttribute('viewBox','0 0 24 24');svg.setAttribute('aria-hidden','true');const path=document.createElementNS(svg.namespaceURI,'path');path.setAttribute('d',kind==='hello'?'M12 3a7 7 0 0 0-7 7v4m14 0v-4a7 7 0 0 0-7-7M8 20v-9a4 4 0 0 1 8 0v9M11 21V11h2v10':kind==='exit'?'M10 4H4v16h6m4-13 5 5-5 5m-5-5h10':'M6 11V7a6 6 0 0 1 12 0v4M4 11h16v11H4zM12 15v3');svg.append(path);return svg}
function createAccessScene(){const scene=document.createElement('div');scene.className='access-scene';scene.setAttribute('aria-hidden','true');for(let i=0;i<3;i++){const orbit=document.createElement('div');orbit.className='access-orbit orbit-'+i;scene.append(orbit)}const crest=document.createElement('img');crest.src='brand/relay-mark.svg';crest.className='access-crest';scene.append(crest);return scene;}
function waitForApplicationLogin(){
 return new Promise(resolve=>{
  const gate=accessGate('IDENTIFICAZIONE RICHIESTA','La tua console. Le tue connessioni.');gate.classList.add('access-login');gate.removeAttribute('role');
  gate.prepend(createAccessScene());
  const card=document.createElement('section');card.className='access-card';const kicker=document.createElement('small');kicker.textContent='FLEETCOM / IDENTITÀ LOCALE';const heading=document.createElement('h2');heading.textContent='Bentornato, Comandante.';const description=document.createElement('p');description.textContent='Accedi per sbloccare la console e il tuo archivio cifrato.';card.append(kicker,heading,description);
  const form=document.createElement('form');const label=document.createElement('label');label.textContent='Password principale';const password=document.createElement('input');password.type='password';password.autocomplete='current-password';password.maxLength=4096;password.required=true;password.setAttribute('aria-label','Password principale');label.append(password);form.append(label);
  const status=document.createElement('p');status.className='access-error';status.setAttribute('role','status');status.setAttribute('aria-live','polite');const controls=[];
  async function authenticate(method){if(controls.some(b=>b.disabled))return;controls.forEach(b=>b.disabled=true);status.textContent='Verifica in corso…';try{const value=password.value;password.value='';update(await rpc('applicationAuthenticate',{method,...(method==='password'?{password:value}:{})}));if(state.appLocked)throw Error('Accesso non completato.');resolve()}catch(e){status.textContent=e.message||'Verifica non riuscita. Riprova.';controls.forEach(b=>b.disabled=false);if(method==='password')password.focus()}}
  const submit=document.createElement('button');submit.type='submit';submit.className='primary access-auth-button';submit.append(accessIcon('password'),'Accedi con password');form.append(submit);controls.push(submit);form.onsubmit=e=>{e.preventDefault();authenticate('password')};card.append(form);
  if(state.vault.windowsHello){const hello=button('',()=>authenticate('hello'),'access-auth-button');hello.append(accessIcon('hello'),'Accedi con Windows Hello');card.append(hello);controls.push(hello)}
  card.append(status);const info=document.createElement('p');info.className='access-footnote';info.textContent='Una sola verifica sblocca app e archivio. Puoi restare su questa schermata senza limiti di tempo.';card.append(info);const exit=button('',()=>rpc('applicationExit'),'access-exit');exit.append(accessIcon('exit'),'Esci da Relay');card.append(exit);controls.push(exit);gate.append(card);password.focus();
 });
}
async function finishApplicationStartup(){
 const linkedNow=!!state.account?.requiresLink;
 if(linkedNow)await waitForAccountActivation();
 if(state.appLocked){await waitForApplicationLogin();accessGate('ACCESSO AUTORIZZATO','FLEETCOM / Collegamento alla console');await new Promise(resolve=>setTimeout(resolve,2200));$('access-gate')?.remove();}
 $('access-gate')?.remove();
 await fadeStartupTheme();document.body.classList.remove('startup-covered');await rpc('consoleReady');
 if(state.account?.linked&&!state.account.ready){if(!state.vault.unlocked)await requestVaultUnlock('Sblocca o crea l’archivio locale per migrare chiavi e connessioni sul tuo account.');if(state.vault.unlocked)await cloudSetupDialog();}
 else await startupVaultPrompt();
}
function accessSettingsDialog(){settingsDialog('chatgpt')}

let startupTheme=null,appMusic=null,startupEnding=false,startupTrackKind='primary';
function sequenceVolume(kind){const value=state?.data.settings[kind==='startup'?'startupVolume':'shutdownVolume'];return Number.isFinite(value)?Math.max(0,Math.min(1,value)):(kind==='startup'?.8:1)}
function backgroundTrack(){const audio=new Audio('media/background-theme.m4a');audio.loop=true;return audio}
function startStartupTheme(){
 if(startupTheme||startupEnding)return;startupTrackKind='primary';startupTheme=new Audio('media/startup-theme.mp3');startupTheme.loop=false;startupTheme.volume=sequenceVolume('startup');
 startupTheme.onended=()=>{if(startupEnding)return;startupTrackKind='fallback';startupTheme=backgroundTrack();startupTheme.volume=sequenceVolume('startup');startupTheme.play().catch(()=>{});};
 startupTheme.play().catch(()=>{});
}
async function musicFade(audio,target){const initial=audio.volume,started=performance.now();await new Promise(resolve=>{const step=()=>{const fraction=Math.min(1,(performance.now()-started)/1000);audio.volume=initial+(target-initial)*fraction;if(fraction>=1)resolve();else setTimeout(step,40)};step()});}
function updateMusicButton(){const enabled=!!state?.data.settings.appMusicEnabled;const btn=$('music-btn');btn.setAttribute('aria-pressed',String(enabled));btn.title=enabled?'Disattiva musica dell’app':'Attiva musica dell’app';btn.querySelector('span').textContent=enabled?'Musica ON':'Musica OFF';}
function stopAppMusic(){if(appMusic){appMusic.pause();appMusic.src='';appMusic=null}}
function appMusicVolume(){const v=state?.data.settings.appMusicVolume;return Number.isFinite(v)?Math.max(0,Math.min(1,v)):.35}
function startAppMusic(){updateMusicButton();if(!state?.data.settings.appMusicEnabled)return stopAppMusic();if(!appMusic)appMusic=backgroundTrack();appMusic.volume=appMusicVolume();appMusic.play().catch(()=>toast('Premi Musica per avviare la colonna sonora.',true));}
function stopStartupTheme(){startupEnding=true;if(startupTheme){startupTheme.onended=null;startupTheme.pause();startupTheme.src='';startupTheme=null;}}
async function fadeStartupTheme(){
 startupEnding=true;if(startupTrackKind==='fallback'&&startupTheme&&state.data.settings.appMusicEnabled){appMusic=startupTheme;startupTheme=null;await musicFade(appMusic,appMusicVolume());updateMusicButton();return;}
 if(startupTheme){const audio=startupTheme;await musicFade(audio,0);audio.pause();audio.src='';startupTheme=null;}startAppMusic();
}
$('music-btn').onclick=async()=>{try{const enabled=!state.data.settings.appMusicEnabled;update(await call('musicSet',{enabled}));startAppMusic();}catch{}};

function presentStartupFrame(){$('startup-curtain').hidden=true;rpc('startupPresented').catch(()=>{})}
