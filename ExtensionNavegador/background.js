// Añade «Traducir con Traductor MSM» al menú del clic derecho cuando hay texto seleccionado.
chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({ id: "traductormsm", title: "Traducir con Traductor MSM", contexts: ["selection"] });
});

// Al hacer clic: abre traductormsm:?t=<texto>. Windows lo entrega al Traductor (registrado por su instalador).
chrome.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId !== "traductormsm" || !info.selectionText) return;
  const url = "traductormsm:?t=" + encodeURIComponent(info.selectionText);
  if (tab && tab.id >= 0) {
    chrome.scripting.executeScript({ target: { tabId: tab.id }, func: (u) => { location.href = u; }, args: [url] })
      .catch(() => chrome.tabs.update(tab.id, { url }));
  } else {
    chrome.tabs.create({ url });
  }
});
