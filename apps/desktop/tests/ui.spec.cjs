const { test, expect } = require('@playwright/test');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../src/UnscRelay.App/Web');
test.beforeEach(async ({ page, context }) => {
  await context.route('https://relay.local/**', route => {
    const url = new URL(route.request().url());
    const target = path.join(root, url.pathname === '/' ? 'index.html' : decodeURIComponent(url.pathname));
    // Synthetic, silent fixtures keep UI tests independent of personal media assets.
    if (/\.(mp4|mp3|m4a)$/.test(url.pathname) && !fs.existsSync(target)) {
      const video=url.pathname.endsWith('.mp4');
      return route.fulfill({contentType:video?'video/mp4':'audio/wav',body:fs.readFileSync(path.join(__dirname,'fixtures',video?'silent-video.mp4':'silent-audio.wav'))});
    }
    if (!target.startsWith(root + path.sep) || !fs.existsSync(target)) return route.fulfill({ status: 404, body: '' });
    const type = { '.html': 'text/html', '.css': 'text/css', '.js': 'text/javascript', '.mp4': 'video/mp4', '.svg': 'image/svg+xml' }[path.extname(target)] || 'application/octet-stream';
    return route.fulfill({ contentType: type, body: fs.readFileSync(target) });
  });
  await page.addInitScript(() => {
    window.fixtureOriginalPlay=HTMLMediaElement.prototype.play;HTMLMediaElement.prototype.play=function(){if(!window.holdFixtureMedia)queueMicrotask(()=>this.dispatchEvent(new Event('ended')));return Promise.resolve();};
    const state = { version: '0.1.0', data: { folders: ['Connessioni'], profiles: [], snippets: [], workspaces: [], settings: { startupVideo: false, shutdownVideo: false, startupVolume: .8, shutdownVolume: 1, autoLockMinutes: 10 } }, vault: { exists: false, unlocked: false, keys: [] }, hostKeys: [] };
    window.fixtureState=state;const listeners = []; window.testCalls = []; window.fixtureEmit=(type,payload)=>listeners.forEach(callback=>callback({data:{type,payload}}));
    window.chrome = window.chrome || {};
    window.chrome.webview = {
      addEventListener: (_, callback) => listeners.push(callback),
      postMessageWithAdditionalObjects: (message,files)=>{window.fixtureDroppedFileCount=files.length;queueMicrotask(()=>listeners.forEach(callback=>callback({data:{type:'reply',payload:{id:message.id,result:{paths:['C:\\Fixture\\local.txt']}}}})))},
      postMessage: ({ id, action, payload }) => {
        window.testCalls.push({ action, payload }); let result = state, error = null;
        if(action==='saveProfile'){const {password,keyPassphrase,rememberPassword,...safe}=payload;if(payload.addToAddressBook){state.data.machines=state.data.machines||[];state.data.machines.push({id:'machine-fixture',name:payload.machineName,host:payload.host,port:payload.port})}if(rememberPassword&&password)safe.passwordId='fixture-password-id';const i=state.data.profiles.findIndex(p=>p.id===safe.id);if(i<0)state.data.profiles.push(safe);else state.data.profiles[i]=safe}
        if(action==='certificateRevoke'&&!window.fixtureCancelRevocation){const key=state.vault.keys.find(k=>k.id===payload.keyId);state.vault.revocations=state.vault.revocations||[];state.vault.revocations.push({id:'revocation-fixture',name:key.name,reason:payload.reason,revokedAt:'2026-11-14T20:00:00Z',certificate:structuredClone(key.certificate)});}
        if(action==='certificateRemove')state.vault.keys.find(k=>k.id===payload.keyId).certificate=null;
        if(action==='certificateKrlExport'){result=window.fixtureCancelExport?{exported:false}:{exported:true,count:state.vault.revocations.length,fileName:'fixture-revoked.krl'};if(window.fixtureKrlFailure){result=null;error='OpenSSH non ha generato la lista KRL.';}}
        if(action==='certificateKrlQuery'){result={cancelled:false,revoked:!!window.fixtureKrlRevoked,fileName:'external-fixture.krl'};if(window.fixtureKrlFailure){result=null;error='OpenSSH non ha potuto verificare la KRL.';}}
        if(action==='audioSettings'){state.data.settings.appMusicEnabled=payload.enabled;state.data.settings.appMusicVolume=payload.volume;if('startupVolume'in payload)state.data.settings.startupVolume=payload.startupVolume;if('shutdownVolume'in payload)state.data.settings.shutdownVolume=payload.shutdownVolume;result=state;}
        if(action==='editorDetectProgram'){state.data.settings.externalEditor='C:\\Fixture\\Notepad++\\notepad++.exe';state.data.settings.editorPreference='auto';result=state;}
        if(action==='editorChooseProgram'){state.data.settings.externalEditor='C:\\Fixture\\editor.exe';result=state;}if(action==='editorResetProgram'){state.data.settings.externalEditor='';result=state;}
        if(action==='settings'){state.data.settings.autoLockMinutes=payload.autoLockMinutes;result=state;}
        if(action==='musicSet'){state.data.settings.appMusicEnabled=payload.enabled;result=state;}
        if(action==='accessSettings'){state.data.settings={...state.data.settings,...payload};result=state;}
        if(action==='vaultRevealPrivate'){result={text:'-----BEGIN PRIVATE KEY-----\nFIXTURE-ONLY\n-----END PRIVATE KEY-----'};}
        if(action==='accountAvailability')result={registrationAllowed:false};
        if(action==='accountProfile')result={username:'fixture',firstName:'Fixture',lastName:'User',email:'fixture@example.com',emailVerified:true};
        if(action==='accountSecurity')result=state;
        if(action==='accountLogout'){state.account={requiresLink:true,linked:false,ready:false};state.vault.unlocked=false;result=state;}
        if(action==='accountLogin'){state.account={requiresLink:false,linked:true,name:'Fixture User',ready:true,revision:1,conflict:false,message:'Dispositivo autorizzato'};result=state;}
        if(action==='accountSync'){state.account={...state.account,message:'Sincronizzato',conflict:false};result=state;}
        if(action==='applicationAuthenticate'){state.appLocked=false;state.vault.unlocked=true;result=state;}
        if(action==='nativeTools')result={tools:[{kind:'ssh',available:true,executable:'C:\\Fixture\\Tools\\ssh.exe'}],keys:['ssh-ed25519','ssh-mldsa44-ed25519']};
        if(action==='externalCertificateStatus')result=state.data.certificateProviders||[];
        if(action==='externalCertificateSave'){state.data.certificateProviders=payload.remove?[]:[payload.provider];result=state;}
        if(action==='nativeTask')result={id:'native-fixture-task',name:'Key generation',protocol:'native',native:{kind:'ssh',target:'tool'},authentication:'auto'};
        if(action==='localAvailability')result={shells:[{id:'cmd',name:'CMD',available:true},{id:'powershell',name:'Windows PowerShell',available:true},{id:'pwsh',name:'PowerShell 7',available:true},{id:'wsl',name:'WSL',available:true}],distributions:['Ubuntu','Debian']};
        if(action==='localShellFolder')result='C:\\Fixture\\Local';
        if(action==='connect'){const protocol=state.data.profiles.find(p=>p.id===payload.profileId)?.protocol;result={sessionId:payload.sessionId,filesAvailable:['ssh','sftp'].includes(protocol),desktop:['rdp','vnc'].includes(protocol),local:protocol==='local',running:true}}
        if(action==='recoverProfile')result={success:true,retryable:false,error:'',sessions:[...panes.values()].filter(p=>p.profile.id===payload.profileId&&!p.editor).map(p=>({sessionId:p.id,connected:true,filesAvailable:true,terminalRestarted:!p.fileWorkspace&&!p.connected}))};
        if(action==='sftpDirectory')result=payload.path==='.'?'/home/test':payload.path;
        if(action==='sftpHome')result='/home/test';
        if(action==='fileCompare')result={local:{path:payload.local,text:'one\nlocal new\n',hash:'new-hash'},remote:{path:payload.remote,text:'one\nremote old\n',hash:'old-hash'}};
        if(action==='recordingRetentionStatus')result={days:state.data.settings.recordingRetentionDays||0,result:null};
        if(action==='recordingRetentionSet'){state.data.settings.recordingRetentionDays=payload.days;result=state;}
        if(action==='conversionStatus')result=window.fixtureConversion||{active:false,toolVersion:'0.85'};
        if(action==='conversionStart'){window.fixtureConversion={active:true,id:'convert-fixture',toolVersion:'0.85',target:payload.target,source:{fileName:'fixture.ppk',format:'ppk3',algorithm:'ssh-ed25519',bits:255,fingerprint:'SHA256:fixture'}};result=window.fixtureConversion;}
        if(action==='conversionCancel'){window.fixtureConversion=null;result={active:false,toolVersion:'0.85'};}
        if(action==='conversionOpen')result=window.fixtureConversion;
        if(action==='conversionFinish'){if(window.fixtureConversionFailure){error='Il risultato appartiene a una chiave diversa.';result=null;}else{if(payload.import)state.vault.keys.push({id:'converted-key',name:payload.name,comment:payload.comment,algorithm:'ssh-ed25519',fingerprint:'SHA256:fixture',publicKey:'ssh-ed25519 fixture'});window.fixtureConversion=null;result={verified:true,fileName:'converted.key',format:'openssh',fingerprint:'SHA256:fixture',importedId:payload.import?'converted-key':null,snapshot:payload.import?state:null};}}
        if(action==='fileQueue')result=window.fixtureQueue||[];
        if(action==='filePlan')result={items:payload.paths.map(path=>({local:payload.direction==='upload'?path:'C:\\Fixture\\remote.txt',remote:payload.direction==='download'?path:'/home/test/local.txt',source:{size:100,modified:'2026-11-14T00:00:00Z'},target:window.fixtureConflict?{size:50,modified:'2026-11-13T00:00:00Z'}:null})),skippedLinks:0};
        if(action==='fileEnqueue'){window.fixtureQueue=payload.items.map((item,index)=>({id:'job-'+index,sessionId:payload.sessionId,name:'local.txt',local:item.local,remote:item.remote,direction:payload.direction,status:'In coda',progress:0,error:''}));result=window.fixtureQueue}
        if(action==='filePause'){window.fixtureQueue.find(j=>j.id===payload.id).status='In pausa';result=window.fixtureQueue}
        if(action==='fileResume'){window.fixtureQueue.find(j=>j.id===payload.id).status='In coda';result=window.fixtureQueue}

        if(action==='localList')result={path:'C:\\Fixture',parent:'C:\\',entries:[{name:'local.txt',path:'C:\\Fixture\\local.txt',directory:false,size:24,modified:'2026-01-01'}]};
        if(action==='confirmAction')result=true;
        if(action==='clipboardRead')result=window.fixtureClipboard||'';
        if(action==='clipboardWrite'){window.fixtureClipboard=payload.text;result=null}
        if(action==='editorOpen')result={id:'fixture-edit',sessionId:payload.sessionId,path:payload.path,text:'hello\nremote file\n',hash:'fixture-hash'};
        if(action==='editorSave')result=null;
        if (action === 'sftpList') result = window.fixtureRemoteFiles || [{ name: '<img src=x onerror=alert(1)>', path: '/safe', directory: false, size: 24, modified: '2026-01-01' }];
        if (action === 'checkUpdates') result = { version: 'v0.2.0', notes: '<script>test</script>', fileName: 'test.exe' };
        if (action === 'agentList') result = [];
        if(action==='updateTokenStatus')result={saved:!!window.fixtureTokenSaved};
        if(action==='updateTokenSave'){window.fixtureTokenSaved=true;result={saved:true}}
        if(action==='updateTokenDelete'){window.fixtureTokenSaved=false;result={saved:false}}
        if(action==='tunnelStatus')result=window.fixtureTunnels||[];
        if(action==='saveTunnel'){window.fixtureTunnels=(window.fixtureTunnels||[]).filter(x=>x.rule.id!==payload.rule.id).concat([{rule:payload.rule,active:false}]);result=window.fixtureTunnels}
        if(action==='toggleTunnel'){window.fixtureTunnels.find(x=>x.rule.id===payload.ruleId).active=payload.enabled;result=window.fixtureTunnels}
        if(action==='deleteTunnel'){window.fixtureTunnels=window.fixtureTunnels.filter(x=>x.rule.id!==payload.ruleId);result=window.fixtureTunnels}
        if(action==='workspaceWindowCapture')result=payload.sessionIds.map(id=>window.fixtureWindowPlacements?.[id]||null);
        if(action==='workspaceWindowPrepare')result={applied:false,relocated:false};
        if(action==='saveSnippet'){state.data.snippets=state.data.snippets.filter(s=>s.id!==payload.id);state.data.snippets.push(payload);} if(action==='saveWorkspace'){state.data.workspaces=state.data.workspaces.filter(w=>w.name!==payload.name);state.data.workspaces.push(payload)}
        if(action==='deleteSnippet')state.data.snippets=state.data.snippets.filter(s=>s.id!==payload.snippetId);
        if (action === 'folderCreate') state.data.folders.push(payload.path);
        if (action === 'vaultUnlock') { state.vault.exists = true; state.vault.unlocked = true; }
        if (action === 'vaultLock') state.vault.unlocked = false;
        if (action === 'helloEnable') state.vault.windowsHello = true;
        if (action === 'helloDisable') state.vault.windowsHello = false;
        if (action === 'helloUnlock') {if(window.fixtureHelloCancel){listeners.forEach(callback=>callback({data:{type:'helloPending',payload:{}}}));listeners.forEach(callback=>callback({data:{type:'helloFinished',payload:{}}}));error='Operazione annullata.'}else state.vault.unlocked=true;}
        if(action==='vaultGenerate'&&window.fixtureGenerationFailure){result=null;error='Generazione non riuscita.';}
        if (action === 'vaultGenerate'&&!window.fixtureGenerationFailure) state.vault.keys.push({ id: crypto.randomUUID(), name: payload.name, fingerprint: 'SHA256:fixture', publicKey: 'ssh-ed25519 AAAAFixture', algorithm:'ssh-ed25519',bits:256,created:'2026-11-14T00:00:00Z',comment:'',certificate: null });
        if (action === 'vaultSelectImport') result={selectionId:'fixture-selection',fileName:'id_ed25519',name:'id_ed25519'};
        if (action === 'vaultImport') state.vault.keys.push({id:crypto.randomUUID(),name:payload.name,comment:payload.comment,algorithm:'ssh-ed25519',bits:256,created:'2026-11-14T00:00:00Z',fingerprint:'SHA256:import-fixture',publicKey:'ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIFixtureOnly',certificate:null});
        if (action === 'vaultRename') Object.assign(state.vault.keys.find(k=>k.id===payload.keyId),{name:payload.name,comment:payload.comment});
        if (action === 'vaultDelete') {if(window.testCancelDeletion)error='Operazione annullata.';else state.vault.keys=state.vault.keys.filter(k=>k.id!==payload.keyId)}
        queueMicrotask(() => listeners.forEach(callback => callback({ data: { type: 'reply', payload: { id, result: structuredClone(result),error } } })));
      }
    };
  });
  await page.goto('https://relay.local/index.html');
});

test('key archive lists, names, renames and deletes keys with return navigation',async({page})=>{
 await page.locator('#keys-btn').click();await page.getByRole('button',{name:'Crea archivio',exact:true}).click();
 for(const name of ['Contabo production','University lab']){
  await page.getByRole('button',{name:'Importa chiave',exact:true}).click();
  await page.getByRole('button',{name:'Seleziona file della chiave',exact:true}).click();
  await page.locator('[name=name]').fill(name);await page.locator('[name=comment]').fill('Imported fixture');
  await page.getByRole('button',{name:'Importa nell’archivio',exact:true}).click();
 }
 const rows=page.locator('.key-table tbody tr');await expect(rows).toHaveCount(2);
 await expect(page.getByRole('button',{name:'Elimina chiave',exact:true})).toBeInViewport();
 await rows.filter({hasText:'Contabo production'}).click();
 await expect(page.locator('.key-detail h3')).toHaveText('Contabo production');
 await expect(page.getByRole('textbox',{name:'Chiave pubblica selezionata',exact:true})).toHaveValue(/••/);await page.getByRole('button',{name:'Mostra pubblica',exact:true}).click();
 await expect(page.getByRole('textbox',{name:'Chiave pubblica selezionata',exact:true})).toHaveValue(/ssh-ed25519/);
 await page.getByRole('button',{name:'Modifica nome e commento',exact:true}).click();
 await page.locator('[name=name]').fill('Renamed <img src=x onerror=alert(1)>');await page.locator('[name=comment]').fill('Main SSH key');
 await page.getByRole('button',{name:'Salva modifiche',exact:true}).click();
 await expect(page.locator('.key-detail h3')).toHaveText('Renamed <img src=x onerror=alert(1)>');
 await expect(page.locator('#modal-body img')).toHaveCount(0);
 await page.getByRole('button',{name:'Accesso / Windows Hello',exact:true}).click();await expect(page.getByRole('tab',{name:'Accesso',exact:true})).toHaveAttribute('aria-selected','true');await page.locator('#modal-close').click();await page.locator('#keys-btn').click();
 await expect(rows).toHaveCount(2);await expect(page.locator('.key-detail h3')).toHaveText('Renamed <img src=x onerror=alert(1)>');
 await page.getByRole('button',{name:'Token / agent',exact:true}).click();await page.locator('#modal-back').click();await expect(rows).toHaveCount(2);
 await page.evaluate(()=>window.testCancelDeletion=true);await page.getByRole('button',{name:'Elimina chiave',exact:true}).click();await expect(rows).toHaveCount(2);
 await page.evaluate(()=>window.testCancelDeletion=false);await page.getByRole('button',{name:'Elimina chiave',exact:true}).click();await expect(rows).toHaveCount(1);
 await expect(page.locator('.key-detail h3')).toHaveText('University lab');
 await page.getByRole('button',{name:'Blocca archivio',exact:true}).click();await expect(rows).toHaveCount(0);await expect(page.locator('#modal-body')).toContainText('ARCHIVIO BLOCCATO');
 await page.getByRole('button',{name:'Sblocca archivio',exact:true}).click();await expect(rows).toHaveCount(1);
 await page.screenshot({path:'artifacts/ui-key-manager.png',fullPage:true});
});

