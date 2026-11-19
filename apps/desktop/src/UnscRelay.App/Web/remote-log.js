"use strict";
async function remoteLogDialog(pane, file) {
  const body = showModal(uiText("Log remoto"), file.name, null, true), marker = document.createElement("div"), help = document.createElement("p"), actions = document.createElement("div"), status = document.createElement("p"), output = document.createElement("pre"), label = document.createElement("label"), filter = document.createElement("input");
  body.append(marker);
  help.textContent = uiText("Vista temporanea: legge al massimo 64 KiB per richiesta e conserva fino a 1.000 righe. Alcuni cambiamenti del file tra due letture possono sfuggire.");
  filter.id = "log-filter-" + crypto.randomUUID();
  filter.maxLength = 100;
  label.htmlFor = filter.id;
  label.textContent = uiText("Filtra le righe");
  status.setAttribute("role", "status");
  output.className = "remote-log-output";
  output.tabIndex = 0;
  interfaceAttribute(output, 'aria-label', "Contenuto del log remoto");
  actions.className = "form-actions";
  body.append(help, label, filter, actions, status, output);
  let buffer = "", offset = null, prefixHash = "", decoder = new TextDecoder(), following = true, busy = false, sequence = 0, timer;
  const live = () => marker.isConnected && modal.open;
  function stop() {
    following = false;
    sequence++;
    clearTimeout(timer);
    buffer = "";
    output.textContent = "";
  }
  modal.addEventListener("close", stop, { once: true });
  function render() {
    const query = filter.value.toLocaleLowerCase();
    output.textContent = buffer.split("\n").filter((line) => !query || line.toLocaleLowerCase().includes(query)).join("\n");
    if (scroll.checked) output.scrollTop = output.scrollHeight;
  }
  filter.oninput = render;
  const refresh = localizedButton("Aggiorna adesso", () => read(false)), follow = localizedButton("Arresta aggiornamenti", () => {
    following = !following;
    sequence++;
    clearTimeout(timer);
    follow.textContent = uiText(following ? "Arresta aggiornamenti" : "Riprendi aggiornamenti");
    if (following) read(true);
    else status.textContent = uiText("Aggiornamenti del log arrestati.");
  });
  actions.append(refresh, follow);
  const scrollLabel = document.createElement("label"), scroll = document.createElement("input");
  scroll.type = "checkbox";
  scroll.checked = true;
  scrollLabel.append(scroll, document.createTextNode(uiText("Scorri automaticamente")));
  actions.append(scrollLabel);
  async function read(automatic) {
    if (!live() || busy) return;
    clearTimeout(timer);
    busy = true;
    refresh.disabled = true;
    const request = sequence;
    try {
      const value = await call("remoteLog", { sessionId: pane.id, path: file.path, offset, prefixHash, automatic });
      if (!live() || request !== sequence) return;
      if (value.paused) {
        following = false;
        follow.textContent = uiText("Riprendi aggiornamenti");
        status.textContent = uiText("Connessione a consumo: aggiornamenti automatici del log in pausa.");
        return;
      }
      if (value.reset) {
        buffer = "";
        decoder = new TextDecoder();
      }
      const bytes = Uint8Array.from(atob(value.data), (c) => c.charCodeAt(0));
      if (bytes.includes(0)) throw Error(uiText("Il file contiene dati binari."));
      buffer = (buffer + decoder.decode(bytes, { stream: true })).slice(-262144).split("\n").slice(-1e3).join("\n");
      offset = value.offset;
      prefixHash = value.prefixHash;
      render();
      status.textContent = uiText(value.reset ? "Il file è cambiato: vista del log riavviata." : "Log aggiornato.") + " " + new Intl.DateTimeFormat(document.documentElement.lang, { timeStyle: "medium" }).format(/* @__PURE__ */ new Date());
    } catch (error) {
      if (live() && request === sequence) {
        following = false;
        follow.textContent = uiText("Riprendi aggiornamenti");
        status.textContent = error.message;
      }
    } finally {
      busy = false;
      if (live()) {
        refresh.disabled = false;
        if (following) timer = setTimeout(() => read(true), 2e3);
      } else stop();
    }
  }
  await read(false);
}
