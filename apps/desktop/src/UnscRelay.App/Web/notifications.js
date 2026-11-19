"use strict";
function notificationsDialog() {
  const body = showModal(uiText("Notifiche"), uiText("ACCOUNT / CENTRO NOTIFICHE"), null, true), host = document.createElement("section");
  body.append(host);
  void mountNotificationCenter(host, uiText, { load: () => rpc("notificationOverview"), save: (value, version) => rpc("notificationDisplay", { value, version }), read: (id) => rpc("notificationRead", { id }), readAll: () => rpc("notificationReadAll"), date: (value) => new Intl.DateTimeFormat(document.documentElement.lang, { dateStyle: "short", timeStyle: "short" }).format(new Date(value)), changed: () => {
  }, open: (row) => {
    if (/^\/area-privata\/ticket\/[0-9a-f-]{36}$/i.test(row.href)) supportDialog();
    else if (row.href === "/area-privata") archivesDialog();
    else accountProfileDialog();
  } });
}
