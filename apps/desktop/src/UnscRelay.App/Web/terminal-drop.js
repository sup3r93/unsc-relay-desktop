"use strict";
function quoteTerminalPath(path) {
  if (/[\x00-\x1f\x7f]/.test(path)) throw Error("Il percorso contiene caratteri di controllo.");
  return "'" + path.replaceAll("'", "'\\''") + "'";
}
function wireTerminalDrop(pane, surface) {
  surface.addEventListener("dragover", (e) => {
    if ([...e.dataTransfer.types].some((t) => ["Files", "application/x-relay-file"].includes(t))) {
      e.preventDefault();
      surface.classList.add("terminal-drop-target");
    }
  });
  surface.addEventListener("dragleave", () => surface.classList.remove("terminal-drop-target"));
  surface.addEventListener("drop", async (e) => {
    if (!e.dataTransfer.files.length && !e.dataTransfer.types.includes("application/x-relay-file")) return;
    e.preventDefault();
    e.stopPropagation();
    surface.classList.remove("terminal-drop-target");
    if (!pane.connected) return toast(uiText("Connetti il terminale prima di trascinare file."), true);
    try {
      let paths = [], remote = false;
      if (e.dataTransfer.files.length) paths = await droppedPaths([...e.dataTransfer.files]);
      else {
        const source = JSON.parse(e.dataTransfer.getData("application/x-relay-file"));
        const owner = panes.get(source.pane);
        if (!owner?.fileWorkspace || owner.profile.id !== pane.profile.id || owner.profile.host !== pane.profile.host || owner.profile.port !== pane.profile.port || owner.profile.username !== pane.profile.username) throw Error("Usa file della stessa connessione.");
        remote = source.side === "remote";
        paths = selectedFiles(owner, source.side).map((f) => f.path);
      }
      if (paths.length) terminalDropDialog(pane, paths, remote);
    } catch (error) {
      toast(error.message, true);
    }
  });
}
function terminalDropDialog(pane, paths, remote = false) {
  const body = showModal(uiText("File nel terminale"), "SSH / SCEGLI L’AZIONE");
  notice(body, paths.length + " elementi · " + pane.profile.name + " / " + pane.profile.host);
  const list = document.createElement("pre");
  list.className = "drop-path-list";
  list.textContent = paths.join("\n");
  body.append(list);
  notice(body, uiText("I percorsi vengono quotati e inseriti senza Invio finale. Il terminale può trovarsi in un programma interattivo: controlla il contenuto prima di inviarlo."));
  const actions = document.createElement("div");
  actions.className = "form-actions";
  actions.append(localizedButton("Annulla", () => modal.close()), button(remote ? "Inserisci percorsi remoti" : "Inserisci percorsi locali", async () => {
    if (!pane.connected) throw Error("Terminale disconnesso.");
    const text = paths.map(quoteTerminalPath).join(" ");
    await call("input", { sessionId: pane.id, data: text });
    modal.close();
    activeId = pane.id;
    refreshStage();
    pane.term.focus();
  }));
  if (!remote) {
    const destination = field(body, uiText("Cartella remota di destinazione"), "terminalDropDestination", pane.cwd || pane.profile.remoteDirectory || ".");
    notice(body, uiText("Per il caricamento si apre la tab SFTP. Prima dei trasferimenti vedrai eventuali conflitti con file esistenti."));
    actions.append(localizedButton("Carica tramite SFTP", async () => {
      const target = destination.value.trim();
      if (!target) return toast(uiText("Indica la cartella remota."), true);
      modal.close();
      const files = await openFileWorkspace(pane.profile);
      if (!files?.files) return;
      if (!await loadFileSide(files, "remote", target)) return;
      await enqueuePaths(files, "local", paths, true);
    }, "primary"));
  }
  body.append(actions);
}