test('key actions remain visible in a compact window',async({page})=>{
 await page.setViewportSize({width:1000,height:680});await page.locator('#keys-btn').click();
 await page.getByRole('button',{name:'Crea archivio',exact:true}).click();await page.getByRole('button',{name:'Genera chiave',exact:true}).click();
 await page.locator('[name=name]').fill('Compact fixture');await page.getByRole('button',{name:'Genera nel deposito',exact:true}).click();
 await expect(page.getByRole('button',{name:'Elimina chiave',exact:true})).toBeInViewport();
 await expect(page.getByRole('button',{name:'Modifica nome e commento',exact:true})).toBeInViewport();
 await page.screenshot({path:'artifacts/ui-key-manager-compact.png',fullPage:true});
});
test('empty nested folders can be created independently of connections', async ({ page }) => {
  await page.getByRole('button', { name: 'Nuova cartella', exact: true }).click();
  await page.locator('[name=path]').fill('Università/Laboratorio');
  await page.getByRole('button', { name: 'Crea cartella', exact: true }).click();
  await expect(page.locator('.folder-branch>summary')).toContainText(['Connessioni', 'Università', 'Laboratorio']);
  await expect(page.locator('.profile-item')).toHaveCount(0);
  await page.locator('.folder-branch>summary').filter({ hasText: 'Università' }).click();
  await expect(page.locator('.folder-branch>summary').filter({ hasText: 'Laboratorio' })).not.toBeVisible();
});
test('key algorithms, certificates and hardware capabilities are reachable', async ({ page }) => {
  await page.locator('#keys-btn').click();
  await page.getByRole('button', { name: 'Algoritmi e disponibilità', exact: true }).click();
  await expect(page.locator('#modal-body')).toContainText('ssh-mldsa44-ed25519');
  await expect(page.locator('#modal-body')).toContainText('Disponibile nel client OpenSSH incluso');
  await page.locator('#modal-close').click();
  await page.locator('#keys-btn').click();
  await page.getByRole('button', { name: 'Token / agent', exact: true }).click();
  await page.getByRole('button', { name: 'Leggi identità disponibili', exact: true }).click();
  await expect(page.locator('#modal-body')).toContainText('Nessuna identità disponibile');
  await page.locator('#modal-close').click();
  await page.locator('#keys-btn').click();
  await page.getByRole('button', { name: 'Crea archivio', exact: true }).click();
  await page.getByRole('button', { name: 'Genera chiave', exact: true }).click();
  await expect(page.getByRole('radio',{name:'RSA 4096 bit',exact:true})).toHaveCount(1);
  await expect(page.getByRole('radio',{name:'ECDSA NIST P-521',exact:true})).toHaveCount(1);
  await page.locator('[name=name]').fill('Test certificate key');
  await page.getByRole('radio',{name:'ECDSA NIST P-384',exact:true}).check();
  await page.getByRole('button', { name: 'Genera nel deposito', exact: true }).click();
  await page.getByRole('button', { name: 'Certificato', exact: true }).click();
  await expect(page.locator('#modal-body')).toContainText('occorre una seconda chiave');
});
test('profile, splits, SFTP escaping and GitHub update workflow', async ({ page }) => {
  const errors = []; page.on('pageerror', e => errors.push(e.message));
  await openNewConnection(page);
  await page.locator('[name=name]').fill('Relay fixture');
  await page.locator('[name=host]').fill('127.0.0.1');
  await page.locator('[name=username]').fill('fixture');
  await page.getByRole('button', { name: 'Salva connessione', exact: true }).click();
  await expect(page.locator('.profile-item')).toHaveCount(1);
  await page.locator('.profile-item').dblclick();
  await expect(page.locator('.pane-status')).toHaveText('Connesso');
  await page.locator('.profile-item').dblclick();
  await page.locator('#split-h').click();
  await expect(page.locator('.terminal-pane:visible')).toHaveCount(2);
  await page.locator('#files-btn').click();
  await expect(page.locator('.file-workspace')).toContainText('<img src=x onerror=alert(1)>');
  await expect(page.locator('.file-workspace img')).toHaveCount(0);
  await page.locator('#updates-btn').click();
  await page.getByRole('button',{name:'Salva token GitHub',exact:true}).click();
  await page.getByRole('button', { name: 'Controlla aggiornamenti', exact: true }).click();
  await expect(page.locator('#modal-body')).toContainText('v0.2.0');
  await expect(page.locator('[name=token]')).toHaveCount(0); await expect(page.locator('#update-token-state')).toContainText('Token salvato');
  await expect(page.locator('#modal-body script')).toHaveCount(0);
  expect(errors).toEqual([]);
});
test('key manager and settings expose actual development state', async ({ page }) => {
  await page.locator('#keys-btn').click();
  await expect(page.locator('#modal-body')).toContainText(/password principale/i);
  await page.locator('#modal-close').click();
  await page.locator('#settings-btn').click();
  await page.getByRole('tab',{name:'Funzionalità',exact:true}).click();
  await expect(page.locator('#modal-body')).toContainText('IN SVILUPPO');
  await expect(page.locator('#modal-body')).toContainText('RDP/VNC');
  await page.locator('#modal-close').click();
  await page.screenshot({ path: 'artifacts/ui-home.png', fullPage: true });
});

test('archive access has actionable creation, native Hello and password choices', async ({page}) => {
  await page.locator('#keys-btn').click();
  await page.getByRole('button', {name:'Accesso / Windows Hello', exact:true}).click();
  await expect(page.locator('#modal-body')).toContainText('Account Windows condiviso?');
  await expect(page.locator('#modal-body')).not.toContainText('Bitwarden');
  await page.getByRole('button',{name:'Accedi con il tuo account',exact:true}).click();
  await page.getByRole('button',{name:'Abilita Windows Hello su questo PC',exact:true}).click();
  await expect(page.locator('#modal-body')).toContainText('Windows Hello abilitato');
  await page.getByRole('button',{name:'Blocca archivio',exact:true}).click();
  await expect(page.getByRole('button',{name:'Accedi con il tuo account',exact:true})).toBeVisible();
  await page.getByRole('button',{name:'Sblocca con Windows Hello',exact:true}).click();
  expect(await page.evaluate(()=>window.testCalls.filter(c=>c.action.startsWith('passkey')))).toEqual([]);
  expect(await page.evaluate(()=>window.testCalls.some(c=>c.action==='helloUnlock'))).toBe(true);
});

test('saved update credential is managed without exposing a token to the web page',async({page})=>{
 await page.locator('#updates-btn').click();await expect(page.locator('#update-token-state')).toContainText('Nessun token');
 await page.getByRole('button',{name:'Salva token GitHub',exact:true}).click();await expect(page.locator('#update-token-state')).toContainText('Token salvato');
 await page.locator('#modal-close').click();await page.locator('#updates-btn').click();await expect(page.locator('#update-token-state')).toContainText('Token salvato');
 await expect(page.locator('input[type=password]')).toHaveCount(0);await page.getByRole('button',{name:'Controlla aggiornamenti',exact:true}).click();
 await expect(page.getByRole('button',{name:'Scarica e installa',exact:true})).toBeVisible();
 expect(await page.evaluate(()=>window.testCalls.filter(c=>['checkUpdates','updateTokenSave'].includes(c.action)).every(c=>Object.keys(c.payload).length===0))).toBe(true);
 await page.getByRole('button',{name:'Rimuovi token',exact:true}).click();await expect(page.locator('#update-token-state')).toContainText('Nessun token');
 await page.screenshot({path:'artifacts/ui-updates.png',fullPage:true});
});

test('tunnels can be created edited toggled and deleted without reconnecting',async({page})=>{
 await openNewConnection(page);await page.locator('[name=name]').fill('Tunnel fixture');await page.locator('[name=host]').fill('localhost');await page.locator('[name=username]').fill('fixture');await page.getByRole('button',{name:'Salva connessione',exact:true}).click();
 await page.locator('.profile-item').dblclick();await expect(page.locator('.pane-status')).toHaveText('Connesso');await page.locator('#tunnels-btn').click();
 await page.locator('[name=localPort]').fill('48222');await page.locator('[name=destinationPort]').fill('22');await page.locator('[name=automatic]').check();await page.getByRole('button',{name:'Aggiungi regola',exact:true}).click();
 await expect(page.locator('.tunnel-card')).toContainText('127.0.0.1:48222');await expect(page.locator('.tunnel-card')).toContainText('AUTOMATICO');
 await page.getByRole('button',{name:'Modifica',exact:true}).click();await page.locator('[name=localPort]').fill('48223');await page.getByRole('button',{name:'Salva modifiche',exact:true}).click();await expect(page.locator('.tunnel-card')).toContainText('127.0.0.1:48223');
 await page.getByRole('button',{name:'Attiva',exact:true}).click();await page.getByRole('button',{name:'Modifica',exact:true}).click();await expect(page.locator('#modal-body')).toContainText('Disattiva il tunnel prima');
 await page.getByRole('button',{name:'Disattiva',exact:true}).click();await page.screenshot({path:'artifacts/ui-tunnels.png',fullPage:true});await page.getByRole('button',{name:'Elimina',exact:true}).click();await expect(page.locator('.tunnel-card')).toHaveCount(0);
 expect(await page.evaluate(()=>window.testCalls.filter(c=>c.action==='connect').length)).toBe(1);
});

test('snippet library supports editing deletion and exchange actions',async({page})=>{
 await page.locator('#snippets-btn').click();await expect(page.getByRole('button',{name:'Importa snippet',exact:true})).toBeVisible();await expect(page.getByRole('button',{name:'Esporta snippet',exact:true})).toBeVisible();
 await page.getByRole('button',{name:'Nuovo snippet',exact:true}).click();await page.locator('[name=name]').fill('Fixture snippet');await page.locator('[name=text]').fill('echo {{test}}');await page.getByRole('button',{name:'Salva',exact:true}).click();
 await expect(page.locator('#modal-body')).toContainText('Fixture snippet');await page.getByRole('button',{name:'Elimina',exact:true}).click();await expect(page.locator('#modal-body')).not.toContainText('Fixture snippet');
 expect(await page.evaluate(()=>window.testCalls.some(c=>c.action==='input'))).toBe(false);
});

test('jump profile and tmux options are selected and saved explicitly',async({page})=>{
 for(const name of ['Bastion fixture','Target fixture']){
  await openNewConnection(page);await page.locator('[name=name]').fill(name);await page.locator('[name=host]').fill('localhost');await page.locator('[name=username]').fill('fixture');
  if(name.startsWith('Target')){await page.getByRole('tab',{name:'Rete e SSH',exact:true}).click();const id=await page.evaluate(()=>window.testCalls.find(c=>c.action==='saveProfile').payload.id);await page.locator('[name=jumpHost]').selectOption(id);await page.getByRole('tab',{name:'Terminale',exact:true}).click();await page.locator('[name=tmuxSession]').fill('relay_session')}
  await page.getByRole('button',{name:'Salva connessione',exact:true}).click();
 }
 const profiles=await page.evaluate(()=>window.testCalls.filter(c=>c.action==='saveProfile').map(c=>c.payload));expect(profiles[1].jumpHost).toBe(profiles[0].id);expect(profiles[1].tmuxSession).toBe('relay_session');
});

test('tabs retain names and order in workspaces and reconnect after disconnection',async({page})=>{
 await openNewConnection(page);await page.locator('[name=name]').fill('Session fixture');await page.locator('[name=host]').fill('localhost');await page.locator('[name=username]').fill('fixture');await page.getByRole('button',{name:'Salva connessione',exact:true}).click();
 await page.locator('.profile-item').dblclick();await page.locator('.profile-item').dblclick();
 const ids=await page.locator('#tabs .tab').evaluateAll(tabs=>tabs.map(t=>t.dataset.sessionId));
 for(let index=0;index<2;index++){await page.locator('#tabs .tab').nth(index).dblclick();await page.locator('[name=tabName]').fill(index?'Database':'Application');await page.getByRole('button',{name:'Rinomina',exact:true}).click()}
 await page.locator('#tabs .tab').nth(1).dragTo(page.locator('#tabs .tab').nth(0));await expect(page.locator('#tabs .tab>span:first-child')).toHaveText(['Database','Application']);
 await page.locator('#save-workspace').click();await page.locator('[name=name]').fill('Fixture layout');await page.getByRole('button',{name:'Salva disposizione',exact:true}).click();
 const workspace=await page.evaluate(()=>window.testCalls.find(c=>c.action==='saveWorkspace').payload);expect(workspace.tabNames).toEqual(['Database','Application']);
 await page.evaluate(id=>window.fixtureEmit('status',{sessionId:id,text:'Sessione terminata',connected:false}),ids[1]);
 const reconnect=page.locator('.terminal-pane').filter({has:page.locator('.pane-status', {hasText:'Sessione terminata'})}).getByRole('button',{name:'Riconnetti',exact:true});
 await reconnect.click();await expect(reconnect).toBeHidden();expect(await page.evaluate(()=>window.testCalls.filter(c=>c.action==='recoverProfile').length)).toBe(1);
 await expect(page.locator('#tabs .tab')).toHaveCount(2);await expect(page.locator('#tabs .tab>span:first-child')).toHaveText(['Database','Application']);
});

test('connection editor has hierarchical folders and distinct password and key tabs',async({page})=>{
 await page.getByRole('button',{name:'Nuova cartella',exact:true}).click();await page.locator('[name=path]').fill('Contabo/Produzione');await page.getByRole('button',{name:'Crea cartella',exact:true}).click();
 await openNewConnection(page);await expect(page.getByRole('tab',{name:'Connessione',exact:true})).toHaveAttribute('aria-selected','true');
 await expect(page.locator('select[name=folder] option[value="Contabo/Produzione"]')).toHaveText(/└ Produzione/);await page.locator('[name=folder]').selectOption('Contabo/Produzione');
 await page.locator('[name=name]').fill('Password fixture');await page.locator('[name=host]').fill('localhost');await page.locator('[name=username]').fill('fixture');
 await page.getByRole('tab',{name:'Autenticazione',exact:true}).click();await expect(page.locator('[name=authentication]')).toHaveValue('password');await page.getByRole('button',{name:'Crea archivio',exact:true}).click();await page.locator('[name=password]').fill('Not a real password');await page.locator('[name=rememberPassword]').check();
 await page.screenshot({path:'artifacts/ui-connection-auth.png',fullPage:true});await page.getByRole('button',{name:'Salva connessione',exact:true}).click();
 const profile=await page.evaluate(()=>window.testCalls.filter(c=>c.action==='saveProfile').at(-1).payload);expect(profile.authentication).toBe('password');expect(profile.rememberPassword).toBe(true);expect(profile.folder).toBe('Contabo/Produzione');
 await page.locator('.profile-item .edit-profile').click();await page.getByRole('tab',{name:'Autenticazione',exact:true}).click();await expect(page.locator('[name=password]')).toHaveValue('');
 await page.locator('[name=authentication]').selectOption('key');await expect(page.locator('[name=keyId]')).toBeVisible();await expect(page.locator('[name=password]')).not.toBeVisible();await expect(page.locator('[name=keyPassphrase]')).toBeVisible();
});

test('RDP and VNC have embedded panes which hide when a dialog opens',async({page})=>{
 for(const protocol of ['rdp','vnc']){
  await openNewConnection(page,protocol);await page.locator('[name=name]').fill(protocol.toUpperCase()+' fixture');await page.locator('[name=host]').fill('localhost');
  await expect(page.locator('[name=port]')).toHaveValue(protocol==='rdp'?'3389':'5900');await page.getByRole('button',{name:'Salva connessione',exact:true}).click();
  await page.locator('.profile-item').filter({hasText:protocol.toUpperCase()+' fixture'}).dblclick();
 }
 await expect(page.locator('.desktop-pane')).toHaveCount(2);await expect(page.locator('.desktop-pane .xterm')).toHaveCount(0);await page.locator('#split-h').click();
 await expect.poll(()=>page.evaluate(()=>window.testCalls.filter(c=>c.action==='desktopLayout').at(-1)?.payload.rects.length)).toBe(2);
 await page.locator('#settings-btn').click();await expect.poll(()=>page.evaluate(()=>window.testCalls.filter(c=>c.action==='desktopLayout').at(-1)?.payload.rects.length)).toBe(0);
 await page.locator('#modal-close').click();await expect.poll(()=>page.evaluate(()=>window.testCalls.filter(c=>c.action==='desktopLayout').at(-1)?.payload.rects.length)).toBe(2);
});

test('connection actions remain visible in a compact window',async({page})=>{
 await page.setViewportSize({width:1000,height:680});await openNewConnection(page);await page.getByRole('tab',{name:'Autenticazione',exact:true}).click();await expect(page.getByRole('button',{name:'Salva connessione',exact:true})).toBeInViewport();await page.screenshot({path:'artifacts/ui-connection-compact.png',fullPage:true});
});

test('archive auto-lock refreshes the connection editor without losing typed input',async({page})=>{
 await openNewConnection(page);await page.getByRole('tab',{name:'Autenticazione',exact:true}).click();await page.getByRole('button',{name:'Crea archivio',exact:true}).click();
 await page.locator('[name=password]').fill('Unsaved fixture password');await page.evaluate(()=>window.fixtureEmit('vault',{unlocked:false}));
 await expect(page.getByRole('button',{name:'Sblocca archivio',exact:true})).toBeVisible();await expect(page.locator('[name=password]')).toHaveValue('Unsaved fixture password');await page.getByRole('button',{name:'Sblocca archivio',exact:true}).click();await expect(page.locator('#modal-body')).toContainText('Archivio sbloccato');
});

