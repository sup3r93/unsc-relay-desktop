function installPresentationMode(options) {
  const key = "relay.presentation.v1", text = options.text;
  let enabled = false, requested = false;
  try {
    requested = sessionStorage.getItem(key) === "on";
  } catch {
  }
  const original = /* @__PURE__ */ new Map(), dialogs = /* @__PURE__ */ new Map(), control = document.createElement("button"), banner = document.createElement("aside"), message = document.createElement("p"), stop = document.createElement("button"), style = document.createElement("style");
  control.type = stop.type = "button";
  control.dataset.presentationToggle = "";
  control.setAttribute("aria-keyshortcuts", "Control+Shift+P");
  banner.className = "presentation-banner";
  banner.setAttribute("aria-label", text("Modalit\xE0 presentazione"));
  message.setAttribute("role", "status");
  stop.onclick = () => set(false);
  control.onclick = () => set(!enabled);
  banner.append(message, stop);
  document.head.append(style);
  document.body.append(banner);
  style.textContent = `html[data-presentation=on] [data-presentation-covered]{visibility:hidden!important;position:relative!important;user-select:none!important}html[data-presentation=on] [data-presentation-covered] *,html[data-presentation=on] [data-presentation-covered] *::before,html[data-presentation=on] [data-presentation-covered] *::after{visibility:hidden!important}html[data-presentation=on] [data-presentation-covered]::after{content:attr(data-presentation-label);visibility:visible!important;position:absolute;inset:0;background:#0b1714;color:#e6e9df;display:flex;align-items:center;justify-content:center;padding:.5rem;box-sizing:border-box;font:400 1rem/1.5 system-ui;text-align:center;overflow-wrap:anywhere;z-index:100;pointer-events:none}.presentation-banner{position:fixed;inset:auto .75rem .75rem auto;max-width:min(38rem,calc(100vw - 1.5rem));padding:.75rem;background:#0b1714;color:#e6e9df;border:2px solid #bdd282;border-radius:.6rem;z-index:2147483647;box-shadow:0 .25rem 1rem #0008}.presentation-banner[hidden]{display:none}.presentation-dialog-cover{position:absolute;inset:0;z-index:200;background:#0b1714;color:#e6e9df;padding:1rem;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:1rem;overflow:auto}.presentation-dialog-cover button{min-height:2.75rem;padding:.5rem .75rem}.presentation-banner p{margin:0 0 .5rem}.presentation-banner button{background:#bdd282;color:#101b13;min-height:2.75rem;padding:.5rem .75rem;border:1px solid #bdd282;border-radius:.3rem}.presentation-banner button:focus-visible{outline:3px solid #fff;outline-offset:3px}`;
  function protect(region) {
    if (!original.has(region)) original.set(region, region.inert);
    region.inert = true;
    region.dataset.presentationCovered = "";
    region.dataset.presentationLabel = text("Contenuti nascosti \xB7 Ctrl+Shift+P per tornare");
  }
  function restore(region, inert) {
    region.inert = inert;
    delete region.dataset.presentationCovered;
    delete region.dataset.presentationLabel;
  }
  function regions() {
    if (!enabled) return;
    for (const dialog of document.querySelectorAll("dialog[open]")) {
      let entry = dialogs.get(dialog);
      if (!entry) {
        const overlay = document.createElement("section"), hint = document.createElement("p"), restoreButton = document.createElement("button");
        overlay.className = "presentation-dialog-cover";
        hint.textContent = text("Contenuti nascosti \xB7 Ctrl+Shift+P per tornare");
        restoreButton.type = "button";
        restoreButton.textContent = text("Disattiva presentazione");
        restoreButton.onclick = async () => {
          await set(false);
          Array.from(dialog.querySelectorAll("button:not([disabled])")).find((button) => button.getClientRects().length > 0 && getComputedStyle(button).visibility === "visible")?.focus();
        };
        overlay.append(hint, restoreButton);
        entry = { label: dialog.getAttribute("aria-label"), labelledby: dialog.getAttribute("aria-labelledby"), overlay };
        dialogs.set(dialog, entry);
        dialog.removeAttribute("aria-labelledby");
        dialog.setAttribute("aria-label", text("Modalit\xE0 presentazione"));
        dialog.append(overlay);
        restoreButton.focus();
      }
      for (const child of dialog.children) if (child !== entry.overlay && child instanceof HTMLElement) protect(child);
    }
    for (const region of document.querySelectorAll(options.selectors)) {
      if (region.closest(".presentation-banner")) continue;
      protect(region);
    }
    for (const [region, inert] of original) if (!region.isConnected) {
      restore(region, inert);
      original.delete(region);
    }
  }
  function labels() {
    const label = text(enabled ? "Disattiva presentazione" : "Modalit\xE0 presentazione");
    if (control.textContent !== label) control.textContent = label;
    control.setAttribute("aria-pressed", String(enabled));
    banner.hidden = !enabled;
    const copy = text("Presentazione attiva: identit\xE0, schermate operative e notifiche sono nascoste. \xC8 un mascheramento visivo: finestre esterne, registrazioni, log e acquisizioni precedenti non vengono anonimizzati.");
    if (message.textContent !== copy) message.textContent = copy;
    stop.textContent = text("Disattiva presentazione");
    regions();
  }
  function apply(value) {
    enabled = value;
    document.documentElement.dataset.presentation = value ? "on" : "off";
    try {
      if (value) sessionStorage.setItem(key, "on");
      else sessionStorage.removeItem(key);
    } catch {
    }
    if (!value) {
      for (const [region, inert] of original) restore(region, inert);
      original.clear();
      for (const [dialog, entry] of dialogs) {
        entry.overlay.remove();
        if (entry.label === null) dialog.removeAttribute("aria-label");
        else dialog.setAttribute("aria-label", entry.label);
        if (entry.labelledby === null) dialog.removeAttribute("aria-labelledby");
        else dialog.setAttribute("aria-labelledby", entry.labelledby);
      }
      dialogs.clear();
    }
    labels();
    if (!value && control.isConnected && !control.closest("[popover]:not(:popover-open)")) control.focus();
  }
  let changing = false;
  function set(value) {
    if (changing) return;
    if (!options.nativeMask) {
      apply(value);
      return;
    }
    changing = true;
    control.disabled = stop.disabled = true;
    return options.nativeMask(value).then(() => apply(value), (error) => {
      message.textContent = text(error instanceof Error ? error.message : "Operazione annullata.");
      banner.hidden = false;
    }).finally(() => {
      changing = false;
      control.disabled = stop.disabled = false;
    });
  }
  const observer = new MutationObserver(regions);
  observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ["open"] });
  window.addEventListener("keydown", (event) => {
    if (event.ctrlKey && event.shiftKey && !event.altKey && !event.metaKey && event.code === "KeyP") {
      event.preventDefault();
      event.stopImmediatePropagation();
      set(!enabled);
    }
  }, true);
  window.addEventListener("relay-preferences", labels);
  window.addEventListener("relay-language-changed", labels);
  labels();
  set(requested);
  return { button: control, set };
}
