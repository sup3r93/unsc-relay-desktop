"use strict";
async function ticketAttachmentDialog(id, attachment) {
  const dialog = document.createElement("dialog"), heading = document.createElement("h2"), header = document.createElement("header"), name = document.createElement("p"), body = document.createElement("div"), marker = document.createElement("div"), status = document.createElement("p");
  dialog.className = "manager-modal";
  dialog.dataset.attachmentPreview = "true";
  heading.id = "attachment-preview-" + crypto.randomUUID();
  heading.textContent = uiText("Anteprima protetta");
  dialog.setAttribute("aria-labelledby", heading.id);
  name.textContent = attachment.name;
  body.className = "attachment-preview-body";
  const close = localizedButton("Chiudi", () => dialog.close());
  header.className = "modal-header";
  header.append(heading, close);
  body.append(name, marker, status);
  dialog.append(header, body);
  document.body.append(dialog);
  dialog.showModal();
  close.focus();
  status.setAttribute("role", "status");
  status.textContent = uiText("Caricamento…");
  let closed = false;
  const closeWithParent = () => dialog.close();
  modal.addEventListener("close", closeWithParent, { once: true });
  dialog.addEventListener("close", () => {
    closed = true;
    marker.replaceChildren();
    modal.removeEventListener("close", closeWithParent);
    dialog.remove();
  }, { once: true });
  try {
    const value = await rpc("ticketAttachmentPreview", { id, attachment: attachment.id });
    if (closed || !marker.isConnected) return;
    const bytes = Uint8Array.from(atob(value.data), (c) => c.charCodeAt(0));
    if (bytes.length > 2097152) throw Error(uiText("Allegato massimo 2 MiB."));
    body.append(localizedButton("Scarica allegato", () => rpc("ticketAttachmentSave", { id, attachment: attachment.id })));
    if (["image/png", "image/jpeg", "image/webp"].includes(value.mediaType)) {
      safeImagePreview(bytes, value.mediaType);
      const image = document.createElement("img");
      image.className = "protected-attachment-image";
      image.alt = attachment.name;
      image.src = "data:" + value.mediaType + ";base64," + value.data;
      marker.append(image);
    } else if (["text/plain", "application/json"].includes(value.mediaType)) {
      const text = document.createElement("pre");
      text.tabIndex = 0;
      interfaceAttribute(text, 'aria-label', "Contenuto dell’allegato");
      text.className = "protected-attachment-text";
      if (bytes.includes(0)) throw Error(uiText("Il file contiene dati binari."));
      try {
        text.textContent = new TextDecoder("utf-8", { fatal: true }).decode(bytes);
      } catch {
        throw Error(uiText("Il file non è testo UTF-8 valido."));
      }
      marker.append(text);
    } else throw Error(uiText("Formato non supportato per l’anteprima protetta."));
    status.textContent = uiText("Anteprima in sola lettura. Il contenuto non viene eseguito.");
  } catch (error) {
    if (!closed && marker.isConnected) status.textContent = error.message;
  }
}