async function createConnection(page,name='SFTP fixture',protocol='ssh'){
 await openNewConnection(page,protocol);await page.locator('[name=name]').fill(name);await page.locator('[name=host]').fill('127.0.0.1');await page.locator('[name=username]').fill('fixture');await page.getByRole('button',{name:'Salva connessione',exact:true}).click();
}
test('named machines populate host and port without storing credentials',async({page})=>{
 await openNewConnection(page);await page.locator('[name=name]').fill('Production SSH');await page.locator('[name=host]').fill('127.0.0.1');await page.locator('[name=port]').fill('48221');await page.locator('[name=username]').fill('fixture');await page.getByLabel('Aggiungi a rubrica',{exact:true}).check();await expect(page.locator('[name=machineName]')).toBeVisible();await page.locator('[name=machineName]').fill('Contabo fixture');await page.getByRole('button',{name:'Salva connessione',exact:true}).click();
 await openNewConnection(page);await page.locator('[name=machineSelect]').selectOption('machine-fixture');await expect(page.locator('[name=host]')).toHaveValue('127.0.0.1');await expect(page.locator('[name=port]')).toHaveValue('48221');await page.locator('#modal-close').click();await page.locator('#machines-btn').click();await expect(page.locator('#modal-body')).toContainText('Contabo fixture');
});
test('SFTP opens as its own tab with local remote browser and queue copy paste',async({page})=>{
 const errors=[];page.on('pageerror',e=>errors.push(e.message));await createConnection(page);await page.locator('.profile-item').dblclick();await page.locator('#files-btn').click();await expect(page.locator('#tabs .tab')).toHaveCount(2);await expect(page.locator('.file-columns')).toBeVisible();await expect(page.locator('#files')).toBeHidden();
 await page.locator('.file-column[data-file-side=local] .workspace-file-row').click();await page.locator('.file-column[data-file-side=local] .workspace-file-row').click({button:'right'});await page.getByRole('menuitem',{name:'Copia file',exact:true}).click();await page.locator('.file-column[data-file-side=remote] .workspace-file-list').click({button:'right',position:{x:20,y:100}});await page.getByRole('menuitem',{name:'Incolla file dal pannello opposto'}).click();await expect(page.locator('.queue-row')).toHaveCount(1);expect(await page.evaluate(()=>window.testCalls.find(c=>c.action==='fileEnqueue').payload.direction)).toBe('upload');expect(errors).toEqual([]);
});
test('remote text opens in a third tab and save uses editor bridge',async({page})=>{
 await createConnection(page);await page.locator('.profile-item').dblclick();await page.locator('#files-btn').click();await page.locator('.file-column[data-file-side=remote] .workspace-file-row').dblclick();await expect(page.locator('#tabs .tab')).toHaveCount(3);await page.getByRole('button',{name:'Mostra editor integrato',exact:true}).click();const editor=page.getByRole('textbox',{name:'Contenuto del file remoto'});await expect(editor).toHaveValue('hello\nremote file\n');await editor.fill('edited\n');await editor.press('Control+s');await expect.poll(()=>page.evaluate(()=>window.testCalls.some(c=>c.action==='editorSave'&&c.payload.text==='edited\n'))).toBe(true);await expect(page.locator('.editor-toolbar')).toBeVisible();
});
test('context menus support fields and terminal paste without duplicate input',async({page})=>{
 await createConnection(page);await page.locator('.profile-item').click({button:'right'});await expect(page.getByRole('menuitem',{name:'Modifica connessione',exact:true})).toBeVisible();await page.getByRole('menuitem',{name:'Modifica connessione',exact:true}).click();const input=page.locator('[name=host]');await input.focus();await input.press('Control+a');await input.click({button:'right'});await page.getByRole('menuitem',{name:'Copia',exact:true}).click();expect(await page.evaluate(()=>window.fixtureClipboard)).toBe('127.0.0.1');await page.locator('#modal-close').click();await page.locator('.profile-item').dblclick();await page.evaluate(()=>window.fixtureClipboard='echo fixture');await page.locator('.terminal-surface').click({button:'right'});await expect(page.getByRole('menu')).toHaveCount(0);await expect.poll(()=>page.evaluate(()=>window.testCalls.filter(c=>c.action==='input'&&c.payload.data==='echo fixture').length)).toBe(1);
});
test('new file workspace remains usable at compact size',async({page})=>{
 await page.setViewportSize({width:1200,height:760});await createConnection(page,'Compact SFTP','sftp');await page.locator('.profile-item').dblclick();await expect(page.locator('#tabs .tab')).toHaveCount(1);await expect(page.locator('.file-workspace')).toBeVisible();await expect(page.getByRole('button',{name:'↑ Carica selezionati',exact:true})).toBeInViewport();await expect(page.locator('.file-queue')).toBeInViewport();await expect(page.locator('.sidebar')).toHaveCSS('width','360px');await page.screenshot({path:'artifacts/ui-sftp-017-compact.png'});
});

test('file conflict dialog shows both versions and applies selected resolution',async({page})=>{
 await createConnection(page,'Conflict fixture','sftp');await page.locator('.profile-item').dblclick();await page.evaluate(()=>window.fixtureConflict=true);await page.locator('.file-column[data-file-side=local] .workspace-file-row').click();await page.getByRole('button',{name:'↑ Carica selezionati',exact:true}).click();await expect(page.getByRole('heading',{name:'Il file esiste già a destinazione'})).toBeVisible();await expect(page.locator('.conflict-comparison')).toContainText('100 B');await expect(page.locator('.conflict-comparison')).toContainText('50 B');await page.screenshot({path:'artifacts/ui-sftp-017-conflict.png'});await page.getByRole('button',{name:'Riprendi',exact:true}).click();await expect.poll(()=>page.evaluate(()=>window.testCalls.find(c=>c.action==='fileEnqueue')?.payload.items[0].conflict)).toBe('resume');
});
test('drag from Windows uses native file objects and the visible SFTP destination',async({page})=>{
 await createConnection(page,'Drop fixture','sftp');await page.locator('.profile-item').dblclick();await page.locator('.file-column[data-file-side=remote] .workspace-file-list').evaluate(element=>{const transfer=new DataTransfer();transfer.items.add(new File(['fixture'],'local.txt',{type:'text/plain'}));element.dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:transfer}))});await expect.poll(()=>page.evaluate(()=>window.fixtureDroppedFileCount)).toBe(1);await expect.poll(()=>page.evaluate(()=>window.testCalls.find(c=>c.action==='fileEnqueue')?.payload.items[0].remote)).toBe('/home/test/local.txt');
});

test('locked archive is requested on startup and password action unlocks it',async({page})=>{
 await page.evaluate(()=>{window.fixtureState.vault.exists=true;window.fixtureState.vault.unlocked=false;boot()});await expect(page.getByRole('heading',{name:'Sblocca archivio',exact:true})).toBeVisible();await page.getByRole('button',{name:'Accedi con il tuo account',exact:true}).click();await expect(page.locator('#modal')).not.toBeVisible();await expect(page.locator('#vault-state')).toHaveText('SBLOCCATO');
});
test('files default to type groups and support independent grouping and order',async({page})=>{
 await createConnection(page,'Grouped fixture','sftp');await page.evaluate(()=>window.fixtureRemoteFiles=[{name:'b.txt',path:'/home/test/b.txt',size:20,modified:'2026-11-14',directory:false},{name:'a.log',path:'/home/test/a.log',size:10,modified:'2026-11-14',directory:false},{name:'a.txt',path:'/home/test/a.txt',size:30,modified:'2026-11-14',directory:false}]);await page.locator('.profile-item').dblclick();await expect(page.locator('.file-column[data-file-side=remote] .file-group')).toHaveText(['▾ File LOG (1)','▾ File TXT (2)']);await page.locator('[name=group-remote]').selectOption('none');await expect(page.locator('.file-column[data-file-side=remote] .file-group')).toHaveCount(0);await page.locator('[name=sort-remote]').selectOption('size');await page.locator('[name=order-remote]').selectOption('desc');await expect(page.locator('.file-column[data-file-side=remote] .workspace-file-row>span')).toHaveText(['a.txt','b.txt','a.log']);
});
test('file history home and main home preserve sessions',async({page})=>{
 await createConnection(page,'Navigation fixture','sftp');await page.locator('.profile-item').dblclick();const path=page.getByRole('textbox',{name:'Cartella remota',exact:true});await path.fill('/srv/projects');await path.press('Enter');await expect(path).toHaveValue('/srv/projects');await page.getByRole('button',{name:'Indietro remoto',exact:true}).click();await expect(path).toHaveValue('/home/test');await page.getByRole('button',{name:'Avanti remoto',exact:true}).click();await expect(path).toHaveValue('/srv/projects');await page.getByRole('button',{name:'Home remota',exact:true}).click();await expect(path).toHaveValue('/home/test');await page.getByRole('button',{name:'Pagina iniziale',exact:true}).click();await expect(page.locator('#welcome')).toBeVisible();await expect(page.locator('#tabs .tab')).toHaveCount(1);await page.locator('#tabs .tab').click();await expect(page.locator('.file-workspace')).toBeVisible();
});
test('conflict offers local highlighted side-by-side and unified text comparison',async({page})=>{
 await createConnection(page,'Diff fixture','sftp');await page.locator('.profile-item').dblclick();await page.evaluate(()=>window.fixtureConflict=true);await page.locator('.file-column[data-file-side=local] .workspace-file-row').click();await page.getByRole('button',{name:'↑ Carica selezionati',exact:true}).click();await page.getByRole('button',{name:'Confronta differenze',exact:true}).click();await expect(page.locator('.diff-cell.removed')).toContainText('remote old');await expect(page.locator('.diff-cell.added')).toContainText('local new');await page.locator('[name=diff-mode]').selectOption('unified');await expect(page.locator('.diff-content')).toHaveClass(/unified/);await page.screenshot({path:'artifacts/ui-diff-018.png'});await page.getByRole('button',{name:'Annulla',exact:true}).click();expect(await page.evaluate(()=>window.testCalls.filter(c=>c.action==='fileEnqueue').length)).toBe(0);
});
test('OSC7 follow directory and explicit reverse navigation use the matching SSH tab',async({page})=>{
 await createConnection(page,'Follow fixture');await page.locator('.profile-item').dblclick();await page.locator('#files-btn').click();await page.getByLabel('Segui cartella del terminale',{exact:true}).check();await page.evaluate(()=>{const shell=[...panes.values()].find(p=>!p.fileWorkspace&&!p.editor);shell.term.write('\x1b]7;file://fixture/home/test/project\x07')});await expect(page.getByRole('textbox',{name:'Cartella remota',exact:true})).toHaveValue('/home/test/project');await page.getByRole('button',{name:'Terminale qui',exact:true}).click();await expect.poll(()=>page.evaluate(()=>window.testCalls.some(c=>c.action==='input'&&c.payload.data==="cd -- '/home/test/project'\r"))).toBe(true);
});

test('startup Hello cancellation retains password unlock and does not open an SSH pane',async({page})=>{
 await page.evaluate(()=>{window.fixtureState.vault.exists=true;window.fixtureState.vault.windowsHello=true;window.fixtureState.vault.unlocked=false;window.fixtureHelloCancel=true;boot()});await page.getByRole('button',{name:'Sblocca con Windows Hello',exact:true}).click();await expect(page.getByRole('heading',{name:'Sblocca archivio',exact:true})).toBeVisible();await expect(page.getByRole('button',{name:'Accedi con il tuo account',exact:true})).toBeEnabled();await page.getByRole('button',{name:'Accedi con il tuo account',exact:true}).click();await expect(page.locator('#modal')).not.toBeVisible();await expect(page.locator('#tabs .tab')).toHaveCount(0);
});


test('multi-send has explicit SSH recipients, final Enter opt-in and clears the plan',async({page})=>{
 await page.evaluate(async()=>{for(const [id,name,protocol]of [['ssh-a','Fixture Alpha','ssh'],['ssh-b','Fixture Beta','ssh'],['files','Fixture Files','sftp']]){const p={id,name,protocol,host:'192.0.2.10',port:22,username:'fixture',folder:'Fixture',environment:'test',authentication:'password'};state.data.profiles.push(p);await connect(p)}window.chrome.webview.postMessage=((original)=>message=>{if(message.action==='multiSend'){window.testCalls.push({action:message.action,payload:message.payload});window.fixtureEmit('reply',{id:message.id,result:message.payload.sessionIds.map(id=>({id,sent:true,error:''}))});return}original(message)})(window.chrome.webview.postMessage)});
 await page.locator('#multi-send-btn').click();await expect(page.locator('.workflow-targets input')).toHaveCount(2);
 await expect(page.locator('.workflow-targets input:checked')).toHaveCount(0);
 await page.getByRole('button',{name:'Rivedi destinatari e invia'}).click();
 expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='multiSend').length)).toBe(0);
 await page.locator('.workflow-targets input').first().check();await page.locator('[name=multiText]').fill('echo fixture');
 await page.getByRole('button',{name:'Rivedi destinatari e invia'}).click();
 expect(await page.evaluate(()=>testCalls.find(c=>c.action==='multiSend').payload.enter)).toBe(false);
 await expect(page.locator('[name=multiText]')).toHaveValue('');await expect(page.locator('.workflow-targets input:checked')).toHaveCount(0);
 await expect(page.locator('#modal-body')).toContainText('testo inviato');
});
test('OpenSSH preview excludes unsupported entries and imports only chosen aliases',async({page})=>{
 await page.evaluate(()=>{window.chrome.webview.postMessage=((original)=>message=>{if(message.action==='sshConfigPreview'){window.fixtureEmit('reply',{id:message.id,result:{warnings:['Include non supportato nella fixture'],candidates:[{alias:'safe',profile:{username:'test',host:'192.0.2.1',port:22},warnings:['IdentityFile non letto']},{alias:'blocked',profile:null,warnings:['ProxyCommand non supportato']}]}});return}if(message.action==='sshConfigImport'){window.testCalls.push(message);window.fixtureEmit('reply',{id:message.id,result:window.fixtureState});return}original(message)})(window.chrome.webview.postMessage)});
 await page.locator('#ssh-config-btn').click();await page.getByRole('button',{name:'Seleziona file config',exact:true}).click();
 await expect(page.locator('.ssh-config-preview')).toContainText('IdentityFile non letto');
 await expect(page.locator('.ssh-config-candidate input').last()).toBeDisabled();
 await page.locator('.ssh-config-candidate input').first().check();await page.getByRole('button',{name:'Importa alias selezionati',exact:true}).click();
 expect(await page.evaluate(()=>testCalls.find(c=>c.action==='sshConfigImport').payload.aliases)).toEqual(['safe']);
});

test('automatic reconnect is opt-in, delayed, bounded and stops on authentication failure',async({page})=>{
 await expect(page.locator('body')).not.toHaveClass(/startup-covered/);await page.clock.install();await page.evaluate(async()=>{const p={id:'auto-ssh',name:'Auto fixture',protocol:'ssh',host:'192.0.2.10',port:22,username:'fixture',authentication:'password',folder:'Test',environment:'test'};state.data.profiles.push(p);await connect(p)});
 const sid=await page.evaluate(()=>[...panes.keys()][0]);
 await page.evaluate(id=>window.fixtureEmit('status',{sessionId:id,connected:false,text:'fixture disconnected'}),sid);
 await page.clock.fastForward(11000);expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='connect').length)).toBe(1);
 await page.evaluate(()=>reconnectPane([...panes.keys()][0]));
 await page.getByRole('checkbox',{name:'Riconnessione automatica Auto fixture'}).check();
 await page.evaluate(id=>{window.chrome.webview.postMessage=((original)=>m=>{if(m.action==='recoverProfile'&&m.payload.automatic){testCalls.push(m);window.fixtureEmit('reply',{id:m.id,error:'Authentication failed',retryable:false});return}original(m)})(window.chrome.webview.postMessage);window.fixtureEmit('status',{sessionId:id,connected:false,retryable:true,text:'lost'})},sid);
 await page.clock.fastForward(10000);await expect(page.getByRole('checkbox',{name:'Riconnessione automatica Auto fixture'})).not.toBeChecked();
 await page.clock.fastForward(120000);expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='recoverProfile'&&c.payload.automatic).length)).toBe(1);
});

test('recording is opt-in, status is visible and replay never sends terminal input',async({page})=>{
 await page.evaluate(async()=>{const p={id:'record-ssh',name:'Recording fixture',protocol:'ssh',host:'192.0.2.1',port:22,username:'fixture',authentication:'password',folder:'Test',environment:'test'};state.data.profiles.push(p);await connect(p);window.chrome.webview.postMessage=((original)=>m=>{if(m.action==='recordingStart'||m.action==='recordingStop'){testCalls.push(m);fixtureEmit('recordingStatus',{sessionId:m.payload.sessionId,active:m.action==='recordingStart'});fixtureEmit('reply',{id:m.id,result:null});return}if(m.action==='recordingList'){fixtureEmit('reply',{id:m.id,result:[{id:'fixture-record',created:'2026-11-14T12:00:00Z',bytes:200}]});return}if(m.action==='recordingRead'){fixtureEmit('reply',{id:m.id,result:{title:'Fixture replay',duration:1500,frames:[{milliseconds:0,data:btoa('fixture output\r\n')}]}});return}original(m)})(window.chrome.webview.postMessage)});
 expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='recordingStart').length)).toBe(0);
 await page.getByRole('button',{name:'● Registra',exact:true}).click();await expect(page.getByRole('button',{name:'■ Ferma REC',exact:true})).toBeVisible();
 await page.getByRole('button',{name:'■ Ferma REC',exact:true}).click();
 await page.locator('#recordings-btn').click();await page.getByRole('button',{name:'Riproduci',exact:true}).click();await expect(page.locator('.tab.active')).toContainText('Replay');
 await page.getByRole('button',{name:'Riproduci',exact:true}).click();await page.waitForTimeout(100);await page.locator('.replay-workspace textarea').press('x');
 expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='input').length)).toBe(0);
 await page.screenshot({path:'artifacts/ui-replay-019.png',fullPage:true});
});
test('automatic reconnect retries only network failures and never exceeds three attempts',async({page})=>{
 await expect(page.locator('body')).not.toHaveClass(/startup-covered/);await page.clock.install();await page.evaluate(async()=>{const p={id:'retry-ssh',name:'Retry fixture',protocol:'ssh',host:'192.0.2.10',port:22,username:'fixture',authentication:'password',folder:'Test',environment:'test'};state.data.profiles.push(p);await connect(p)});
 await page.getByRole('checkbox',{name:'Riconnessione automatica Retry fixture'}).check();
 await page.evaluate(()=>{window.chrome.webview.postMessage=((original)=>m=>{if(m.action==='recoverProfile'&&m.payload.automatic){testCalls.push(m);fixtureEmit('reply',{id:m.id,error:'Network timeout',retryable:true});return}original(m)})(window.chrome.webview.postMessage);fixtureEmit('status',{sessionId:[...panes.keys()][0],connected:false,retryable:true,text:'network lost'})});
 for(const ms of [10000,30000,60000]){await page.clock.fastForward(ms);await page.waitForTimeout(10)}
 await expect(page.getByRole('checkbox',{name:'Riconnessione automatica Retry fixture'})).not.toBeChecked();expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='recoverProfile'&&c.payload.automatic).length)).toBe(3);
 await page.clock.fastForward(600000);expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='recoverProfile'&&c.payload.automatic).length)).toBe(3);
});

test('manual address book creation and editing need no connection profile',async({page})=>{
 await page.evaluate(()=>{window.chrome.webview.postMessage=((original)=>m=>{if(m.action==='machineSave'){testCalls.push(m);fixtureState.data.machines=(fixtureState.data.machines||[]).filter(x=>x.id!==m.payload.id).concat(m.payload);fixtureEmit('reply',{id:m.id,result:fixtureState});return}original(m)})(window.chrome.webview.postMessage)});
 await page.locator('#machines-btn').click();await page.getByRole('button',{name:'Nuovo indirizzo',exact:true}).click();await page.locator('[name=machineName]').fill('Manual server');await page.locator('[name=machineHost]').fill('192.0.2.50');await page.locator('[name=machinePort]').fill('48221');await page.getByRole('button',{name:'Salva indirizzo',exact:true}).click();await expect(page.locator('#modal-body')).toContainText('192.0.2.50:48221');
 expect(await page.evaluate(()=>fixtureState.data.profiles.length)).toBe(0);await page.getByRole('button',{name:'Modifica',exact:true}).click();await page.locator('[name=machineName]').fill('Renamed server');await page.getByRole('button',{name:'Salva indirizzo',exact:true}).click();await expect(page.locator('#modal-body')).toContainText('Renamed server');expect(await page.evaluate(()=>fixtureState.data.machines.length)).toBe(1);
});
test('SSH selection automatically copies and clearing it preserves clipboard',async({page})=>{
 await page.evaluate(async()=>{const p={id:'copy-auto',name:'Copy fixture',protocol:'ssh',host:'192.0.2.1',port:22,username:'fixture',authentication:'password',folder:'Test',environment:'test'};state.data.profiles.push(p);await connect(p);const pane=[...panes.values()][0];await new Promise(resolve=>pane.term.write('\r\nAUTOCOPY_FIXTURE',resolve));pane.term.select(0,pane.term.buffer.active.cursorY,16)});
 await expect.poll(()=>page.evaluate(()=>window.fixtureClipboard)).toBe('AUTOCOPY_FIXTURE');await page.evaluate(()=>[...panes.values()][0].term.clearSelection());await page.waitForTimeout(180);expect(await page.evaluate(()=>window.fixtureClipboard)).toBe('AUTOCOPY_FIXTURE');
});
test('connection editor configures bounded hooks and custom SFTP command',async({page})=>{
 await openNewConnection(page);await page.locator('[name=name]').fill('Hook fixture');await page.locator('[name=host]').fill('192.0.2.1');await page.locator('[name=username]').fill('fixture');
 await page.getByRole('tab',{name:'SFTP',exact:true}).click();await page.locator('[name=subsystem]').fill('sudo -n /usr/lib/openssh/sftp-server');
 await page.getByRole('tab',{name:'Automazione',exact:true}).click();const card=page.locator('.hook-card').first();await card.locator('input[type=checkbox]').check();await card.locator('input[type=text]').fill('C:\\Windows\\System32\\whoami.exe');await card.locator('textarea').fill('/user');await page.getByRole('button',{name:'Salva connessione',exact:true}).click();
 const saved=await page.evaluate(()=>testCalls.find(c=>c.action==='saveProfile').payload);expect(saved.subsystem).toBe('sudo -n /usr/lib/openssh/sftp-server');expect(saved.hooks[0]).toMatchObject({phase:'before-connect',kind:'local',enabled:true,arguments:['/user'],timeoutSeconds:15});
 await page.evaluate(()=>profileDialog(state.data.profiles[0]));await page.getByRole('tab',{name:'Automazione',exact:true}).click();await expect(page.locator('.hook-card').first().locator('input[type=checkbox]')).toBeChecked();await page.screenshot({path:'artifacts/ui-hooks-0110.png',fullPage:true});
});

