 'use strict';
let workspaceRestoring=false;
async function restoreWorkspace(w){
 if(workspaceRestoring)return toast('Un workspace è già in apertura.',true);workspaceRestoring=true;const restored=[],floating=[];let skipped=0;
 try{
  for(let i=0;i<w.profileIds.length;i++){
   const profile=state.data.profiles.find(p=>p.id===w.profileIds[i]);if(!profile){restored[i]=null;skipped++;continue}
   try{const pane=w.paneKinds?.[i]==='sftp'?await openFileWorkspace(profile):await connect(profile);restored[i]=pane;if(!pane){skipped++;continue}if(w.tabNames?.[i]){pane.title.textContent=w.tabNames[i];pane.name.textContent=w.tabNames[i]+' / '+profileEndpoint(profile)}if(w.windowPlacements?.[i])floating.push({pane,placement:w.windowPlacements[i]})}catch{restored[i]=null;skipped++}
  }
  for(const {pane,placement} of floating.sort((a,b)=>b.placement.order-a.placement.order))try{const result=await call('workspaceWindowPrepare',{id:pane.id,placement});if(!result.applied&&!await detachPane(pane))skipped++}catch{skipped++;toast('Una finestra resta disponibile nella console.',true)}
  layout=['tabs','columns','rows','grid'].includes(w.layout)?w.layout:'tabs';splitRatios=w.splitRatios?.slice()||[.5,.5];splitZoom=false;splitPaneIds=w.splitPaneIndices?.map(i=>restored[i]?.id||null)||restored.filter(p=>p&&!p.detachedWindow).slice(0,splitSlotCount()).map(p=>p.id);const active=restored[w.activePaneIndex];activeId=active&&!active.detachedWindow?active.id:splitPaneIds.find(Boolean)||restored.find(p=>p&&!p.detachedWindow)?.id||null;lastSplitActive=activeId;refreshStage();if(active?.detachedWindow)await call('detachedFocus',{id:active.id});else{await call('activateMain');panes.get(activeId)?.term.focus?.()}
  toast(skipped?'Workspace aperto: '+skipped+' elementi non ripristinati; le schede disponibili restano accessibili.':'Workspace ripristinato: schede, riquadri e finestre separate.',!!skipped);
 }finally{workspaceRestoring=false}
}
