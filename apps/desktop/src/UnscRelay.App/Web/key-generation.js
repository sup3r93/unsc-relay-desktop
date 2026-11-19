"use strict";
const keyAlgorithmChoices = [
  { id: "ed25519", name: "Ed25519", score: 8, level: "Circa 128 bit di sicurezza", recommended: true, how: "Firma su curva Edwards, deterministica. Chiavi compatte e operazioni rapide.", use: "Scelta consigliata per nuovi accessi SSH. Richiede un server compatibile con Ed25519.", security: "Elevata sicurezza classica con dimensioni ridotte. Un voto maggiore non e necessario per il normale uso SSH." },
  { id: "rsa3072", name: "RSA 3072 bit", score: 8, level: "Livello di riferimento: 128 bit", how: "Firma basata su grandi numeri interi. Relay usa firme RSA SHA-2.", use: "Ampia compatibilita con server che supportano RSA SHA-2. Chiavi piu grandi rispetto alle curve ellittiche.", security: "Buona base di sicurezza classica. Il nome pubblico ssh-rsa non implica che venga usata la firma SHA-1." },
  { id: "rsa4096", name: "RSA 4096 bit", score: 8, level: "Margine oltre RSA 3072 · inferiore a 192 bit", how: "La stessa famiglia RSA, con una chiave piu lunga. Firma tramite SHA-2.", use: "Per sistemi che richiedono RSA 4096. Generazione e firme piu costose rispetto a Ed25519.", security: "Margine maggiore di RSA 3072, nella stessa fascia del voto: 4096 bit di chiave non equivalgono a 4096 bit di sicurezza." },
  { id: "rsa8192", name: "RSA 8192 bit", score: 9, level: "Livello di riferimento: almeno 192 bit", how: "Firma RSA con chiave da 8192 bit e firme SHA-2.", use: "Per requisiti specifici di RSA con margine elevato. La generazione puo richiedere molto tempo; chiavi e firme sono grandi.", security: "Margine classico maggiore di RSA 4096. Non e post-quantistico e non protegge dal furto della chiave privata." },
  { id: "ecdsa256", name: "ECDSA NIST P-256", score: 8, level: "Livello di riferimento: 128 bit", how: "Firma su curva ellittica NIST P-256. Chiavi e firme compatte.", use: "Per sistemi che richiedono una curva NIST. Il server deve supportare ECDSA P-256.", security: "Elevata sicurezza classica. La generazione casuale durante la firma deve essere corretta; se ne occupa la libreria del sistema." },
  { id: "ecdsa384", name: "ECDSA NIST P-384", score: 9, level: "Livello di riferimento: 192 bit", how: "Firma su curva ellittica NIST P-384, con parametri piu ampi di P-256.", use: "Per requisiti espliciti di margine crittografico maggiore. Verifica il supporto del server.", security: "Margine teorico classico superiore a P-256. Richiede piu calcolo e non certifica da sola il sistema." },
  { id: "ecdsa521", name: "ECDSA NIST P-521", score: 10, level: "Livello di riferimento: 256 bit", how: "Firma su curva ellittica NIST P-521, la curva piu ampia disponibile qui.", use: "Per esigenze specifiche di margine classico elevato. Supporto e prestazioni dipendono dal server.", security: "Fascia massima di questo indice. 10/10 non significa invulnerabile e non protegge dal furto della chiave." }
];
function renderKeyGeneration() {
  const body = showModal(uiText("Genera chiave SSH"), "CHIAVI LOCALI / SCEGLI L’ALGORITMO", keysDialog, true);
  modal.classList.add("algorithm-modal");
  const form = document.createElement("form");
  form.className = "key-generation-form";
  const head = document.createElement("div");
  head.className = "key-generation-head";
  const keyName = field(head, uiText("Nome della chiave"), "name", "Nuova chiave");
  keyName.required = true;
  keyName.maxLength = 100;
  notice(head, uiText("Seleziona una card. Ed25519 e la scelta consigliata per l’uso comune; gli altri algoritmi servono anche per requisiti e compatibilita specifici."));
  form.append(head);
  const selection = document.createElement("section");
  selection.className = "algorithm-selection";
  interfaceAttribute(selection, 'aria-label', "Algoritmi e dettagli");
  const legend = document.createElement("h3");
  legend.className = "algorithm-section-title";
  legend.id = "algorithm-choice-heading";
  legend.textContent = uiText("Algoritmo · Sicurezza classica (indice indicativo 1–10)");
  selection.append(legend);
  const grid = document.createElement("div");
  grid.className = "algorithm-grid";
  grid.setAttribute("role", "radiogroup");
  grid.setAttribute("aria-labelledby", "algorithm-choice-heading");
  for (const choice of [...keyAlgorithmChoices].sort((a, b) => b.score - a.score)) {
    const card = document.createElement("label");
    card.className = "algorithm-card";
    card.dataset.algorithm = choice.id;
    const radio = document.createElement("input");
    radio.type = "radio";
    radio.name = "algorithm";
    radio.value = choice.id;
    radio.checked = choice.recommended === true;
    radio.required = true;
    radio.setAttribute("aria-label", choice.name);
    const top = document.createElement("span");
    top.className = "algorithm-card-top";
    const name = document.createElement("strong");
    name.textContent = choice.name;
    top.append(radio, name);
    card.append(top);
    if (choice.recommended) {
      const recommended = document.createElement("span");
      recommended.className = "algorithm-recommended";
      recommended.textContent = uiText("CONSIGLIATA");
      card.append(recommended);
    }
    const score = document.createElement("span");
    score.className = "algorithm-score";
    score.textContent = uiText("Sicurezza classica ") + choice.score + " / 10";
    card.append(score);
    const level = document.createElement("span");
    level.className = "algorithm-level";
    level.textContent = choice.level;
    card.append(level);
    for (const [label, text] of [["Come funziona", choice.how], ["Quando sceglierla", choice.use], ["Sicurezza", choice.security]]) {
      const paragraph = document.createElement("span");
      paragraph.className = "algorithm-copy";
      const caption = document.createElement("b");
      caption.textContent = label + " · ";
      paragraph.append(caption, document.createTextNode(text));
      card.append(paragraph);
    }
    grid.append(card);
  }
  selection.append(grid);
  const criteria = document.createElement("details");
  criteria.className = "algorithm-criteria";
  const summary = document.createElement("summary");
  summary.textContent = uiText("Come leggere il voto da 1 a 10");
  criteria.append(summary);
  notice(criteria, uiText("Indice indicativo di Relay, non un voto NIST o una certificazione. Fasce: 8/10 da circa 128 bit, 9/10 da circa 192 bit, 10/10 da circa 256 bit di forza crittografica classica. RSA 4096 ha piu margine di RSA 3072 ma resta sotto la fascia dei 192 bit. Non sono percentuali di protezione."));
  notice(criteria, uiText("La scala si basa sui livelli di riferimento NIST SP 800-57 e, per Ed25519, su RFC 8032. Non misura protezione del PC, furto della chiave o qualita della configurazione. I bit di sicurezza sono diversi dalla lunghezza della chiave. Nessuno dei sette algoritmi e post-quantistico; lo scambio chiavi post-quantistico SSH e una funzione distinta."));
  selection.append(criteria);
  const advanced = document.createElement("details");
  advanced.className = "algorithm-criteria";
  const advancedTitle = document.createElement("summary");
  advancedTitle.textContent = uiText("Hardware, Ed448 e firme post-quantistiche");
  advanced.append(advancedTitle);
  const advancedGrid = document.createElement("div");
  advancedGrid.className = "algorithm-grid algorithm-advanced";
  for (const item of [
    { name: "Ed25519-SK / FIDO2", status: "TRAMITE AGENT · TOKEN NECESSARIO", text: "Forza classica della famiglia Ed25519: circa 128 bit, indice 8/10. La firma resta sul dispositivo; maggiore protezione dal furto dei file. Generazione disponibile tramite client OpenSSH e token compatibili.", agent: true },
    { name: "ECDSA-SK / FIDO2", status: "TRAMITE AGENT · TOKEN NECESSARIO", text: "Curva NIST P-256: circa 128 bit, indice 8/10. Non e automaticamente meno sicura di Ed25519-SK. PIN, tocco e protezione dipendono da dispositivo e agent.", agent: true },
    { name: "Ed448", status: "NON DISPONIBILE NEL MOTORE", text: "La specifica Ed448 punta a circa 224 bit di forza classica. Non e supportata dal motore SSH di questa versione, quindi non e selezionabile per la generazione." },
    { name: "ML-DSA-44 + Ed25519", status: "INCLUSA / OPENSSH NATIVO", text: "Generazione e firma disponibili con OpenSSH incluso. Usa una sessione OpenSSH nativa con server compatibile. Il deposito e il motore SSH.NET ordinario non interpretano questa chiave: non viene sostituita silenziosamente con un algoritmo classico." }
  ]) {
    const card = document.createElement("section");
    card.className = "algorithm-card";
    const title = document.createElement("strong");
    title.textContent = item.name;
    const status = document.createElement("span");
    status.className = "algorithm-recommended";
    status.textContent = item.status;
    const text = document.createElement("span");
    text.className = "algorithm-copy";
    text.textContent = item.text;
    card.append(title, status, text);
    if (item.agent) card.append(localizedButton("Gestisci token / agent", agentDialog), localizedButton("Genera sul dispositivo", hardwareGenerationDialog));
    else card.append(localizedButton("Opzioni client OpenSSH", hardwareGenerationDialog));
    advancedGrid.append(card);
  }
  advanced.append(advancedGrid);
  selection.append(advanced);
  form.append(selection);
  const actions = document.createElement("div");
  actions.className = "key-generation-actions";
  const selected = document.createElement("span");
  selected.className = "generation-selected";
  selected.setAttribute("role", "status");
  selected.textContent = uiText("Selezionata: Ed25519");
  form.onchange = () => {
    selected.textContent = uiText("Selezionata: ") + keyAlgorithmChoices.find((x) => x.id === form.querySelector("[name=algorithm]:checked").value).name;
  };
  const cancel = localizedButton("Annulla", keysDialog);
  const save = document.createElement("button");
  save.className = "primary";
  save.textContent = uiText("Genera nel deposito");
  actions.append(selected, cancel, save);
  form.append(actions);
  form.onsubmit = async (e) => {
    e.preventDefault();
    if (save.disabled || !form.reportValidity()) return;
    save.disabled = true;
    keyName.disabled = true;
    for (const radio of form.querySelectorAll("[name=algorithm]")) radio.disabled = true;
    cancel.disabled = true;
    selected.textContent = uiText("Generazione in corso…") + (form.querySelector("[name=algorithm]:checked").value === "rsa8192" ? " RSA 8192 puo richiedere tempo." : "");
    const payload = { name: keyName.value, algorithm: form.querySelector("[name=algorithm]:checked").value };
    try {
      update(await call("vaultGenerate", payload));
      selectedClientKeyId = state.vault.keys.at(-1)?.id || null;
      clientKeyFilter = "";
      keysDialog();
      toast(uiText("Chiave generata nel deposito cifrato."));
    } catch {
    } finally {
      save.disabled = false;
      keyName.disabled = false;
      cancel.disabled = false;
      for (const radio of form.querySelectorAll("[name=algorithm]")) radio.disabled = false;
      selected.textContent = uiText("Selezionata: ") + keyAlgorithmChoices.find((x) => x.id === payload.algorithm).name;
    }
  };
  body.append(form);
}