test('sequences have no skip mute or settings toggles and ignore playback hotkeys',async({page})=>{
 await page.locator('#settings-btn').click();for(const name of ['startupVideo','shutdownVideo','startupVolume','shutdownVolume'])await expect(page.locator('[name='+name+']')).toHaveCount(0);await page.locator('#modal-close').click();
 await page.evaluate(()=>{window.holdFixtureMedia=true;window.sequenceCompleted=false;playSequence('shutdown',.61,()=>window.sequenceCompleted=true)});
 await expect(page.locator('#sequence')).toBeVisible();await expect(page.locator('#sequence-skip,#sequence-mute')).toHaveCount(0);
 await page.keyboard.press('Escape');await page.keyboard.press('Space');await page.keyboard.press('Control+k');await expect(page.locator('#sequence')).toBeVisible();await expect(page.locator('#modal')).not.toBeVisible();expect(await page.evaluate(()=>window.sequenceCompleted)).toBe(false);
 await page.evaluate(()=>{const video=document.getElementById('sequence-video');video.muted=true;video.volume=0});await expect.poll(()=>page.evaluate(()=>({muted:document.getElementById('sequence-video').muted,volume:document.getElementById('sequence-video').volume}))).toEqual({muted:false,volume:.61});
 await page.evaluate(()=>document.getElementById('sequence-video').dispatchEvent(new Event('ended')));await expect(page.locator('#sequence')).not.toBeVisible();expect(await page.evaluate(()=>window.sequenceCompleted)).toBe(true);
});


test('SFTP folder comparison selects differences by direction and queues only confirmed paths',async({page})=>{
 await page.evaluate(async()=>{
  const entry=(relative,local,remote,status,transferable)=>({relative,local,remote,status,transferable});const stamp={size:4,modified:'2026-11-14T00:00:00Z'};
  window.chrome.webview.postMessage=((original)=>m=>{if(m.action==='folderCompare'){testCalls.push(m);fixtureEmit('reply',{id:m.id,result:{id:'folder-plan',items:[entry('same.txt',stamp,stamp,'Identico',false),entry('only-local.txt',stamp,null,'Solo locale',true),entry('only-remote.txt',null,stamp,'Solo remoto',true),entry('different.txt',stamp,stamp,'Contenuto diverso',true),entry('bad/<img>',stamp,stamp,'Conflitto nome/tipo',false)],skippedLinks:1}});return}if(m.action==='folderSync'){testCalls.push(m);fixtureEmit('reply',{id:m.id,result:{queued:m.payload.paths.length}});return}original(m)})(window.chrome.webview.postMessage);
  const profile={id:'sync-ui',name:'Sync fixture',protocol:'sftp',host:'192.0.2.1',port:22,username:'fixture',folder:'Test',authentication:'password',environment:'test'};fixtureState.data.profiles.push(profile);state.data.profiles.push(profile);await openFileWorkspace(profile);
 });
 await page.getByRole('button',{name:'Confronta cartelle',exact:true}).click();await expect(page.getByRole('dialog')).toContainText('5 elementi confrontati');
 await expect(page.getByRole('checkbox',{name:'Trasferisci only-local.txt',exact:true})).toBeChecked();await expect(page.getByRole('checkbox',{name:'Trasferisci only-remote.txt',exact:true})).toBeDisabled();await expect(page.getByRole('checkbox',{name:'Trasferisci different.txt',exact:true})).toBeChecked();await expect(page.getByRole('checkbox',{name:'Trasferisci bad/<img>',exact:true})).toBeDisabled();await expect(page.locator('.sync-list img')).toHaveCount(0);
 await page.locator('[name=sync-direction]').selectOption('download');await expect(page.getByRole('checkbox',{name:'Trasferisci only-remote.txt',exact:true})).toBeChecked();await expect(page.getByRole('checkbox',{name:'Trasferisci only-local.txt',exact:true})).toBeDisabled();
 await page.getByRole('checkbox',{name:'Mostra anche elementi identici'}).check();await expect(page.getByRole('checkbox',{name:'Trasferisci same.txt',exact:true})).toBeDisabled();
 const bounds=await page.getByRole('button',{name:'Trasferisci selezionati',exact:true}).boundingBox();expect(bounds.y+bounds.height).toBeLessThan(920);await page.screenshot({path:'artifacts/ui-sync-0112.png',fullPage:true});await page.getByRole('button',{name:'Trasferisci selezionati',exact:true}).click();
 const queued=await page.evaluate(()=>testCalls.find(c=>c.action==='folderSync').payload);expect(queued.direction).toBe('download');expect(queued.paths.sort()).toEqual(['different.txt','only-remote.txt']);
});

test('closing folder comparison cancels pending scan without transfers',async({page})=>{
 await page.evaluate(()=>{window.chrome.webview.postMessage=((original)=>m=>{if(m.action==='folderCompare'){testCalls.push(m);return}original(m)})(window.chrome.webview.postMessage);folderSyncDialog({id:'cancel-sync',files:true,localPath:'C:\\Fixture',remotePath:'/home/fixture',profile:{host:'192.0.2.1'}})});
 await expect(page.getByRole('dialog')).toContainText('verifica dei contenuti');await page.getByRole('button',{name:'Annulla confronto',exact:true}).click();expect(await page.evaluate(()=>testCalls.some(c=>c.action==='folderCancel'))).toBe(true);expect(await page.evaluate(()=>testCalls.some(c=>c.action==='folderSync'))).toBe(false);
});


test('dropping Windows files on SSH offers quoted insertion without executing a command',async({page})=>{
 await createConnection(page);await page.locator('.profile-item').dblclick();await page.evaluate(()=>{const data=new DataTransfer();data.items.add(new File(['fixture'],'local.txt'));document.querySelector('.terminal-surface').dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:data}))});
 await expect(page.getByRole('dialog')).toContainText('File nel terminale');await expect(page.getByRole('button',{name:'Carica tramite SFTP',exact:true})).toBeVisible();await page.getByRole('button',{name:'Inserisci percorsi locali',exact:true}).click();
 const input=await page.evaluate(()=>testCalls.filter(c=>c.action==='input').map(c=>c.payload.data));expect(input).toEqual(["'C:\\Fixture\\local.txt'"]);expect(input[0]).not.toMatch(/[\r\n]/);
 const quoted=await page.evaluate(()=>quoteTerminalPath("/home/o'brien/file name"));expect(quoted).toBe("'/home/o'\\''brien/file name'");
 expect(await page.evaluate(()=>{try{quoteTerminalPath('bad\ncommand');return false}catch{return true}})).toBe(true);
});

test('terminal upload chooses an explicit SFTP destination and uses the normal queue',async({page})=>{
 await createConnection(page);await page.locator('.profile-item').dblclick();await page.evaluate(()=>{const data=new DataTransfer();data.items.add(new File(['fixture'],'local.txt'));document.querySelector('.terminal-surface').dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:data}))});
 await page.locator('[name=terminalDropDestination]').fill('/staging');await page.getByRole('button',{name:'Carica tramite SFTP',exact:true}).click();await expect(page.locator('#tabs .tab')).toHaveCount(2);await expect(page.locator('.file-queue .queue-row')).toHaveCount(1);const plan=await page.evaluate(()=>testCalls.find(c=>c.action==='filePlan').payload);expect(plan.direction).toBe('upload');expect(plan.remoteDirectory).toBe('/staging');
});

test('recording retention is optional configurable and can be disabled again',async({page})=>{
 await page.locator('#recordings-btn').click();await expect(page.locator('[name=recordingRetentionDays]')).toHaveValue('0');await page.locator('[name=recordingRetentionDays]').fill('30');await page.getByRole('button',{name:'Salva conservazione automatica',exact:true}).click();await expect(page.locator('[name=recordingRetentionDays]')).toHaveValue('30');await page.locator('[name=recordingRetentionDays]').fill('0');await page.getByRole('button',{name:'Salva conservazione automatica',exact:true}).click();await expect(page.locator('[name=recordingRetentionDays]')).toHaveValue('0');expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='recordingRetentionSet').map(c=>c.payload.days))).toEqual([30,0]);
});


test('PuTTYgen conversion has guided native flow name comment and verified vault import',async({page})=>{
 await page.locator('#keys-btn').click();await page.getByRole('button',{name:'Converti PPK / OpenSSH',exact:true}).click();await page.getByRole('button',{name:'Seleziona originale e apri PuTTYgen',exact:true}).click();await expect(page.getByRole('dialog')).toContainText('Export OpenSSH key (force new file format)');await expect(page.getByRole('dialog')).toContainText('SHA256:fixture');await expect(page.getByRole('button',{name:'Verifica e importa nell’archivio',exact:true})).toBeDisabled();
 await page.evaluate(()=>{fixtureState.vault.exists=true;fixtureState.vault.unlocked=true;update(fixtureState)});await page.evaluate(()=>keyConversionDialog());await page.locator('[name=conversionName]').fill('Imported via PuTTYgen');await page.locator('[name=conversionComment]').fill('Verified roundtrip');await page.screenshot({path:'artifacts/ui-conversion-0114.png',fullPage:true});await page.getByRole('button',{name:'Verifica e importa nell’archivio',exact:true}).click();await expect(page.getByRole('dialog')).toContainText('Conversione verificata');await page.getByRole('button',{name:'Chiavi salvate',exact:true}).click();await expect(page.getByRole('table',{name:'Elenco delle chiavi SSH'})).toContainText('Imported via PuTTYgen');const request=await page.evaluate(()=>testCalls.find(c=>c.action==='conversionFinish').payload);expect(request).toMatchObject({id:'convert-fixture',import:true,name:'Imported via PuTTYgen',comment:'Verified roundtrip'});expect(JSON.stringify(request)).not.toMatch(/private|passphrase|password/i);
});

test('conversion can verify without vault and rejects mismatch without success',async({page})=>{
 await page.evaluate(()=>keyConversionDialog());await page.locator('[name=conversionTarget]').selectOption('ppk');await page.getByRole('button',{name:'Seleziona originale e apri PuTTYgen',exact:true}).click();await expect(page.getByRole('dialog')).toContainText('Save private key');await page.evaluate(()=>window.fixtureConversionFailure=true);await page.getByRole('button',{name:'Verifica soltanto',exact:true}).click();await expect(page.locator('#toasts')).toContainText('chiave diversa');await expect(page.getByRole('dialog')).not.toContainText('Conversione verificata');await page.evaluate(()=>window.fixtureConversionFailure=false);await page.getByRole('button',{name:'Verifica soltanto',exact:true}).click();await expect(page.getByRole('dialog')).toContainText('Impronta identica');expect(await page.evaluate(()=>fixtureState.vault.keys.length)).toBe(0);
});

test('conversion cancellation releases guided state and start can be retried',async({page})=>{
 await page.evaluate(()=>keyConversionDialog());await page.getByRole('button',{name:'Seleziona originale e apri PuTTYgen',exact:true}).click();await page.getByRole('button',{name:'Annulla conversione',exact:true}).click();await expect(page.locator('[name=conversionTarget]')).toBeVisible();expect(await page.evaluate(()=>testCalls.some(c=>c.action==='conversionCancel'))).toBe(true);
});

test('OpenSSH config write requires explicit preview and apply, showing config as text',async({page})=>{
 await page.evaluate(()=>{state.data.profiles.push({id:'config-fixture',name:'Config fixture',protocol:'ssh',host:'192.0.2.55',port:22,username:'fixture'});window.chrome.webview.postMessage=((original)=>m=>{if(m.action==='sshConfigWritePreview'){testCalls.push(m);fixtureEmit('reply',{id:m.id,result:{id:'plan-fixture',path:'C:/fixture/config',before:'# original',after:'Host fixture\n HostName <script>never-run</script>',expires:'2026-11-14T22:00:00Z'}});return}if(m.action==='sshConfigWriteApply'){testCalls.push(m);fixtureEmit('reply',{id:m.id,result:{saved:true,backup:'C:/fixture/config.relay-backup-fixture'}});return}original(m)})(window.chrome.webview.postMessage)});
 await page.locator('#ssh-config-btn').click();await page.getByRole('checkbox',{name:/Config fixture/}).check();await page.getByRole('button',{name:'Anteprima scrittura nel config esistente'}).click();
 await expect(page.getByRole('heading',{name:'Rivedi il config OpenSSH'})).toBeVisible();await expect(page.getByLabel('Config risultante')).toContainText('<script>never-run</script>');expect(await page.evaluate(()=>testCalls.some(c=>c.action==='sshConfigWriteApply'))).toBe(false);
 await page.screenshot({path:'artifacts/ui-config-write-0115.png',fullPage:true});await page.getByRole('button',{name:'Conferma scrittura del config'}).click();expect(await page.evaluate(()=>testCalls.find(c=>c.action==='sshConfigWriteApply').payload)).toEqual({id:'plan-fixture'});
});
test('Recording text export needs unlock and explicit export action',async({page})=>{
 await page.evaluate(()=>{window.chrome.webview.postMessage=((original)=>m=>{if(m.action==='recordingList'){fixtureEmit('reply',{id:m.id,result:[{id:'log-fixture',created:'2026-11-14T12:00:00Z',bytes:200}]});return}if(m.action==='recordingExportText'){testCalls.push(m);fixtureEmit('reply',{id:m.id,result:{saved:true,fileName:'fixture.txt',bytes:50}});return}original(m)})(window.chrome.webview.postMessage)});
 await page.locator('#recordings-btn').click();expect(await page.evaluate(()=>testCalls.some(c=>c.action==='recordingExportText'))).toBe(false);await page.getByRole('button',{name:'Esporta testo',exact:true}).click();expect(await page.evaluate(()=>testCalls.find(c=>c.action==='recordingExportText').payload)).toEqual({recordingId:'log-fixture'});await expect(page.locator('body')).toContainText('Testo esportato: fixture.txt');
});

test('SSH keepalive interval is editable and saved as a number including zero',async({page})=>{
 await openNewConnection(page);await page.getByRole('tab',{name:'Connessione',exact:true}).click();await page.locator('#modal input[name="name"]').fill('Keepalive fixture');await page.locator('#modal input[name="host"]').fill('192.0.2.55');await page.locator('#modal input[name="username"]').fill('fixture');await page.getByRole('tab',{name:'Rete e SSH',exact:true}).click();await page.locator('input[name="keepAliveSeconds"]').fill('0');await page.getByRole('button',{name:'Salva connessione',exact:true}).click();expect(await page.evaluate(()=>testCalls.find(c=>c.action==='saveProfile').payload.keepAliveSeconds)).toBe(0);
});

test('live SSH tab detaches and reattaches preserving terminal buffer and session',async({page})=>{
 await page.evaluate(async()=>{const p={id:'window-fixture',name:'Window fixture',protocol:'ssh',host:'192.0.2.55',port:22,username:'fixture',folder:'Test'};state.data.profiles.push(p);const pane=await connect(p);pane.term.writeln('BEFORE DETACH fixture');});
 const popupPromise=page.waitForEvent('popup');await page.getByRole('button',{name:'Finestra separata Window fixture'}).click();const popup=await popupPromise;await popup.waitForLoadState();await expect(popup.locator('.terminal-pane')).toBeVisible();expect(await page.evaluate(()=>[...panes.values()][0].detachedWindow!==null)).toBe(true);
 await page.evaluate(()=>{const p=[...panes.values()][0];fixtureEmit('output',{sessionId:p.id,data:btoa('AFTER DETACH fixture\r\n')})});await expect(popup.locator('.xterm-screen')).toBeVisible();expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='connect').length)).toBe(1);
 expect(await page.evaluate(()=>{const p=[...panes.values()][0];return Array.from({length:p.term.buffer.active.length},(_,i)=>p.term.buffer.active.getLine(i).translateToString()).join('\n')})).toContain('BEFORE DETACH');
 await popup.setViewportSize({width:1100,height:700});await page.evaluate(()=>reattachDetachedPane([...panes.keys()][0]));await expect(page.locator('.terminal-pane')).toBeVisible();expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='disconnect').length)).toBe(0);await popup.close();
});
test('SFTP separate window keeps navigation and selects files for native Explorer export',async({page})=>{
 await page.evaluate(async()=>{const p={id:'floating-sftp',name:'Floating SFTP',protocol:'sftp',host:'192.0.2.55',port:22,username:'fixture',folder:'Test'};state.data.profiles.push(p);fixtureState.data.profiles.push(p);window.fixtureRemoteFiles=[{name:'fixture.txt',path:'/fixture.txt',directory:false,size:24,modified:'2026-01-01'}];await openFileWorkspace(p)});
 const popupPromise=page.waitForEvent('popup');await page.getByRole('button',{name:'Finestra separata SFTP · Floating SFTP'}).click();const popup=await popupPromise;await popup.waitForLoadState();await popup.locator('[data-file-side="remote"] .workspace-file-row').click();await popup.getByRole('button',{name:'Trascina verso Esplora file',exact:true}).click();expect(await page.evaluate(()=>testCalls.find(c=>c.action==='prepareExplorerExport').payload.paths)).toEqual(['/fixture.txt']);expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='connect').length)).toBe(1);await page.evaluate(()=>reattachDetachedPane([...panes.keys()][0]));await popup.close();
});

