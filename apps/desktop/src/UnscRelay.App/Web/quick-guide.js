'use strict';
function quickGuideDialog(){const body=showModal(uiText('Guida rapida'),uiText('PRIMI PASSI / ACCESSO SICURO'),null,true);buildQuickGuide(body,uiText,true)}

function buildQuickGuide(body,text,desktop){
 const intro=document.createElement('p');intro.textContent=text('Scegli un argomento. La guida non avvia connessioni e non modifica account o archivi.');body.append(intro);
 const topics=[['Account e accesso',[
 'Seleziona un account salvato oppure aggiungine un altro. La selezione non sostituisce la verifica dell’identità.',
 'Online usa email, password dell’account e verifica a due fattori. Non usare la vecchia password locale dell’app.',
 desktop?'Per accedere offline, abilita Windows Hello manualmente su un account Windows personale. Un account Windows condiviso può consentire ad altre persone di sbloccare Relay.':'Il sito richiede una connessione. Windows Hello offline riguarda soltanto l’app desktop.'
 ]],['Connessioni sicure',[
 'Le connessioni remote si configurano nell’app desktop. Scegli il protocollo e controlla host, porta e metodo di autenticazione.',
 'Prima di accettare una nuova chiave host, confronta l’impronta con quella fornita dal gestore del server tramite un canale affidabile.',
 'La mappa mostra il percorso configurato. Una porta raggiungibile non dimostra l’identità del server.'
 ]],['Archivi e condivisioni',[
 'Ogni archivio ha un solo titolare. Il titolare invita gli altri account, sceglie lettura o scrittura e può impostare una scadenza.',
 'Il destinatario accede con il proprio account. Per aprire i contenuti cifrati deve preparare il dispositivo e ottenere l’autorizzazione del titolare.',
 'La revoca blocca gli accessi online successivi. Non cancella copie già scaricate, esportazioni o dispositivi offline.',
 'Prima di cambiare account o archivio, salva le modifiche oppure conserva una copia di recupero cifrata.'
 ]]];
 for(const [title,steps] of topics){const details=document.createElement('details'),summary=document.createElement('summary'),list=document.createElement('ol');summary.textContent=text(title);for(const step of steps){const item=document.createElement('li');item.textContent=text(step);list.append(item)}details.append(summary,list);body.append(details)}
 body.classList.add('quick-guide');
}

function mountFirstRunGuide(host,text,open){
 const key='relay.quick-guide.v1';try{if(localStorage.getItem(key)==='dismissed')return}catch{}
 const card=document.createElement('section');card.className='first-run-guide';card.dataset.firstRunGuide='';const title=document.createElement('h3');title.textContent=text('Inizia da qui');const description=document.createElement('p');description.textContent=text('Una guida breve per account, connessioni e condivisioni. Riaprila con Guida rapida.');const actions=document.createElement('div');actions.className='first-run-actions';const guide=document.createElement('button'),dismiss=document.createElement('button');guide.type=dismiss.type='button';guide.textContent=text('Apri guida rapida');dismiss.textContent=text('Non mostrare più');for(const [element,key]of [[title,'Inizia da qui'],[description,'Una guida breve per account, connessioni e condivisioni. Riaprila con Guida rapida.'],[guide,'Apri guida rapida'],[dismiss,'Non mostrare più']])element.dataset.interfaceKey=key;guide.onclick=open;dismiss.onclick=()=>{try{localStorage.setItem(key,'dismissed')}catch{}const next=host.querySelector('button:not([data-first-run-guide] button)');card.remove();if(next instanceof HTMLElement)next.focus()};actions.append(guide,dismiss);card.append(title,description,actions);host.append(card);
}
mountFirstRunGuide($('welcome'),uiText,quickGuideDialog);
