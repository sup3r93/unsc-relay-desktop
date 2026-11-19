 'use strict';
function connectionMapDialog(profile){
 const text=uiText,body=showModal(text('Mappa della connessione'),text('PERCORSO CONFIGURATO'),null,true);
 const note=document.createElement('p');note.textContent=text('Questa mappa descrive la configurazione salvata, non verifica raggiungibilità, identità o tunnel attivi.');body.append(note);
 const list=document.createElement('ol');list.className='connection-route';body.append(list);
 function node(title,detail){const item=document.createElement('li'),h=document.createElement('h3'),p=document.createElement('p');h.textContent=title;p.textContent=detail;item.append(h,p);list.append(item)}
 node(text('Questo PC'),text('Origine della connessione'));
 if(profile.protocol==='native'){node(profile.name,text('Il percorso del client nativo può dipendere da configurazioni esterne e non viene ricostruito.'));return}
 if(profile.protocol==='local'){node(profile.name,text('Sessione locale: nessun server remoto configurato.'));return}
 const route=[],seen=new Set();let current=profile,error='';
 while(current){if(seen.has(current.id)||route.length===4){error=text('Percorso incompleto: catena ciclica o troppo lunga.');break}seen.add(current.id);route.push(current);if(!current.jumpHost)break;current=state.data.profiles.find(p=>p.id===current.jumpHost);if(!current){error=text('Percorso incompleto: server intermedio non disponibile.');break}}
 if(!error){route.reverse();if(profile.rdp?.gateway)node(text('Gateway desktop remoto'),profile.rdp.gateway);for(const hop of route)node(hop.name,hop.host+':'+hop.port+' · '+String(hop.protocol||'').toUpperCase())}
 else {const warning=document.createElement('p');warning.setAttribute('role','status');warning.textContent=error;body.append(warning)}
 const heading=document.createElement('h3');heading.textContent=text('Tunnel configurati');body.append(heading);let total=0;
 for(const hop of route){const tunnels=Array.isArray(hop.tunnels)?hop.tunnels:[];for(const tunnel of tunnels){const row=document.createElement('p');row.className='connection-tunnel';const kind=text(tunnel.kind==='remote'?'Remoto':tunnel.kind==='socks'?'Proxy SOCKS':'Locale');row.textContent=hop.name+' · '+kind+' · '+tunnel.localPort+(tunnel.kind==='socks'?'':' → '+tunnel.destination+':'+tunnel.destinationPort)+' · '+text(tunnel.automatic?'Avvio automatico':'Avvio manuale');body.append(row);total++}}
 if(!total){const empty=document.createElement('p');empty.textContent=text('Nessun tunnel configurato.');body.append(empty)}
}