test('split slots select and swap sessions, resize, zoom and preserve transports',async({page})=>{
 await createConnection(page,'Split fixture','ssh');for(let i=0;i<4;i++)await page.locator('.profile-item').dblclick();
 await page.getByRole('button',{name:'Quattro riquadri',exact:true}).click();await expect(page.locator('.terminal-pane:visible')).toHaveCount(4);await expect(page.locator('[role=separator]')).toHaveCount(2);
 const ids=await page.locator('#tabs .tab').evaluateAll(t=>t.map(x=>x.dataset.sessionId));await page.getByLabel('Scheda riquadro 1',{exact:true}).selectOption(ids[3]);await expect(page.getByLabel('Scheda riquadro 4',{exact:true})).toHaveValue(ids[0]);
 await page.getByRole('separator',{name:'Larghezza riquadri'}).focus();await page.keyboard.press('ArrowRight');await expect(page.getByRole('separator',{name:'Larghezza riquadri'})).toHaveAttribute('aria-valuenow','52');
 const divider=page.getByRole('separator',{name:'Altezza riquadri'});const rect=await divider.boundingBox();await page.mouse.move(rect.x+rect.width/2,rect.y+rect.height/2);await page.mouse.down();await page.mouse.move(rect.x+rect.width/2,rect.y-70);await page.mouse.up();expect(await page.evaluate(()=>splitRatios[1])).toBeLessThan(.5);
 await page.getByRole('button',{name:'Ingrandisci riquadro attivo',exact:true}).click();await expect(page.locator('.terminal-pane:visible')).toHaveCount(1);await page.getByRole('button',{name:'Ingrandisci riquadro attivo',exact:true}).click();await expect(page.locator('.terminal-pane:visible')).toHaveCount(4);
 await page.locator('#split-v').click();await expect(page.locator('.terminal-pane:visible')).toHaveCount(2);await page.locator('#tabs .tab').nth(1).click();await page.locator('#tabs .tab').nth(2).click();expect(await page.evaluate(()=>splitPaneIds[1])).toBe(ids[2]);await page.getByRole('button',{name:'Una sola scheda',exact:true}).click();await expect(page.locator('.terminal-pane:visible')).toHaveCount(1);expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='connect').length)).toBe(4);expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='disconnect').length)).toBe(0);
});
test('workspace captures and restores split assignments and detached positions without secrets',async({page})=>{
 await createConnection(page,'Workspace fixture','ssh');for(let i=0;i<3;i++)await page.locator('.profile-item').dblclick();const ids=await page.locator('#tabs .tab').evaluateAll(t=>t.map(x=>x.dataset.sessionId));
 const popupPromise=page.waitForEvent('popup');await page.getByRole('button',{name:'Finestra separata Workspace fixture',exact:true}).nth(2).click();const popup=await popupPromise;await expect(popup.locator('.terminal-pane')).toBeVisible();
 const placement={monitor:'fixture-monitor',x:.2,y:.1,width:.5,height:.6,maximized:true,order:0};await page.evaluate(({id,placement})=>window.fixtureWindowPlacements={[id]:placement},{id:ids[2],placement});await page.locator('#split-h').click();await page.getByLabel('Scheda riquadro 1',{exact:true}).selectOption(ids[1]);await page.evaluate(()=>splitRatios=[.65,.4]);
 await page.locator('#save-workspace').click();await page.locator('[name=name]').fill('Split and float');await page.getByRole('button',{name:'Salva disposizione',exact:true}).click();const w=await page.evaluate(()=>testCalls.find(c=>c.action==='saveWorkspace').payload);expect(w.windowPlacements).toEqual([null,null,placement]);expect(w.splitPaneIndices).toEqual([1,0]);expect(w.splitRatios).toEqual([.65,.4]);expect(JSON.stringify(w)).not.toContain('password');
 await page.evaluate(()=>reattachDetachedPane([...panes.keys()][2]));await popup.close();for(const id of ids)await page.evaluate(id=>closePane(id),id);
 const reopened=page.waitForEvent('popup');await page.evaluate(w=>{window.fixtureRestore=restoreWorkspace(w)},w);const child=await reopened;await expect(child.locator('.terminal-pane')).toBeVisible();await page.evaluate(()=>window.fixtureRestore);await expect(page.locator('.terminal-pane:visible')).toHaveCount(2);expect(await page.evaluate(()=>splitRatios)).toEqual([.65,.4]);expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='workspaceWindowPrepare').at(-1).payload.placement)).toEqual(placement);await page.screenshot({path:'artifacts/ui-workspace-splits-0117.png'});await page.evaluate(()=>reattachDetachedPane([...panes.keys()][2]));await child.close();
});
test('old workspace remains compatible and missing profiles do not discard open sessions',async({page})=>{
 await createConnection(page,'Old workspace','ssh');await page.locator('.profile-item').dblclick();await page.evaluate(async()=>{const p=state.data.profiles[0];await restoreWorkspace({name:'Legacy',layout:'columns',profileIds:[p.id,'missing'],tabNames:['Restored','Missing']})});await expect(page.locator('#tabs .tab')).toHaveCount(2);await expect(page.locator('.toast')).toContainText(['Workspace aperto: 1 elementi non ripristinati; le schede disponibili restano accessibili.']);await expect(page.locator('#tabs')).toContainText('Restored');
});

test('local profile has shell, folder and startup commands without SSH credentials',async({page})=>{
 await openNewConnection(page,'pwsh');await page.locator('[name=name]').fill('Local PowerShell');await expect(page.getByRole('tab',{name:'Autenticazione',exact:true})).toHaveCount(0);await expect(page.locator('[name=host]')).toBeHidden();await page.getByRole('tab',{name:'Shell locale',exact:true}).click();await page.locator('[name=localShell]').selectOption('pwsh');await page.getByRole('button',{name:'Scegli cartella iniziale',exact:true}).click();await expect(page.locator('[name=startupDirectory]')).toHaveValue('C:\\Fixture\\Local');await page.locator('[name=startupCommands]').fill("Write-Output 'Welcome'\nGet-Location");await page.getByRole('button',{name:'Salva connessione',exact:true}).click();const p=await page.evaluate(()=>testCalls.find(c=>c.action==='saveProfile').payload);expect(p.protocol).toBe('local');expect(p.localShell).toBe('pwsh');expect(p.startupCommands).toContain('Get-Location');expect(p.keyId).toBe('');expect(p.password).toBe('');await expect(page.locator('.profile-item')).toContainText('PowerShell 7');await page.locator('.profile-item').dblclick();await expect(page.locator('.pane-status')).toHaveText('In esecuzione');await expect(page.getByRole('button',{name:'● Registra',exact:true})).toHaveCount(0);await expect(page.locator('.auto-reconnect')).toHaveCount(0);expect(await page.evaluate(()=>testCalls.filter(c=>['vaultUnlock','helloUnlock'].includes(c.action)).length)).toBe(0);await page.screenshot({path:'artifacts/ui-local-powershell-0118.png'});
 await page.evaluate(()=>fixtureEmit('localStatus',{sessionId:[...panes.keys()][0],connected:false,text:'Processo terminato · codice 0'}));await expect(page.getByRole('button',{name:'Riavvia',exact:true})).toBeVisible();await page.getByRole('button',{name:'Riavvia',exact:true}).click();expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='connect').length)).toBe(2);
});
test('WSL selects a distribution and a Linux path and saves in workspace',async({page})=>{
 await openNewConnection(page,'wsl');await page.locator('[name=name]').fill('Linux development');await page.getByRole('tab',{name:'Shell locale',exact:true}).click();await page.locator('[name=localShell]').selectOption('wsl');await page.locator('[name=wslDistribution]').selectOption('Ubuntu');await expect(page.getByRole('button',{name:'Scegli cartella iniziale',exact:true})).toBeHidden();await page.locator('[name=startupDirectory]').fill('/srv/projects');await page.locator('[name=startupCommands]').fill('pwd');await page.screenshot({path:'artifacts/ui-local-editor-0118.png'});await page.getByRole('button',{name:'Salva connessione',exact:true}).click();await page.locator('.profile-item').dblclick();await page.locator('#save-workspace').click();await page.getByRole('button',{name:'Salva disposizione',exact:true}).click();const w=await page.evaluate(()=>testCalls.find(c=>c.action==='saveWorkspace').payload);await page.evaluate(w=>restoreWorkspace(w),w);await expect(page.locator('#tabs .tab')).toHaveCount(2);expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='connect').length)).toBe(2);await page.locator('#split-h').click();await expect(page.locator('.terminal-pane:visible')).toHaveCount(2);const popupPromise=page.waitForEvent('popup');await page.getByRole('button',{name:'Finestra separata Linux development',exact:true}).first().click();const popup=await popupPromise;await expect(popup.locator('.terminal-pane')).toBeVisible();await page.evaluate(()=>reattachDetachedPane([...panes.keys()][0]));await popup.close();
});

async function openNewConnection(page,type='ssh'){
 await page.locator('#new-profile').click();await page.locator('[data-session-type="'+type+'"]').click();
}
test('session picker cancels without saving and filters available session types',async({page})=>{
 await page.locator('#welcome-new').click();await expect(page.locator('.session-type-card')).toHaveCount(21);await expect(page.locator('[name=host]')).toHaveCount(0);
 await page.getByLabel('Cerca tipo di sessione').fill('WSL');await expect(page.locator('.session-type-card:visible')).toHaveCount(1);await page.screenshot({path:'artifacts/ui-picker-filter-0119.png'});
 await page.getByRole('button',{name:'Annulla',exact:true}).click();expect(await page.evaluate(()=>testCalls.some(c=>['saveProfile','connect'].includes(c.action)))).toBe(false);
 await page.locator('#new-profile').click();await page.getByLabel('Cerca tipo di sessione').fill('does not exist');await expect(page.getByText('Nessun tipo corrisponde alla ricerca.',{exact:true})).toBeVisible();
});
test('new type-specific forms preserve common draft but discard credentials when changing type',async({page})=>{
 await page.locator('#welcome-new').click();await page.screenshot({path:'artifacts/ui-session-picker-0119.png'});await page.locator('[data-session-type=ssh]').click();await page.locator('[name=name]').fill('Draft machine');await page.locator('[name=host]').fill('localhost');await page.getByRole('tab',{name:'Autenticazione',exact:true}).click();await page.locator('[name=password]').fill('discard this secret');
 await page.getByRole('button',{name:'← Cambia tipo',exact:true}).click();await page.locator('[data-session-type=rdp]').click();await expect(page.locator('[name=name]')).toHaveValue('Draft machine');await expect(page.locator('[name=host]')).toHaveValue('localhost');await expect(page.locator('[name=port]')).toHaveValue('3389');await expect(page.locator('[name=protocol]')).toBeDisabled();await expect(page.getByRole('tab',{name:'SFTP',exact:true})).toHaveCount(0);await expect(page.getByRole('tab',{name:'Automazione',exact:true})).toHaveCount(0);await page.getByRole('tab',{name:'Autenticazione',exact:true}).click();await expect(page.locator('[name=password]')).toHaveValue('');await page.getByRole('button',{name:'Salva connessione',exact:true}).click();const p=await page.evaluate(()=>testCalls.find(c=>c.action==='saveProfile').payload);expect(p.protocol).toBe('rdp');expect(p.port).toBe(3389);expect(p.password).toBe('');
 await page.locator('.profile-item .edit-profile').click();await expect(page.locator('.session-type-card')).toHaveCount(0);await expect(page.locator('[name=name]')).toHaveValue('Draft machine');
});
test('picker is usable in compact windows with keyboard focus',async({page})=>{
 await page.setViewportSize({width:700,height:680});await page.locator('#new-profile').click();await page.getByLabel('Cerca tipo di sessione').fill('CMD');await page.keyboard.press('Tab');await expect(page.locator('[data-session-type=cmd]')).toBeFocused();await page.keyboard.press('Enter');await expect(page.locator('[name=localShell]')).toHaveValue('cmd');await expect(page.getByRole('tab',{name:'Autenticazione',exact:true})).toHaveCount(0);await expect(page.getByRole('button',{name:'Salva connessione',exact:true})).toBeInViewport();
});

test('RDP stores SSH jump route and unlocks its credentials before desktop connection',async({page})=>{
 await createConnection(page,'SSH bastion');await page.evaluate(()=>{for(const source of [state.data,window.fixtureState.data]){const p=source.profiles[0];p.passwordId='fixture-password';p.authentication='password';}state.vault={...state.vault,exists:true,unlocked:false};});
 await openNewConnection(page,'rdp');await page.locator('[name=name]').fill('RDP through SSH');await page.locator('[name=host]').fill('192.0.2.30');await page.getByRole('tab',{name:'Rete',exact:true}).click();await page.locator('[name=jumpHost]').selectOption({label:'SSH bastion / 127.0.0.1'});await expect(page.locator('[name=keepAliveSeconds]')).toBeHidden();await page.getByRole('button',{name:'Salva connessione',exact:true}).click();const p=await page.evaluate(()=>testCalls.filter(c=>c.action==='saveProfile').at(-1).payload);expect(p.jumpHost).not.toBe('');expect(p.protocol).toBe('rdp');
 await page.locator('.profile-item').filter({hasText:'RDP through SSH'}).dblclick();await expect(page.locator('#modal-body')).toContainText('SSH bastion');expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='connect').length)).toBe(0);
});

async function seedCertificate(page){await page.evaluate(()=>{const certificate={text:'ssh-ed25519-cert-v01@openssh.com FixtureCertificate',revocationIdentity:'CA:serial:1',keyId:'fixture-certificate-id',principals:['alice'],validFrom:'2026-11-14T20:00:00Z',validUntil:'2026-11-14T21:00:00Z',authorityFingerprint:'SHA256:fixture-authority'};Object.assign(fixtureState.vault,{exists:true,unlocked:true,keys:[{id:'cert-key',name:'Certificate fixture',algorithm:'ssh-ed25519',created:'2026-11-14T20:00:00Z',publicKey:'ssh-ed25519 fixture',fingerprint:'SHA256:fixture',certificate},{id:'ca-key',name:'Authority fixture',algorithm:'ssh-ed25519',created:'2026-11-14T20:00:00Z',publicKey:'ssh-ed25519 fixture-ca',fingerprint:'SHA256:fixture-ca'}],revocations:[]});update(structuredClone(fixtureState));});}
test('revocation ledger is locked and empty export is disabled',async({page})=>{
 await page.locator('#keys-btn').click();await expect(page.getByRole('button',{name:'Revoche / KRL',exact:true})).toBeDisabled();await page.getByRole('button',{name:'Crea archivio',exact:true}).click();await page.getByRole('button',{name:'Revoche / KRL',exact:true}).click();await expect(page.getByRole('button',{name:'Esporta KRL OpenSSH',exact:true})).toBeDisabled();await expect(page.locator('#modal-body')).toContainText('Nessun certificato revocato.');
});
test('certificate revocation requires reason and remains in ledger after removing certificate',async({page})=>{
 await seedCertificate(page);await page.locator('#keys-btn').click();await page.getByRole('button',{name:'Certificato',exact:true}).click();await page.getByRole('button',{name:'Revoca certificato',exact:true}).click();await page.getByRole('button',{name:'Conferma revoca',exact:true}).click();expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='certificateRevoke').length)).toBe(0);await page.locator('[name=reason]').fill('Retired <script>not code</script>');await page.getByRole('button',{name:'Conferma revoca',exact:true}).click();await expect(page.locator('#modal-body')).toContainText('CERTIFICATO REVOCATO');await expect(page.getByRole('button',{name:'Revoca certificato',exact:true})).toBeDisabled();await page.getByRole('button',{name:'Rimuovi associazione',exact:true}).click();await page.getByRole('button',{name:'Revoche / KRL',exact:true}).click();await expect(page.locator('.revocation-card')).toContainText('Retired <script>not code</script>');await expect(page.locator('.revocation-card script')).toHaveCount(0);await page.getByRole('button',{name:'Esporta KRL OpenSSH',exact:true}).click();await expect(page.locator('#modal-body')).toContainText('KRL esportata: fixture-revoked.krl');await page.screenshot({path:'artifacts/ui-revocations-0120.png'});
});
test('cancelled revocation leaves certificate usable and failed KRL export shows no success',async({page})=>{
 await seedCertificate(page);await page.evaluate(()=>fixtureCancelRevocation=true);await page.locator('#keys-btn').click();await page.getByRole('button',{name:'Certificato',exact:true}).click();await page.getByRole('button',{name:'Revoca certificato',exact:true}).click();await page.locator('[name=reason]').fill('Cancelled');await page.getByRole('button',{name:'Conferma revoca',exact:true}).click();await expect(page.getByRole('button',{name:'Revoca certificato',exact:true})).toBeEnabled();await page.evaluate(()=>{fixtureState.vault.revocations=[{id:'fixture',name:'Certificate fixture',reason:'Test reason',revokedAt:'2026-11-14T20:00:00Z',certificate:fixtureState.vault.keys[0].certificate}];update(structuredClone(fixtureState));fixtureKrlFailure=true;});await page.evaluate(()=>revocationsDialog());await page.getByRole('button',{name:'Esporta KRL OpenSSH',exact:true}).click();await expect(page.locator('#modal-body')).not.toContainText('KRL esportata:');await expect(page.getByRole('button',{name:'Esporta KRL OpenSSH',exact:true})).toBeEnabled();
});
test('external KRL query shows explicit verdict and keeps failures distinct',async({page})=>{
 await seedCertificate(page);await page.locator('#keys-btn').click();await page.getByRole('button',{name:'Certificato',exact:true}).click();await page.evaluate(()=>fixtureKrlRevoked=true);await page.getByRole('button',{name:'Verifica in KRL esterna',exact:true}).click();await expect(page.locator('#modal-body')).toContainText('REVOCATO nella lista external-fixture.krl');await page.evaluate(()=>{fixtureKrlFailure=true;certificateDialog(state.vault.keys[0]);});await page.getByRole('button',{name:'Verifica in KRL esterna',exact:true}).click();await expect(page.locator('#modal-body')).not.toContainText('Non presente nella lista');
});

