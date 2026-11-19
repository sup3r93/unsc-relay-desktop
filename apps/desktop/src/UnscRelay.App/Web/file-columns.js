 'use strict';
const fileColumnDefinitions={
 storage:[['name','Nome',320,160],['size','Dimensione',110,85],['type','Tipo',150,95],['modified','Data modifica',180,140]],
 local:[['name','Nome',320,160],['size','Dimensione',110,85],['type','Tipo',150,95],['modified','Data modifica',180,140],['attributes','Attributi',110,85]],
 remote:[['name','Nome',320,160],['size','Dimensione',110,85],['type','Tipo',150,95],['modified','Data modifica',180,140],['permissions','Permessi',120,100],['owner','Proprietario',120,95],['group','Gruppo',110,85]]
};
function fileColumnWidths(side){
 const defaults=Object.fromEntries(fileColumnDefinitions[side].map(([key,,width])=>[key,width]));
 try{const saved=JSON.parse(localStorage.getItem('relay.fileColumns.v1.'+side)||'{}');for(const[key,,width,min]of fileColumnDefinitions[side])if(Number.isFinite(saved[key]))defaults[key]=Math.round(Math.max(min,Math.min(1400,saved[key])))}catch{}
 return defaults;
}
function applyFileColumns(pane,side){
 const widths=pane.nav[side].widths??=fileColumnWidths(side),list=pane.lists[side];
 const values=fileColumnDefinitions[side].map(([key])=>widths[key]);
 list.style.setProperty('--file-columns',values.map(n=>n+'px').join(' '));list.style.setProperty('--file-table-width',(values.reduce((a,b)=>a+b,0)+24)+'px');
}
function saveFileColumns(pane,side){
 try{localStorage.setItem('relay.fileColumns.v1.'+side,JSON.stringify(pane.nav[side].widths))}catch{toast(uiText('Impossibile memorizzare la larghezza delle colonne.'),true)}
 for(const other of panes.values())if(other.fileWorkspace&&other!==pane){other.nav[side].widths={...pane.nav[side].widths};applyFileColumns(other,side)}
}
function renderFileHeader(pane,side){
 applyFileColumns(pane,side);const header=document.createElement('div');header.className='workspace-file-head';
 for(const[key,label,initial,min]of fileColumnDefinitions[side]){
  const cell=document.createElement('span');cell.className='file-column-label';cell.dataset.column=key;cell.append(document.createTextNode(label));
  const grip=document.createElement('span');grip.className='file-column-resizer';grip.tabIndex=0;grip.setAttribute('role','separator');grip.setAttribute('aria-orientation','vertical');grip.setAttribute('aria-label',uiText('Larghezza ')+label+' '+(side==='local'?'locale':'remota'));grip.setAttribute('aria-valuemin',String(min));grip.setAttribute('aria-valuemax','1400');interfaceAttribute(grip, 'title', 'Trascina o usa le frecce per ridimensionare. Doppio clic per ripristinare.');
  const set=width=>{pane.nav[side].widths[key]=Math.round(Math.max(min,Math.min(1400,width)));applyFileColumns(pane,side);grip.setAttribute('aria-valuenow',String(pane.nav[side].widths[key]))};set(pane.nav[side].widths[key]);
  grip.onpointerdown=e=>{if(e.button!==0)return;e.preventDefault();e.stopPropagation();const start=e.clientX,width=pane.nav[side].widths[key];grip.setPointerCapture(e.pointerId);grip.onpointermove=move=>{if(grip.hasPointerCapture(move.pointerId))set(width+move.clientX-start)};const done=event=>{if(grip.hasPointerCapture(event.pointerId))grip.releasePointerCapture(event.pointerId);grip.onpointermove=null;saveFileColumns(pane,side)};grip.onpointerup=done;grip.onpointercancel=done};
  grip.onkeydown=e=>{if(['ArrowLeft','ArrowRight','Home'].includes(e.key)){e.preventDefault();e.stopPropagation();set(e.key==='Home'?initial:pane.nav[side].widths[key]+(e.key==='ArrowLeft'?-1:1)*(e.shiftKey?40:10));saveFileColumns(pane,side)}};
  grip.ondblclick=e=>{e.preventDefault();e.stopPropagation();set(initial);saveFileColumns(pane,side)};cell.append(grip);header.append(cell);
 }
 return header;
}
function fileGlyph(file){
 const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');svg.setAttribute('viewBox','0 0 24 24');svg.setAttribute('aria-hidden','true');svg.classList.add('file-glyph');
 const extension=file.name.split('.').at(-1).toLowerCase();let kind=file.directory?'folder':/^(png|jpg|jpeg|gif|svg|webp|ico|bmp)$/.test(extension)?'image':/^(zip|7z|rar|tar|gz|bz2)$/.test(extension)?'archive':/^(cs|js|ts|py|sh|cmd|ps1|json|xml|html|css|sql|yml|yaml)$/.test(extension)?'code':'document';svg.dataset.kind=kind;
 const shapes={folder:'M2 6a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2v10H2Z M2 10h20',document:'M6 2h8l5 5v15H6Z M14 2v6h5 M9 12h7 M9 16h7',image:'M4 3h16v18H4Z M5 17l5-6 4 4 2-2 3 4 M15 7h1',archive:'M5 2h14v20H5Z M11 3h2 M11 6h2 M11 9h2 M11 12h2 M10 16h4v3h-4Z',code:'M6 2h8l5 5v15H6Z M14 2v6h5 M10 12l-2 3 2 3 M15 12l2 3-2 3'};
 const path=document.createElementNS(svg.namespaceURI,'path');path.setAttribute('d',shapes[kind]);svg.append(path);
 if(file.link){const badge=document.createElementNS(svg.namespaceURI,'path');badge.setAttribute('d','M2 20v-5h5 M2 20l6-6');badge.classList.add('file-link-badge');svg.append(badge)}return svg;
}
function renderFileCells(row,file,side){
 for(const[key]of fileColumnDefinitions[side]){const cell=document.createElement(key==='name'?'span':'small');cell.dataset.column=key;
  if(key==='name'){cell.className='file-name-cell';cell.append(fileGlyph(file));const text=document.createElement('span');text.textContent=file.name;cell.append(text)}
  else{const value=key==='size'?(file.directory?'—':formatSize(file.size)):key==='type'?fileType(file):key==='modified'?(Number.isNaN(Date.parse(file.modified))?'—':new Date(file.modified).toLocaleString(document.documentElement.lang)):file[key]??'—';cell.textContent=String(value);cell.title=key==='owner'&&file.owner!=null?'Identificativo proprietario (UID): '+value:key==='group'&&file.group!=null?'Identificativo gruppo (GID): '+value:String(value)}row.append(cell);
 }
}
