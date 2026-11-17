'use strict';
function detachPane(pane){
 if(!pane||pane.connecting){toast('Attendi il completamento della connessione.',true);return Promise.resolve(false)}
 if(pane.detachedWindow)return call('detachedFocus',{id:pane.id}).then(()=>true);
 if(pane.detachPromise)return pane.detachPromise;
 pane.detachPromise=new Promise(resolve=>pane.detachResolve=resolve);
 pane.detachRequested=true;const floating=window.open('detached.html#'+pane.id,'relay-'+pane.id,'popup,width=1250,height=820');
 if(!floating){pane.detachRequested=false;pane.detachResolve(false);pane.detachPromise=null;toast('Impossibile aprire la finestra separata.',true);return Promise.resolve(false)}
 pane.detachTimer=setTimeout(()=>{if(!pane.detachedWindow){pane.detachRequested=false;pane.detachResolve(false);pane.detachPromise=null;toast('La finestra non è pronta. La scheda resta nella console.',true)}},10000);return pane.detachPromise;
}
function attachDetachedPane(id,child){
 const pane=panes.get(id);if(!pane||!pane.detachRequested||child.location.origin!==location.origin)return false;
 clearTimeout(pane.detachTimer);pane.detachRequested=false;pane.detachedWindow=child;pane.detachResolve?.(true);pane.detachPromise=null;desktopLayoutFrame=null;child.document.title='UNSC Relay · '+pane.title.textContent;child.document.getElementById('detached-title').textContent=pane.title.textContent;
 child.document.getElementById('detached-stage').append(pane.container);pane.container.style.display='flex';pane.container.style.gridColumn='';pane.container.style.gridRow='';pane.container.classList.add('detached-pane');pane.tab.classList.add('tab-detached');
 if(!pane.fileWorkspace&&!pane.storageWorkspace&&!pane.editor&&!pane.desktop){pane.term.open(pane.container.querySelector('.terminal-surface'));pane.fit.fit();pane.term.refresh(0,pane.term.rows-1)}
 const resize=()=>{try{pane.fit.fit();scheduleDesktopLayout()}catch{}};child.addEventListener('resize',resize);child.document.getElementById('return-main').onclick=()=>call('detachedClose',{id});bindContextDocument(child.document);pane.detachedObserver=new ResizeObserver(resize);pane.detachedObserver.observe(pane.container);
 if(activeId===id)activeId=[...panes.keys()].find(key=>!panes.get(key).detachedWindow)??null;refreshStage();resize();return true;
}
function reattachDetachedPane(id){
 const pane=panes.get(id);if(!pane)return true;if(!pane.detachedWindow)return !pane.detachRequested;
 pane.detachedObserver?.disconnect();pane.detachedObserver=null;pane.detachedWindow=null;pane.tab.classList.remove('tab-detached');pane.container.classList.remove('detached-pane');$('terminals').append(pane.container);activeId=id;
 if(!pane.fileWorkspace&&!pane.storageWorkspace&&!pane.editor&&!pane.desktop){pane.term.open(pane.container.querySelector('.terminal-surface'));pane.term.refresh(0,pane.term.rows-1)}refreshStage();try{pane.fit.fit()}catch{}lastDesktopLayout='';scheduleDesktopLayout();return true;
}
function finishDetachedPane(pane){if(!pane.detachedWindow)return;reattachDetachedPane(pane.id);call('detachedClose',{id:pane.id}).catch(()=>{});}