async function openKeyGeneration(page){await page.locator('#keys-btn').click();await page.getByRole('button',{name:'Crea archivio',exact:true}).click();await page.getByRole('button',{name:'Genera chiave',exact:true}).click();}
test('algorithm cards replace combo with descriptions ratings and recommended default',async({page})=>{
 await openKeyGeneration(page);await expect(page.locator('select[name=algorithm]')).toHaveCount(0);await expect(page.getByRole('radio')).toHaveCount(7);await expect(page.getByRole('radio',{name:'Ed25519',exact:true})).toBeChecked();for(const card of await page.locator('label.algorithm-card[data-algorithm]').all())await expect(card).toContainText(/Sicurezza classica [89]|Sicurezza classica 10/);await page.screenshot({path:'artifacts/ui-algorithm-cards-0121.png'});await page.getByText('Come leggere il voto da 1 a 10',{exact:true}).click();await expect(page.locator('.algorithm-criteria').first()).toContainText('non un voto NIST');await expect(page.locator('#modal-body')).toContainText('Non sono percentuali');
});
test('each available algorithm sends its exact value and keeps the supplied key name',async({page})=>{
 await openKeyGeneration(page);for(const id of ['ed25519','rsa3072','rsa4096','rsa8192','ecdsa256','ecdsa384','ecdsa521']){await page.locator('[name=name]').fill('Key '+id);await page.locator('input[name=algorithm][value='+id+']').check();await page.getByRole('button',{name:'Genera nel deposito',exact:true}).click();const payload=await page.evaluate(()=>testCalls.filter(c=>c.action==='vaultGenerate').at(-1).payload);expect(payload).toEqual({name:'Key '+id,algorithm:id});await page.getByRole('button',{name:'Genera chiave',exact:true}).click();}
});
test('algorithm cards allow keyboard selection and do not generate on cancellation',async({page})=>{
 await openKeyGeneration(page);await page.getByRole('radio',{name:'Ed25519',exact:true}).focus();await page.keyboard.press('ArrowRight');await expect(page.getByRole('radio',{name:'RSA 3072 bit',exact:true})).toBeChecked();await expect(page.locator('.generation-selected')).toHaveText('Selezionata: RSA 3072 bit');await page.getByRole('button',{name:'Annulla',exact:true}).click();expect(await page.evaluate(()=>testCalls.some(c=>c.action==='vaultGenerate'))).toBe(false);
});
test('compact algorithm cards scroll without hiding generate actions or overflowing',async({page})=>{
 await page.setViewportSize({width:700,height:680});await openKeyGeneration(page);await page.getByRole('radio',{name:'ECDSA NIST P-521',exact:true}).check();await expect(page.getByRole('button',{name:'Genera nel deposito',exact:true})).toBeInViewport();await expect(page.getByRole('button',{name:'Annulla',exact:true})).toBeInViewport();expect(await page.locator('#modal').evaluate(el=>el.scrollWidth<=el.clientWidth+1)).toBe(true);await page.screenshot({path:'artifacts/ui-algorithm-compact-0121.png'});
});
test('advanced algorithm availability is explicit without unusable generation options',async({page})=>{
 await openKeyGeneration(page);await page.getByText('Hardware, Ed448 e firme post-quantistiche',{exact:true}).click();await expect(page.locator('.algorithm-advanced')).toContainText('NON DISPONIBILE NEL MOTORE');await expect(page.locator('.algorithm-advanced')).toContainText('Non e automaticamente meno sicura');await expect(page.locator('.algorithm-advanced input')).toHaveCount(0);await page.getByRole('button',{name:'Gestisci token / agent',exact:true}).first().click();await expect(page.locator('#modal-title')).toHaveText('Token, smart card e agent');expect(await page.evaluate(()=>testCalls.some(c=>c.action==='vaultGenerate'))).toBe(false);
});

test('failed generation preserves selected card and typed name without claiming success',async({page})=>{
 await openKeyGeneration(page);await page.locator('[name=name]').fill('Keep this name');await page.getByRole('radio',{name:'RSA 8192 bit',exact:true}).check();await page.evaluate(()=>fixtureGenerationFailure=true);await page.getByRole('button',{name:'Genera nel deposito',exact:true}).click();await expect(page.getByRole('button',{name:'Genera nel deposito',exact:true})).toBeEnabled();await expect(page.getByRole('radio',{name:'RSA 8192 bit',exact:true})).toBeChecked();await expect(page.locator('[name=name]')).toHaveValue('Keep this name');expect(await page.evaluate(()=>state.vault.keys.length)).toBe(0);
});

test('generator uses wide viewport, descending scores and spaced flowing sections',async({page})=>{
 await page.setViewportSize({width:3440,height:1440});await openKeyGeneration(page);const scores=await page.locator('label.algorithm-card .algorithm-score').allTextContents();expect(scores.map(s=>Number(s.match(/(\d+) \/ 10/)[1]))).toEqual([10,9,9,8,8,8,8]);await expect(page.getByRole('radio',{name:'Ed25519',exact:true})).toBeChecked();const box=await page.locator('#modal').boundingBox();expect(box.width).toBeGreaterThan(1700);expect(box.height).toBeGreaterThan(1300);const grid=await page.locator('.algorithm-selection>.algorithm-grid').boundingBox();const criteria=await page.locator('.algorithm-criteria').first().boundingBox();expect(criteria.y-grid.y-grid.height).toBeGreaterThanOrEqual(24);const first=await page.locator('.algorithm-criteria').first().boundingBox();const second=await page.locator('.algorithm-criteria').nth(1).boundingBox();expect(second.y-first.y-first.height).toBeGreaterThanOrEqual(24);await page.getByText('Hardware, Ed448 e firme post-quantistiche',{exact:true}).click();await page.screenshot({path:'artifacts/ui-generator-ultrawide-0122.png'});await expect(page.getByRole('button',{name:'Genera nel deposito',exact:true})).toBeInViewport();
});
test('generator heading scrolls with content instead of overlapping cards',async({page})=>{
 await page.setViewportSize({width:1180,height:980});await openKeyGeneration(page);const selection=page.locator('.algorithm-selection');const before=await page.locator('.algorithm-section-title').boundingBox();await selection.evaluate(el=>el.scrollTop=240);const after=await page.locator('.algorithm-section-title').boundingBox();expect(before.y-after.y).toBeGreaterThan(200);await expect(page.getByRole('button',{name:'Genera nel deposito',exact:true})).toBeInViewport();await page.getByText('Hardware, Ed448 e firme post-quantistiche',{exact:true}).click();await page.screenshot({path:'artifacts/ui-generator-spaces-0122.png'});
});
test('bundled native profiles preserve original config and selected signature in tabs',async({page})=>{
 await page.locator('#new-profile').click();await page.locator('[data-session-type=native-ssh]').click();await page.getByLabel('Nome',{exact:true}).fill('Native fixture');await page.getByLabel('Host, alias, URL o cartella',{exact:true}).fill('fixture-alias');await page.getByLabel('File config OpenSSH (opzionale)',{exact:true}).fill('C:\\Fixture\\config');await page.getByLabel('Firma richiesta / nessun fallback',{exact:true}).selectOption('ssh-mldsa44-ed25519');await page.getByRole('button',{name:'Salva sessione',exact:true}).click();const saved=await page.evaluate(()=>fixtureState.data.profiles[0]);expect(saved.native.configFile).toBe('C:\\Fixture\\config');expect(saved.native.executable).toBe('');expect(saved.native.signature).toBe('ssh-mldsa44-ed25519');await page.locator('.profile-item').dblclick();await expect(page.locator('#tabs .tab')).toHaveCount(1);await expect(page.locator('.terminal-pane')).toBeVisible();expect(await page.evaluate(()=>testCalls.some(c=>c.action==='vaultUnlock'))).toBe(false);
});
test('RDP advanced settings save gateway and explicit redirection flags',async({page})=>{
 await openNewConnection(page,'rdp');await page.locator('[name=name]').fill('Gateway fixture');await page.locator('[name=host]').fill('server.invalid');await page.getByRole('tab',{name:'Desktop remoto',exact:true}).click();await page.getByText('Opzioni RDP avanzate / Gateway e dispositivi',{exact:true}).click();await page.getByLabel('RD Gateway / nome DNS',{exact:true}).fill('gateway.invalid');await page.getByLabel('Condividi smart card',{exact:true}).check();await page.getByLabel('Usa piu monitor (apre il client Microsoft in una finestra dedicata)',{exact:true}).check();await page.getByRole('button',{name:'Salva connessione',exact:true}).click();const p=await page.evaluate(()=>fixtureState.data.profiles[0]);expect(p.rdp.gateway).toBe('gateway.invalid');expect(p.rdp.multiMonitor).toBe(true);expect(p.rdp.redirectSmartCards).toBe(true);expect(p.rdp.redirectDrives).toBe(false);
});
test('external CA policy exposes fingerprint renewal and keeps provider arguments explicit',async({page})=>{
 await page.evaluate(()=>{fixtureState.vault.unlocked=true;fixtureState.vault.exists=true;fixtureState.vault.keys=[{id:'11111111111111111111111111111111',name:'Fixture',fingerprint:'SHA256:fixture',publicKey:'ssh-ed25519 fixture'}];update(fixtureState);});await page.evaluate(()=>externalCertificateDialog(fixtureState.vault.keys[0]));await expect(page.getByRole('button',{name:'Salva provider',exact:true})).toBeVisible();await page.getByLabel('Impronta SHA256 della CA fidata',{exact:true}).fill('SHA256:fixture');await page.getByLabel('Principal / username',{exact:true}).fill('fixture');await page.getByLabel('Rinnova automaticamente vicino alla scadenza, con app aperta e archivio sbloccato',{exact:true}).check();await page.getByRole('button',{name:'Salva provider',exact:true}).click();const provider=await page.evaluate(()=>testCalls.find(c=>c.action==='externalCertificateSave').payload.provider);expect(provider.autoRenew).toBe(true);expect(provider.executable).toBe('');expect(provider.authorityFingerprint).toBe('SHA256:fixture');
});
test('bundled native editor keeps save reachable at compact size',async({page})=>{await page.setViewportSize({width:700,height:680});await page.locator('#new-profile').click();await page.getByLabel('Cerca tipo di sessione').fill('OpenSSH nativo');await page.locator('[data-session-type=native-ssh]').click();await expect(page.getByRole('button',{name:'Salva sessione',exact:true})).toBeInViewport();expect(await page.locator('#modal').evaluate(el=>el.scrollWidth<=el.clientWidth+1)).toBe(true);await page.screenshot({path:'artifacts/ui-native-profile-020-compact.png'});});


test('native SSH selects archived hybrid identity and preserves password choice separately',async({page})=>{
 await page.evaluate(()=>{fixtureState.vault={exists:true,unlocked:true,keys:[{id:'hybrid-key',name:'Hybrid fixture',algorithm:'ssh-mldsa44-ed25519',kind:'ssh-native-key'}]};update(fixtureState)});
 await page.locator('#new-profile').click();await page.locator('[data-session-type=native-ssh]').click();await page.getByLabel('Nome',{exact:true}).fill('Hybrid fixture');await page.getByLabel('Host, alias, URL o cartella',{exact:true}).fill('test@fixture.invalid');await page.getByLabel('Autenticazione',{exact:true}).selectOption('key');await expect(page.getByLabel('Password',{exact:true})).toBeHidden();await page.getByLabel('Chiave dell’archivio',{exact:true}).selectOption('hybrid-key');await expect(page.getByLabel('Identita OpenSSH / hardware (opzionale)',{exact:true})).toBeDisabled();await page.getByRole('button',{name:'Salva sessione',exact:true}).click();const p=await page.evaluate(()=>fixtureState.data.profiles[0]);expect(p.keyId).toBe('hybrid-key');expect(p.authentication).toBe('key');expect(p.native.sharedHostKeys).toBe(true);
});
test('native password may be remembered encrypted and unrelated clients hide SSH credentials',async({page})=>{
 await page.locator('#new-profile').click();await page.locator('[data-session-type=native-ssh]').click();await page.getByLabel('Nome',{exact:true}).fill('Password fixture');await page.getByLabel('Host, alias, URL o cartella',{exact:true}).fill('user@fixture.invalid');await page.getByLabel('Autenticazione',{exact:true}).selectOption('password');await page.getByLabel('Password',{exact:true}).fill('fixture-only');await page.getByLabel('Salva password nell’archivio cifrato',{exact:true}).check();await expect(page.getByLabel('Chiave dell’archivio',{exact:true})).toBeHidden();await page.getByRole('button',{name:'Salva sessione',exact:true}).click();const p=await page.evaluate(()=>fixtureState.data.profiles[0]);expect(p.passwordId).toBe('fixture-password-id');expect(p.password).toBeUndefined();
});


test('coordinated recovery shares one request across SSH and SFTP and preserves file navigation',async({page})=>{
 await page.evaluate(async()=>{const p={id:'coordinated-fixture',name:'Coordinated fixture',protocol:'ssh',host:'192.0.2.1',port:22,username:'fixture',authentication:'password',folder:'Test'};fixtureState.data.profiles.push(p);update(fixtureState);await connect(p);await openFileWorkspace(p);const f=[...panes.values()].find(p=>p.fileWorkspace);await loadFileSide(f,'remote','/project');f.remoteFilter.value='safe';f.selection.remote.add('/safe');f.nav.remote.back=['/home/test'];f.title.textContent='Keep SFTP name';});
 const checks=page.getByRole('checkbox',{name:'Riconnessione automatica Coordinated fixture',includeHidden:true});await checks.last().check();await expect(checks.first()).toBeChecked();
 await page.evaluate(()=>{window.chrome.webview.postMessage=((original)=>m=>{if(m.action==='recoverProfile'){testCalls.push(m);window.pendingRecovery=m;return}original(m)})(window.chrome.webview.postMessage);const members=[...panes.values()];window.recoveryA=recoverProfilePanes(members[0],true);window.recoveryB=recoverProfilePanes(members[1],true)});
 expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='recoverProfile').length)).toBe(1);
 await page.evaluate(()=>fixtureEmit('reply',{id:pendingRecovery.id,result:{success:true,retryable:false,error:'',sessions:[...panes.values()].map(p=>({sessionId:p.id,connected:true,filesAvailable:true,terminalRestarted:!p.fileWorkspace}))}}));
 await page.evaluate(()=>Promise.all([recoveryA,recoveryB]));const restored=await page.evaluate(()=>{const f=[...panes.values()].find(p=>p.fileWorkspace);return{path:f.remotePath,back:f.nav.remote.back,filter:f.remoteFilter.value,selected:[...f.selection.remote],title:f.title.textContent}});expect(restored).toEqual({path:'/project',back:['/home/test'],filter:'safe',selected:['/safe'],title:'Keep SFTP name'});await expect(page.locator('#tabs .tab')).toHaveCount(2);await page.screenshot({path:'artifacts/ui-recovery-023.png',fullPage:true});
});
test('manual pause remains available while a transfer waits for network recovery',async({page})=>{
 await page.evaluate(async()=>{const p={id:'queue-fixture',name:'Queue fixture',protocol:'sftp',host:'192.0.2.1',port:22,username:'fixture',authentication:'password',folder:'Test'};fixtureState.data.profiles.push(p);const pane=await openFileWorkspace(p);fixtureQueue=[{id:'waiting-job',sessionId:pane.id,status:'Attesa connessione',name:'fixture.bin',progress:30,host:p.host,port:p.port,local:'C:\\Fixture\\fixture.bin',remote:'/fixture.bin'}];fileQueue=fixtureQueue;renderFileQueues()});
 await expect(page.getByRole('button',{name:'Riconnetti e riprendi',exact:true})).toBeVisible();await page.getByRole('button',{name:'Pausa',exact:true}).click();expect(await page.evaluate(()=>testCalls.some(c=>c.action==='filePause'&&c.payload.id==='waiting-job'))).toBe(true);
});


test('connection chooser scrolls to every session and cancel at compact height', async({page})=>{
 await page.setViewportSize({width:1100,height:700});await page.goto('https://relay.local/');await page.getByRole('button',{name:'Nuova connessione',exact:true}).first().click();const dialog=page.locator('dialog');await expect(dialog).toBeVisible();await dialog.getByRole('button',{name:'Annulla',exact:true}).scrollIntoViewIfNeeded();await expect(dialog.getByRole('button',{name:'Annulla',exact:true})).toBeInViewport();const body=await page.locator('#modal-body').evaluate(e=>({h:e.clientHeight,total:e.scrollHeight,overflow:getComputedStyle(e).overflowY}));expect(body.total).toBeGreaterThan(body.h);expect(body.overflow).toBe('auto');await page.screenshot({path:'artifacts/ui-024-picker.png'});
});
test('API and app authentication controls use separate tabs and partial verified updates',async({page})=>{
 await page.locator('#settings-btn').click();await page.getByRole('tab',{name:'Accesso',exact:true}).click();const app=page.getByLabel('Richiedi autenticazione all’apertura');await expect(app).toBeChecked();await expect(app).toBeDisabled();await page.getByRole('tab',{name:'ChatGPT',exact:true}).click();const api=page.getByLabel('Abilita API locale per ChatGPT / Codex');await expect(api).not.toBeChecked();await api.check();await page.getByRole('button',{name:'Salva API e verifica identità',exact:true}).click();expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='accessSettings').at(-1).payload)).toEqual({assistantApiEnabled:true});await page.getByRole('tab',{name:'Accesso',exact:true}).click();await expect(app).toBeChecked();await page.getByRole('button',{name:'Salva accesso e verifica identità',exact:true}).click();expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='accessSettings').at(-1).payload)).toEqual({requireAppAuthentication:true});expect(await page.evaluate(()=>state.data.settings.assistantApiEnabled)).toBe(true);
});

test('protected startup keeps console hidden until identity and final animated sequence',async({page})=>{
 await page.addInitScript(()=>{window.holdFixtureMedia=true;});await page.goto('https://relay.local/');await page.evaluate(async()=>{fixtureState.appLocked=true;fixtureState.vault.exists=true;update(await rpc('bootstrap'));});await expect(page.locator('.topbar')).toHaveCSS('visibility','hidden');await page.locator('#sequence-video').evaluate(v=>v.dispatchEvent(new Event('ended')));await expect(page.getByLabel('Password principale',{exact:true})).toHaveCount(0);await page.getByRole('button',{name:'Accedi con il tuo account',exact:true}).click();await expect(page.locator('#access-gate h1')).toHaveText('ACCESSO AUTORIZZATO');await expect(page.locator('.topbar')).toHaveCSS('visibility','hidden');await expect(page.locator('#access-gate')).toBeHidden({timeout:7000});await expect(page.locator('.topbar')).toHaveCSS('visibility','visible');expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='applicationAuthenticate').length)).toBe(1);expect(await page.evaluate(()=>testCalls.some(c=>c.action==='consoleReady'))).toBeTruthy();
});


test('music button changes only optional app soundtrack preference',async({page})=>{
 await page.goto('https://relay.local/');const button=page.getByRole('button',{name:'Musica dell’app',exact:true});await expect(button).toHaveAttribute('aria-pressed','false');await button.click();await expect(button).toHaveAttribute('aria-pressed','true');await button.click();await expect(button).toHaveAttribute('aria-pressed','false');expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='musicSet').map(c=>c.payload.enabled))).toEqual([true,false]);
});
test('long login starts fallback only after primary ends and retains track for enabled app music',async({page})=>{
 await page.addInitScript(()=>{window.holdFixtureMedia=true;});await page.goto('https://relay.local/');await page.waitForFunction(()=>typeof startupTheme!=='undefined'&&startupTheme!==null);const result=await page.evaluate(async()=>{startupTheme.dispatchEvent(new Event('ended'));const fallback=startupTheme,source=fallback.src;fixtureState.data.settings.appMusicEnabled=true;update(await rpc('bootstrap'));await fadeStartupTheme();const retained=appMusic===fallback&&startupTheme===null&&appMusic.loop;stopAppMusic();return{source,retained,kind:startupTrackKind}});expect(result.source).toContain('background-theme.m4a');expect(result.retained).toBeTruthy();expect(result.kind).toBe('fallback');
});


