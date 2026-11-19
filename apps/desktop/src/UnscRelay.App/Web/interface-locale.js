"use strict";
function uiText(key) {
  return interfaceCatalog[document.documentElement.lang]?.[key] ?? key;
}
function interfaceLabel(element, key) {
  element.dataset.interfaceKey = key;
  element.textContent = uiText(key);
  return element;
}
const interfaceReverseKeys = /* @__PURE__ */ new Map();
function interfaceKnownKey(value) {
  if (typeof value !== "string") return null;
  if (Object.hasOwn(interfaceCatalog.it, value)) return value;
  const language = document.documentElement.lang;
  if (!interfaceReverseKeys.has(language)) {
    const reverse = /* @__PURE__ */ new Map();
    for (const [key, text] of Object.entries(interfaceCatalog[language] || {})) {
      const matches = reverse.get(text) || [];
      matches.push(key);
      reverse.set(text, matches);
    }
    interfaceReverseKeys.set(language, reverse);
  }
  const keys = interfaceReverseKeys.get(language).get(value) || [];
  if (!keys.length) return null;
  return keys.every((key) => Object.keys(interfaceCatalog).every((locale) => interfaceCatalog[locale][key] === interfaceCatalog[locale][keys[0]])) ? keys[0] : null;
}
function interfaceUiLabel(element, value) {
  const key = interfaceKnownKey(value);
  if (key !== null) return interfaceLabel(element, key);
  element.textContent = value;
  return element;
}
function interfaceAttribute(element, attribute, key) {
  const names = {"aria-label": "interfaceLabel", title: "interfaceTitle", placeholder: "interfacePlaceholder", alt: "interfaceAlt"};
  if (!Object.hasOwn(names, attribute)) throw new Error("Unsupported interface attribute.");
  element.dataset[names[attribute]] = key;
  element.setAttribute(attribute, uiText(key));
  return element;
}
function refreshInterfaceLabels() {
  for (const element of document.querySelectorAll("[data-interface-key]")) element.textContent = uiText(element.dataset.interfaceKey);
  for (const element of document.querySelectorAll("[data-interface-label]")) element.setAttribute("aria-label", uiText(element.dataset.interfaceLabel));
  for (const element of document.querySelectorAll("[data-interface-title]")) element.title = uiText(element.dataset.interfaceTitle);
  for (const element of document.querySelectorAll("[data-interface-placeholder]")) element.placeholder = uiText(element.dataset.interfacePlaceholder);
  for (const element of document.querySelectorAll("[data-interface-alt]")) element.alt = uiText(element.dataset.interfaceAlt);
}
function setInterfaceLanguage(language) {
  const next = Object.hasOwn(interfaceCatalog, language) ? language : "it", changed = document.documentElement.lang !== next;
  document.documentElement.lang = next;
  refreshInterfaceLabels();
  if (changed) window.dispatchEvent(new Event("relay-language-changed"));
}
function localizedButton(key, action, kind = "") {
  return interfaceLabel(button(key, action, kind), key);
}
function createLanguageSelector(parent) {
  const wrapper = document.createElement("div"), label = document.createElement("label"), text = interfaceLabel(document.createElement("span"), "Lingua dell’interfaccia"), select = document.createElement("select");
  select.id = "interface-language-" + crypto.randomUUID();
  label.htmlFor = select.id;
  wrapper.className = "interface-language-control";
  for (const [code, name] of [["it", "Italiano"], ["en", "English"], ["fr", "Français"], ["de", "Deutsch"], ["es", "Español"]]) {
    const option = document.createElement("option");
    option.value = code;
    option.textContent = name;
    select.append(option);
  }
  select.value = document.documentElement.lang;
  select.onchange = async () => {
    const previous = document.documentElement.lang;
    select.disabled = true;
    try {
      const result = await rpc("languageSettings", { language: select.value });
      update(result);
      setInterfaceLanguage(select.value);
      toast(uiText("Lingua salvata."));
    } catch (error) {
      select.value = previous;
      toast(error.message, true);
    } finally {
      select.disabled = false;
    }
  };
  label.append(text);
  wrapper.append(label, select);
  parent.append(wrapper);
  return select;
}