test('included lightweight soundtrack decodes with its full duration',async({page})=>{
 test.skip(!fs.existsSync(path.join(root,'media/background-theme.m4a')), 'Personal soundtrack is excluded from public source; UI playback uses silent fixtures.');
 await page.goto('https://relay.local/');const duration=await page.evaluate(()=>new Promise((resolve,reject)=>{const audio=new Audio();const timer=setTimeout(()=>reject(new Error('Audio metadata timeout')),10000);audio.onloadedmetadata=()=>{clearTimeout(timer);resolve(audio.duration);audio.src=''};audio.onerror=()=>{clearTimeout(timer);reject(new Error('Audio decode failed '+audio.error?.code))};audio.preload='metadata';audio.src='media/background-theme.m4a';audio.load();}));expect(duration).toBeGreaterThan(2368);expect(duration).toBeLessThan(2370);
});


test('settings tabs keep controls accessible and archive access in its category',async({page})=>{
 await page.setViewportSize({width:1100,height:720});await page.locator('#settings-btn').click();await expect(page.getByRole('tab')).toHaveCount(7);await expect(page.locator('#settings-panel-features')).toBeHidden();await page.getByRole('tab',{name:'Accesso',exact:true}).click();await expect(page.getByRole('button',{name:'Accedi con il tuo account',exact:true})).toBeVisible();await page.getByRole('button',{name:'Salva blocco automatico',exact:true}).scrollIntoViewIfNeeded();await expect(page.getByRole('button',{name:'Salva blocco automatico',exact:true})).toBeInViewport();await page.getByRole('tab',{name:'Accesso',exact:true}).focus();await page.keyboard.press('ArrowDown');await expect(page.getByRole('tab',{name:'ChatGPT',exact:true})).toBeFocused();await expect(page.locator('#settings-panel-chatgpt')).toBeVisible();await page.getByRole('tab',{name:'Audio',exact:true}).click();await page.screenshot({path:'artifacts/ui-025-settings-compact.png'});await page.setViewportSize({width:1600,height:1000});await page.screenshot({path:'artifacts/ui-025-settings.png'});
});

test('audio preview saves zero and full volume without changing sequence audio',async({page})=>{
 await page.locator('#settings-btn').click();await page.getByRole('tab',{name:'Audio',exact:true}).click();const enabled=page.getByLabel('Riproduci musica quando l’app è aperta');const slider=page.getByRole('slider',{name:'Volume della musica'});await expect(slider).toHaveValue('35');await enabled.check();await slider.fill('0');await expect(slider).toHaveAttribute('aria-valuetext','0 percento');expect(await page.evaluate(()=>appMusic.volume)).toBe(0);await page.getByRole('button',{name:'Salva audio',exact:true}).click();expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='audioSettings').at(-1).payload)).toEqual({enabled:true,volume:0,startupVolume:.8,shutdownVolume:1});await page.locator('#modal-close').click();await page.locator('#settings-btn').click();await page.getByRole('tab',{name:'Audio',exact:true}).click();await expect(slider).toHaveValue('0');await slider.fill('100');await page.getByRole('button',{name:'Salva audio',exact:true}).click();expect(await page.evaluate(()=>appMusic.volume)).toBe(1);await page.getByRole('button',{name:'Disattiva musica nell’app',exact:true}).click();expect(await page.evaluate(()=>({enabled:state.data.settings.appMusicEnabled,volume:state.data.settings.appMusicVolume,player:appMusic}))).toEqual({enabled:false,volume:1,player:null});expect(await page.evaluate(()=>testCalls.some(c=>c.action==='settings'&&('startupVolume'in c.payload||'shutdownVolume'in c.payload)))).toBe(false);
});

test('closing unsaved audio preview restores saved preference and does not affect startup player',async({page})=>{
 await page.waitForFunction(()=>!document.body.classList.contains('startup-covered'));await page.evaluate(()=>{state.data.settings.appMusicEnabled=true;state.data.settings.appMusicVolume=.42;startAppMusic();startupTheme=new Audio('media/startup-theme.mp3');startupTheme.volume=.8;});await page.locator('#settings-btn').click();await page.getByRole('tab',{name:'Audio',exact:true}).click();await page.getByRole('slider',{name:'Volume della musica'}).fill('80');expect(await page.evaluate(()=>({app:appMusic.volume,startup:startupTheme.volume}))).toEqual({app:.8,startup:.8});await page.locator('#modal-close').click();await expect.poll(()=>page.evaluate(()=>appMusic.volume)).toBe(.42);expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='audioSettings').length)).toBe(0);
});

test('archive auto lock updates preserve music preference and settings modal',async({page})=>{
 await page.locator('#settings-btn').click();await page.getByRole('tab',{name:'Audio',exact:true}).click();await page.getByRole('slider',{name:'Volume della musica'}).fill('65');await page.getByRole('button',{name:'Salva audio',exact:true}).click();await page.getByRole('tab',{name:'Accesso',exact:true}).click();await page.locator('[name=autoLockMinutes]').fill('25');await page.getByRole('button',{name:'Salva blocco automatico',exact:true}).click();await expect(page.locator('#modal-title')).toHaveText('Impostazioni');expect(await page.evaluate(()=>state.data.settings.appMusicVolume)).toBe(.65);await page.getByRole('button',{name:'Accedi con il tuo account',exact:true}).click();await page.getByRole('button',{name:'Abilita Windows Hello su questo PC',exact:true}).click();await expect(page.locator('#modal-title')).toHaveText('Impostazioni');await expect(page.getByRole('button',{name:'Disabilita Windows Hello su questo PC',exact:true})).toBeVisible();await page.locator('#modal-close').click();await page.locator('#keys-btn').click();await expect(page.locator('#modal')).not.toHaveClass(/settings-modal/);
});

test('audio settings display music rights disclaimer',async({page})=>{await page.locator('#settings-btn').click();await page.getByRole('tab',{name:'Audio',exact:true}).click();await expect(page.getByRole('heading',{name:'Musica e diritti',exact:true})).toBeVisible();await expect(page.locator('#settings-panel-audio')).toContainText('non affiliato né approvato da Microsoft o Halo Studios');});

test('three audio sliders save independently and music disable preserves sequence levels',async({page})=>{
 await page.locator('#settings-btn').click();await page.getByRole('tab',{name:'Audio',exact:true}).click();const intro=page.getByRole('slider',{name:'Volume intro e accesso',exact:true}),outro=page.getByRole('slider',{name:'Volume outro',exact:true}),music=page.getByRole('slider',{name:'Volume della musica',exact:true});await intro.fill('20');await outro.fill('70');await music.fill('45');await page.getByRole('button',{name:'Salva audio',exact:true}).click();expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='audioSettings').at(-1).payload)).toEqual({enabled:false,volume:.45,startupVolume:.2,shutdownVolume:.7});await intro.fill('55');await page.getByRole('button',{name:'Disattiva musica nell’app',exact:true}).click();expect(await page.evaluate(()=>state.data.settings.startupVolume)).toBe(.2);await page.locator('#modal-close').click();await page.locator('#settings-btn').click();await page.getByRole('tab',{name:'Audio',exact:true}).click();await expect(intro).toHaveValue('20');await expect(outro).toHaveValue('70');await expect(music).toHaveValue('45');await page.setViewportSize({width:1600,height:1100});await page.screenshot({path:'artifacts/ui-026-audio.png'});
});

test('startup primary fallback and shutdown use distinct saved sequence volumes including zero',async({page})=>{
 await page.waitForFunction(()=>!document.body.classList.contains('startup-covered'));const result=await page.evaluate(()=>{window.holdFixtureMedia=true;state.data.settings.startupVolume=.19;state.data.settings.shutdownVolume=0;startupEnding=false;playSequence('startup',sequenceVolume('startup'),()=>{});const primary=startupTheme.volume;startupTheme.dispatchEvent(new Event('ended'));const fallback=startupTheme.volume;startupEnding=true;startupTheme.pause();startupTheme=null;playSequence('shutdown',sequenceVolume('shutdown'),()=>{});return{primary,fallback,outro:document.getElementById('sequence-video').volume}});expect(result).toEqual({primary:.19,fallback:.19,outro:0});await expect(page.locator('#sequence')).toBeVisible();
});

test('snippet library filters content and rejects conflicting shortcuts before saving',async({page})=>{
 await page.locator('#snippets-btn').click();for(const [name,text]of [['Check uptime','uptime'],['List files','ls -la']]){await page.getByRole('button',{name:'Nuovo snippet',exact:true}).click();await page.locator('[name=name]').fill(name);await page.locator('[name=text]').fill(text);await page.locator('[name=shortcut]').selectOption(name==='Check uptime'?'Ctrl+Alt+Shift+U':'');await page.getByRole('button',{name:'Salva',exact:true}).click()}await page.getByRole('searchbox',{name:'Cerca snippet',exact:true}).fill('uptime');await expect(page.locator('.snippet-item')).toHaveCount(1);await expect(page.locator('.snippet-detail')).toContainText('Check uptime');await page.getByRole('button',{name:'Nuovo snippet',exact:true}).click();await page.locator('[name=name]').fill('Conflict');await page.locator('[name=text]').fill('pwd');await page.locator('[name=shortcut]').selectOption('Ctrl+Alt+Shift+U');await page.getByRole('button',{name:'Salva',exact:true}).click();await expect(page.getByRole('alert')).toContainText('Check uptime');expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='saveSnippet').length)).toBe(2);await page.getByRole('button',{name:'Annulla',exact:true}).click();await page.setViewportSize({width:1550,height:1000});await page.screenshot({path:'artifacts/ui-026-snippets.png'});
});

test('snippet shortcut opens preview and inserts only into the captured terminal',async({page})=>{
 await page.waitForFunction(()=>!document.body.classList.contains('startup-covered'));
 const ids=await page.evaluate(async()=>{fixtureState.vault.unlocked=true;state.vault.unlocked=true;const make=id=>({id,name:id,protocol:'local',localShell:'cmd',folder:'Local',host:'',port:22,username:'',environment:'test'});for(const id of ['first-terminal','second-terminal']){const p=make(id);state.data.profiles.push(p);await connect(p)}state.data.snippets=[{id:'snippet-shortcut',name:'Echo variable',folder:'Ops',text:'echo {{value}}',shortcut:'Ctrl+Alt+Shift+E'}];const ids=[...panes.keys()];activeId=ids[0];refreshStage();return ids});await page.locator('body').click({position:{x:500,y:100}});await page.keyboard.press('Control+Alt+Shift+E');await expect(page.locator('#modal-title')).toHaveText('Inserisci Echo variable');expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='input').length)).toBe(0);await page.getByLabel('value',{exact:true}).fill('fixture');await expect(page.getByLabel('Anteprima del comando',{exact:true})).toHaveText('echo fixture');await page.evaluate(id=>{activeId=id},ids[1]);await page.getByRole('button',{name:'Inserisci nel terminale',exact:true}).click();expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='input').at(-1).payload)).toEqual({sessionId:ids[0],data:'echo fixture'});
});

test('palette finds snippets by command text and keyboard opens preview without sending',async({page})=>{
 await page.evaluate(async()=>{const p={id:'palette-local',name:'Local fixture',protocol:'local',localShell:'cmd',folder:'Local',host:'',port:22,username:'',environment:'test'};state.data.profiles.push(p);await connect(p);state.data.snippets=[{id:'snippet-palette',name:'Inspect host',folder:'Ops',text:'hostname',shortcut:''}]});await page.keyboard.press('Control+k');await page.getByRole('searchbox',{name:'Cerca comandi, connessioni o snippet',exact:true}).fill('hostname');await expect(page.locator('.palette-list button')).toHaveCount(1);await page.keyboard.press('Enter');await expect(page.locator('#modal-title')).toHaveText('Inserisci Inspect host');expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='input').length)).toBe(0);await page.getByRole('button',{name:'Annulla',exact:true}).click();
});

test('snippet shortcut ignores repeat and ordinary editable fields',async({page})=>{
 await page.evaluate(()=>{state.data.snippets=[{id:'guarded',name:'Guarded snippet',folder:'Ops',text:'pwd',shortcut:'Ctrl+Alt+Shift+G'}]});const field=page.locator('#profile-search');await field.fill('');await field.focus();await page.keyboard.press('Control+Alt+Shift+G');await expect(page.locator('#modal')).not.toBeVisible();expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='input').length)).toBe(0);await page.evaluate(()=>document.dispatchEvent(new KeyboardEvent('keydown',{code:'KeyG',key:'G',ctrlKey:true,altKey:true,shiftKey:true,repeat:true,bubbles:true})));await expect(page.locator('#modal')).not.toBeVisible();
});

test('snippet library and editor keep actions reachable in a compact window',async({page})=>{
 await page.setViewportSize({width:1000,height:700});await page.locator('#snippets-btn').click();await page.getByRole('button',{name:'Nuovo snippet',exact:true}).click();await page.locator('[name=name]').fill('Compact fixture');await page.locator('[name=text]').fill('echo fixture');await page.getByRole('button',{name:'Salva',exact:true}).scrollIntoViewIfNeeded();await expect(page.getByRole('button',{name:'Salva',exact:true})).toBeInViewport();await page.getByRole('button',{name:'Salva',exact:true}).click();await page.getByRole('button',{name:'Inserisci',exact:true}).scrollIntoViewIfNeeded();await expect(page.getByRole('button',{name:'Inserisci',exact:true})).toBeInViewport();await page.screenshot({path:'artifacts/ui-026-snippets-compact.png'});
});

test('startup macro profile tab saves a pinned snippet with variables and explicit launch policy',async({page})=>{
 await page.evaluate(()=>{fixtureState.data.snippets=[{id:'macro-source',name:'Inspect service',folder:'Ops',text:'systemctl status {{service}}',shortcut:''}];update(fixtureState);profileDialog({id:'macro-profile',name:'Macro fixture',folder:'Connessioni',protocol:'ssh',host:'192.0.2.1',port:22,username:'fixture',authentication:'password',remoteDirectory:'.',notes:'',tunnels:[],hooks:[]})});await page.getByRole('tab',{name:'Macro di avvio',exact:true}).click();await page.getByLabel('Snippet da usare come macro',{exact:true}).selectOption('macro-source');await page.getByLabel('Esegui una macro all’apertura della sessione',{exact:true}).check();await page.getByLabel('service',{exact:true}).fill('fixture-service');await expect(page.getByLabel('Anteprima macro di avvio',{exact:true})).toHaveText('systemctl status fixture-service');await expect(page.getByLabel('Chiedi conferma a ogni apertura',{exact:true})).toBeChecked();await page.getByRole('button',{name:'Salva connessione',exact:true}).click();const saved=await page.evaluate(()=>testCalls.find(c=>c.action==='saveProfile').payload);expect(saved.startupMacro).toEqual({snippetId:'macro-source',name:'Inspect service',text:'systemctl status {{service}}',variables:{service:'fixture-service'},enabled:true,confirmEachLaunch:true});
 await page.evaluate(()=>{state.data.snippets[0].text='DIFFERENT COMMAND';profileDialog(state.data.profiles.find(p=>p.id==='macro-profile'))});await page.getByRole('tab',{name:'Macro di avvio',exact:true}).click();await expect(page.getByLabel('Anteprima macro di avvio',{exact:true})).toHaveText('systemctl status fixture-service');await page.getByRole('button',{name:'Aggiorna copia dalla libreria',exact:true}).click();await expect(page.getByLabel('Anteprima macro di avvio',{exact:true})).toHaveText('DIFFERENT COMMAND');await page.setViewportSize({width:1500,height:950});await page.screenshot({path:'artifacts/ui-027-macro.png'});
});

test('macro without a selected snippet cannot be enabled and local profiles expose macro tab',async({page})=>{
 await page.evaluate(()=>profileDialog({id:'local-macro',name:'Local macro',folder:'Connessioni',protocol:'local',localShell:'cmd',startupCommands:'echo before',host:'',port:22,username:'',authentication:'auto',notes:'',hooks:[],tunnels:[]}));await page.getByRole('tab',{name:'Macro di avvio',exact:true}).click();await page.getByLabel('Esegui una macro all’apertura della sessione',{exact:true}).check();await page.getByRole('button',{name:'Salva connessione',exact:true}).click();expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='saveProfile').length)).toBe(0);await expect(page.locator('#toasts')).toContainText('Seleziona uno snippet');
});

test('macro settings stay hidden for SFTP and desktop sessions',async({page})=>{
 await page.evaluate(()=>profileDialog({id:'sftp-only',name:'Files',folder:'Connessioni',protocol:'sftp',host:'192.0.2.1',port:22,username:'fixture',authentication:'password',remoteDirectory:'.',notes:'',tunnels:[],hooks:[]}));await expect(page.getByRole('tab',{name:'Macro di avvio',exact:true})).toHaveCount(0);await page.locator('#modal-close').click();await page.evaluate(()=>profileDialog({id:'rdp-only',name:'Desktop',folder:'Connessioni',protocol:'rdp',host:'192.0.2.1',port:3389,username:'fixture',authentication:'password',remoteDirectory:'.',notes:'',tunnels:[],hooks:[]}));await expect(page.getByRole('tab',{name:'Macro di avvio',exact:true})).toHaveCount(0);
});

test('client key material is masked until explicit reveal and private copy uses a dedicated action',async({page})=>{
 await page.evaluate(()=>{fixtureState.vault={exists:true,unlocked:true,keys:[{id:'material',name:'Material fixture',algorithm:'ssh-ed25519',bits:256,created:new Date().toISOString(),publicKey:'ssh-ed25519 PUBLIC-FIXTURE',comment:''}]};update(fixtureState)});await page.locator('#keys-btn').click();const pub=page.getByLabel('Chiave pubblica selezionata'),priv=page.getByLabel('Chiave privata selezionata');await expect(pub).toHaveValue(/••/);await expect(priv).toHaveValue(/••/);expect(await page.evaluate(()=>testCalls.some(c=>c.action==='vaultRevealPrivate'))).toBe(false);await page.getByRole('button',{name:'Mostra pubblica',exact:true}).click();await expect(pub).toHaveValue('ssh-ed25519 PUBLIC-FIXTURE');await page.getByRole('button',{name:'Mostra privata',exact:true}).click();await expect(priv).toHaveValue(/FIXTURE-ONLY/);await page.getByRole('button',{name:'Oscura privata',exact:true}).click();await expect(priv).toHaveValue(/••/);await page.getByRole('button',{name:'Copia privata',exact:true}).click();expect(await page.evaluate(()=>testCalls.at(-1).action)).toBe('vaultCopyPrivate');await page.setViewportSize({width:1920,height:1080});await page.screenshot({path:'artifacts/ui-028-keys.png'});
});
test('integrated login waits for explicit input and keeps console inaccessible',async({page})=>{
 await page.addInitScript(()=>{window.holdFixtureMedia=true});await page.goto('https://relay.local/');await page.evaluate(async()=>{fixtureState.appLocked=true;fixtureState.vault={exists:true,unlocked:false,windowsHello:true,keys:[]};update(await rpc('bootstrap'))});await page.locator('#sequence-video').evaluate(v=>v.dispatchEvent(new Event('ended')));await expect(page.getByRole('button',{name:'Accedi con Windows Hello',exact:true})).toBeVisible();expect(await page.evaluate(()=>testCalls.some(c=>c.action==='applicationAuthenticate'))).toBe(false);await expect(page.locator('.topbar')).toHaveCSS('visibility','hidden');await page.setViewportSize({width:1920,height:1080});await page.screenshot({path:'artifacts/ui-028-login.png'});await page.getByRole('button',{name:'Accedi con Windows Hello',exact:true}).click();await expect(page.locator('#access-gate h1')).toHaveText('ACCESSO AUTORIZZATO');expect(await page.evaluate(()=>testCalls.find(c=>c.action==='applicationAuthenticate').payload)).toEqual({method:'hello'});
});

test('external editor preference is selectable resettable and preserved with general settings',async({page})=>{await page.locator('#settings-btn').click();await page.getByRole('tab',{name:'Editor',exact:true}).click();await expect(page.getByLabel('Programma editor esterno')).toHaveValue('Blocco note di Windows');await page.getByRole('button',{name:'Scegli programma EXE',exact:true}).click();await expect(page.getByLabel('Programma editor esterno')).toHaveValue('C:\\Fixture\\editor.exe');await page.getByRole('tab',{name:'Accesso',exact:true}).click();await page.getByRole('button',{name:'Salva blocco automatico',exact:true}).click();expect(await page.evaluate(()=>state.data.settings.externalEditor)).toBe('C:\\Fixture\\editor.exe');await page.getByRole('tab',{name:'Editor',exact:true}).click();await page.getByRole('button',{name:'Ripristina Blocco note',exact:true}).click();await expect(page.getByLabel('Programma editor esterno')).toHaveValue('Blocco note di Windows');});

test('SSH history arrows keep reaching the terminal after tab changes and repeated repaint',async({page})=>{await createConnection(page,'History fixture','ssh');await page.locator('.profile-item').dblclick();const id=await page.evaluate(()=>activeId);const input=page.locator('.terminal-pane .xterm-helper-textarea');await input.focus();await page.keyboard.press('ArrowUp');await page.keyboard.press('ArrowDown');expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='input').slice(-2).map(c=>c.payload.data))).toEqual(['\u001b[A','\u001b[B']);await page.evaluate(()=>{const p=panes.get(activeId);for(let i=0;i<100;i++)p.term.write('\r\u001b[2Kprompt> '+('long command '.repeat(15))+i);refreshStage();});await page.locator('#home-page').click();await page.locator('#tabs .tab').click();await input.focus();await page.keyboard.press('ArrowUp');expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='input').at(-1).payload)).toEqual({sessionId:id,data:'\u001b[A'});await page.waitForTimeout(100);const content=await page.evaluate(()=>{const t=panes.get(activeId).term;return Array.from({length:t.buffer.active.length},(_,i)=>t.buffer.active.getLine(i)?.translateToString()||'').join('\n')});expect(content).toContain('prompt>');await expect(page.locator('.terminal-pane')).toBeVisible();});

test('key manager exposes separate private and public export without revealing key text',async({page})=>{await page.evaluate(()=>{fixtureState.vault={exists:true,unlocked:true,keys:[{id:'export-key',name:'Export fixture',algorithm:'ssh-ed25519',bits:256,created:new Date().toISOString(),publicKey:'ssh-ed25519 EXPORT-PUBLIC',comment:''}]};update(fixtureState)});await page.locator('#keys-btn').click();await page.getByRole('button',{name:'Esporta chiave privata',exact:true}).click();expect(await page.evaluate(()=>testCalls.at(-1))).toEqual({action:'vaultExportPrivate',payload:{keyId:'export-key'}});await page.getByRole('button',{name:'Esporta chiave pubblica (.pub)',exact:true}).click();expect(await page.evaluate(()=>testCalls.at(-1))).toEqual({action:'vaultExportPublic',payload:{keyId:'export-key'}});await expect(page.getByLabel('Chiave privata selezionata')).toHaveValue(/••/);await expect(page.getByLabel('Chiave pubblica selezionata')).toHaveValue(/••/);expect(await page.evaluate(()=>testCalls.some(c=>c.action==='vaultRevealPrivate'))).toBe(false);});


test('update panel separates controls and notes and shows live download ETA',async({page})=>{
 await page.locator('#updates-btn').click();await expect(page.locator('.updates-modal')).toBeVisible();
 await expect(page.locator('.update-control')).toBeVisible();await expect(page.locator('.update-release')).toBeVisible();
 const box=await page.locator('#modal').boundingBox();expect(box.width).toBeGreaterThan(1100);
 await page.getByRole('button',{name:'Controlla aggiornamenti',exact:true}).click();await expect(page.locator('.update-release pre')).toHaveText('<script>test</script>');
 await page.evaluate(()=>fixtureEmit('updateProgress',{stage:'download',status:'Download dell’aggiornamento…',receivedBytes:52428800,totalBytes:104857600,bytesPerSecond:1048576,remainingSeconds:50}));
 await expect(page.locator('#update-meter')).toHaveJSProperty('value',50);await expect(page.locator('#update-download-details')).toContainText('50 s al termine del download');
 await page.evaluate(()=>fixtureEmit('updateProgress',{stage:'download',status:'Download',receivedBytes:5000,totalBytes:null,bytesPerSecond:0,remainingSeconds:null}));
 await expect(page.locator('#update-meter')).not.toHaveAttribute('value');await expect(page.locator('#update-download-details')).toContainText('calcolo');
 await page.evaluate(()=>fixtureEmit('updateProgress',{stage:'verify',status:'Verifica del pacchetto…',receivedBytes:104857600,totalBytes:104857600}));
 await expect(page.locator('#update-meter')).toHaveJSProperty('value',100);await expect(page.locator('#update-download-details')).toContainText('Download completato');
 await page.screenshot({path:'artifacts/ui-029-updates.png'});
 await page.locator('#modal-close').click();await page.locator('#settings-btn').click();await expect(page.locator('#modal')).not.toHaveClass(/updates-modal/);
});

test('animated login stays within viewport with no scrolling and capitalized Comandante',async({page})=>{
 await page.addInitScript(()=>{window.holdFixtureMedia=true});await page.goto('https://relay.local/');await page.evaluate(async()=>{fixtureState.appLocked=true;fixtureState.vault={exists:true,unlocked:false,windowsHello:true,keys:[]};update(await rpc('bootstrap'))});await page.locator('#sequence-video').evaluate(v=>v.dispatchEvent(new Event('ended')));
 await expect(page.getByRole('heading',{name:'Bentornato, Comandante.'})).toBeVisible();
 const scene=await page.locator('.access-scene').boundingBox();const screen=page.viewportSize();expect(scene.x).toBe(0);expect(scene.y).toBe(0);expect(scene.width).toBe(screen.width);expect(scene.height).toBe(screen.height);
 for(const viewport of [{width:3440,height:1440},{width:1920,height:1080},{width:1366,height:768},{width:800,height:600}]){
  await page.setViewportSize(viewport);await page.waitForTimeout(350);
  const metrics=await page.evaluate(()=>{const gate=document.querySelector('#access-gate');return {htmlX:document.documentElement.scrollWidth>innerWidth,htmlY:document.documentElement.scrollHeight>innerHeight,overflow:getComputedStyle(gate).overflow,buttons:[...gate.querySelectorAll('button,input')].filter(e=>e.checkVisibility()).map(e=>{const r=e.getBoundingClientRect();return r.top>=0&&r.bottom<=innerHeight&&r.left>=0&&r.right<=innerWidth})}});
  expect(metrics.htmlX).toBe(false);expect(metrics.htmlY).toBe(false);expect(metrics.overflow).toBe('clip');expect(metrics.buttons.every(Boolean),JSON.stringify({viewport,...metrics})).toBe(true);
  await page.mouse.wheel(500,500);await page.evaluate(()=>window.scrollTo(1000,1000));expect(await page.evaluate(()=>[scrollX,scrollY])).toEqual([0,0]);
 }
 await page.setViewportSize({width:1920,height:1080});await page.screenshot({path:'artifacts/ui-029-login.png'});
});


test('editor settings recommend Notepad++ with official local logo and explicit automatic detection',async({page})=>{
 await page.locator('#settings-btn').click();await page.getByRole('tab',{name:'Editor',exact:true}).click();const card=page.locator('.editor-recommendation');await expect(card).toBeVisible();await expect(card.getByRole('img',{name:'Logo Notepad++'})).toBeVisible();
 expect(await card.locator('img').evaluate(img=>img.complete&&img.naturalWidth>0)).toBe(true);await expect(card.locator('a')).toHaveAttribute('href','https://notepad-plus-plus.org/');
 await card.getByRole('button',{name:'Rileva e usa Notepad++',exact:true}).click();await expect(page.getByLabel('Programma editor esterno')).toHaveValue('C:\\Fixture\\Notepad++\\notepad++.exe');await expect(page.locator('#settings-panel-editor')).toContainText('assegnato automaticamente');
 expect(await page.evaluate(()=>testCalls.some(c=>c.action==='openNotepadWebsite'))).toBe(false);await card.locator('a').click();expect(await page.evaluate(()=>testCalls.some(c=>c.action==='openNotepadWebsite'))).toBe(true);await page.screenshot({path:'artifacts/ui-0210-editor.png'});
});

 test('intro and outro video cover the entire window on ultrawide and 4K',async({page})=>{
 await page.evaluate(()=>{window.holdFixtureMedia=true;document.querySelector('#sequence').hidden=false;});
 for(const viewport of [{width:3440,height:1440},{width:3840,height:2160},{width:1920,height:1080},{width:1366,height:768}]){
 await page.setViewportSize(viewport);
 const metrics=await page.locator('#sequence-video').evaluate(v=>{const r=v.getBoundingClientRect();return {x:r.x,y:r.y,width:r.width,height:r.height,fit:getComputedStyle(v).objectFit};});
 expect(metrics).toEqual({x:0,y:0,width:viewport.width,height:viewport.height,fit:'cover'});
 }
 });


test('SFTP breadcrumbs use CSS chevrons and preserve Unicode path segments',async({page})=>{
 await createConnection(page,'Metadata fixture','sftp');await page.locator('.profile-item').dblclick();
 await page.evaluate(()=>{const p=[...panes.values()].find(p=>p.fileWorkspace);commitFileNavigation(p,'remote','/home/example-user/Caffè 日本語');});
 const crumbs=page.locator('[data-file-side=remote] .file-breadcrumbs');await expect(crumbs).toContainText('Caffè 日本語');
 const separator=await crumbs.locator('button').nth(1).evaluate(el=>getComputedStyle(el,'::before').content);expect(separator).toBe('""');
 expect(await crumbs.textContent()).not.toMatch(/Ã|â|Â/);
});
test('SFTP double click opens the configured external editor with explicit upload controls',async({page})=>{
 await createConnection(page,'External editor','sftp');await page.locator('.profile-item').dblclick();await page.locator('[data-file-side=remote] .workspace-file-row').dblclick();
 await expect.poll(()=>page.evaluate(()=>testCalls.filter(c=>c.action==='editorExternal').length)).toBe(1);await expect(page.locator('.remote-editor')).toBeHidden();await expect(page.locator('.external-editor-notice')).toBeVisible();await expect(page.getByRole('button',{name:'Carica salvataggio esterno',exact:true})).toBeVisible();
 await page.getByRole('button',{name:'Mostra editor integrato',exact:true}).click();await expect(page.locator('.remote-editor')).toBeVisible();
});
test('SFTP metadata columns resize with pointer and keyboard and survive reopening',async({page})=>{
 await page.evaluate(()=>{window.fixtureRemoteFiles=[{name:'script.sh',path:'/script.sh',directory:false,size:42,modified:'2026-11-15',permissions:'-rwxr-x---',owner:1001,group:1002}];});
 await createConnection(page,'Columns','sftp');await page.locator('.profile-item').dblclick();const remote=page.locator('[data-file-side=remote]');
 await expect(remote.locator('.workspace-file-head .file-column-label')).toHaveText(['Nome','Dimensione','Tipo','Data modifica','Permessi','Proprietario','Gruppo']);await expect(remote.locator('.workspace-file-row [data-column=permissions]')).toHaveText('-rwxr-x---');await expect(remote.locator('.workspace-file-row [data-column=owner]')).toHaveText('1001');await expect(remote.locator('.file-glyph[data-kind=code]')).toHaveCount(1);
 const grip=remote.getByRole('separator',{name:'Larghezza Nome remota'});await grip.focus();await grip.press('ArrowRight');await expect(grip).toHaveAttribute('aria-valuenow','330');
 const bounds=await grip.boundingBox();await page.mouse.move(bounds.x+bounds.width/2,bounds.y+bounds.height/2);await page.mouse.down();await page.mouse.move(bounds.x+bounds.width/2+55,bounds.y+bounds.height/2);await page.mouse.up();await expect(grip).toHaveAttribute('aria-valuenow','385');
 expect(await page.evaluate(()=>JSON.parse(localStorage.getItem('relay.fileColumns.v1.remote')).name)).toBe(385);
 await page.reload();await page.evaluate(()=>{const p={id:'reopened-columns',name:'Reopened columns',protocol:'sftp',host:'127.0.0.1',port:22,username:'fixture'};fixtureState.data.profiles.push(p);return openFileWorkspace(p)});await expect(page.getByRole('separator',{name:'Larghezza Nome remota'})).toHaveAttribute('aria-valuenow','385');
 const column=page.locator('[data-file-side=remote]');await column.locator('.workspace-file-list').evaluate(el=>el.scrollLeft=500);await expect(column.locator('.workspace-file-head [data-column=group]')).toBeVisible();
 await page.screenshot({path:'artifacts/ui-sftp-columns-0211.png'});
});

test('first account activation gates the console and uses the native browser login action',async({page})=>{
 await page.addInitScript(()=>{window.holdFixtureMedia=true});await page.goto('https://relay.local/');
 await page.evaluate(()=>{fixtureState.account={requiresLink:true,linked:false,ready:false};update(fixtureState)});
 await page.locator('#sequence-video').evaluate(v=>v.dispatchEvent(new Event('ended')));
 await expect(page.getByRole('heading',{name:'La tua console, su tutti i dispositivi.'})).toBeVisible();
 await expect(page.locator('.access-scene img')).toBeVisible();
 await page.getByRole('button',{name:'Accedi al tuo account'}).click();await page.waitForFunction(()=>!document.querySelector('#access-gate'));
 expect(await page.evaluate(()=>testCalls.filter(c=>c.action==='accountLogin').length)).toBe(1);
 expect(await page.evaluate(()=>state.account.linked)).toBe(true);
});
test('sync settings expose offline status and explicit conflict choices without credentials',async({page})=>{
 await page.evaluate(()=>{state.account={requiresLink:false,linked:true,name:'Fixture User',ready:true,conflict:true,revision:3,message:'Due copie modificate'};});
 await page.locator('#settings-btn').click();await page.getByRole('tab',{name:'Account e sincronizzazione'}).click();
 await expect(page.locator('#settings-panel-account')).toContainText('Fixture User');
 await expect(page.getByRole('button',{name:'Usa la copia online su questo PC'})).toBeVisible();
 await expect(page.getByRole('button',{name:'Usa la copia di questo PC online'})).toBeVisible();
 await expect(page.locator('#settings-panel-account input[type=password]')).toHaveCount(0);
 await page.setViewportSize({width:1920,height:1080});await page.screenshot({path:'artifacts/ui-0212-account-sync.png'});
});


test('account profile stays in Relay and logout removes all old session views',async({page})=>{
 await page.evaluate(()=>{fixtureState.account={requiresLink:false,linked:true,ready:true,name:'Fixture User'};update(fixtureState)});
 await page.locator('#account-btn').click();await expect(page.getByRole('heading',{name:'Il tuo account',exact:true})).toBeVisible();await expect(page.locator('.account-profile-details')).toContainText('fixture@example.com');
 await page.getByRole('button',{name:'Gestisci password',exact:true}).click();expect(await page.evaluate(()=>testCalls.some(v=>v.action==='accountSecurity'&&v.payload.action==='password'))).toBe(true);
 await page.getByRole('button',{name:'Esci dall’account',exact:true}).click();await expect(page.getByRole('heading',{name:'La tua console, su tutti i dispositivi.'})).toBeVisible();await expect(page.locator('.access-scene img')).toBeVisible();await expect(page.getByRole('button',{name:'Crea account',exact:true})).toBeDisabled();await expect(page.locator('#tabs').locator('[data-session-id]')).toHaveCount(0);
});


test('shutdown from account access removes the gate and stops intro music before the outro',async({page})=>{
 await page.evaluate(()=>{window.holdFixtureMedia=true;const gate=document.createElement('div');gate.id='access-gate';gate.className='access-login';document.body.append(gate);startStartupTheme();window.fixtureEmit('shutdown',{enabled:true,volume:.4})});
 await expect(page.locator('#access-gate')).toHaveCount(0);await expect(page.locator('#sequence')).toBeVisible();
 expect(await page.evaluate(()=>startupTheme===null&&startupEnding)).toBe(true);
 expect(await page.locator('#sequence-video').evaluate(v=>v.src.endsWith('/shutdown.mp4'))).toBe(true);
});


test('updates can be opened before account activation and before local unlock without authenticating',async({page})=>{
 for(const method of ['account','local']){
  await page.evaluate(method=>{state.appLocked=true;state.account={requiresLink:method==='account'};state.vault.windowsHello=false;method==='account'?waitForAccountActivation():waitForApplicationLogin()},method);
  await page.getByRole('button',{name:'Aggiornamenti',exact:true}).click();
  await expect(page.locator('#modal-title')).toHaveText('Aggiornamenti');await expect(page.locator('#access-gate')).toHaveCount(1);
  await page.getByRole('button',{name:'Controlla aggiornamenti',exact:true}).click();await expect(page.locator('#update-progress')).toContainText('Disponibile');
  expect(await page.evaluate(()=>testCalls.some(c=>['accountLogin','applicationAuthenticate'].includes(c.action)))).toBe(false);
  await page.locator('#modal-close').click();await expect(page.locator('#access-gate')).toBeVisible();
  await page.evaluate(()=>document.getElementById('access-gate').remove());
 }
});

for(const hello of [false,true])test(`account login exposes Hello only with manual consent: ${hello}`,async({page})=>{
 await page.evaluate(hello=>{state.appLocked=true;state.vault={exists:true,unlocked:false,windowsHello:hello,keys:[]};waitForApplicationLogin();},hello);
 await expect(page.getByRole('button',{name:'Accedi con il tuo account',exact:true})).toBeVisible();
 await expect(page.getByLabel('Password principale',{exact:true})).toHaveCount(0);
 await expect(page.getByRole('button',{name:'Accedi con Windows Hello',exact:true})).toHaveCount(hello?1:0);
 await expect(page.getByRole('button',{name:'Aggiornamenti',exact:true})).toBeVisible();
 if(!hello)await expect(page.locator('.access-footnote')).toContainText('serve una connessione');
});
test('manual Hello settings explain shared Windows identity and do not request legacy password',async({page})=>{
 await page.evaluate(()=>{state.vault={exists:true,unlocked:true,windowsHello:false,keys:[]};settingsDialog('access');});
 await expect(page.getByRole('button',{name:'Abilita Windows Hello su questo PC',exact:true})).toBeVisible();
 await expect(page.locator('#modal')).toContainText('Account Windows condiviso?');
 await expect(page.getByLabel('Password principale',{exact:true})).toHaveCount(0);
});
