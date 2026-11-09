const NATIVE_HOST = "org.codex.ytmusic_discord";
const SOURCE = "ytmusic-discord-presence";

let nativePort = null;
let latestPayload = null;
let latestTabId = null;
let reconnectTimer = null;

function postLatest() {
  if (!latestPayload) return;
  connectNative();
  if (nativePort) {
    try {
      nativePort.postMessage(latestPayload);
    } catch {
      nativePort = null;
      scheduleReconnect();
    }
  }
}

function connectNative() {
  if (nativePort) return;

  try {
    const port = chrome.runtime.connectNative(NATIVE_HOST);
    nativePort = port;
    port.onMessage.addListener((response) => {
      const ok = response?.ok ?? response?.Ok;
      const error = response?.error ?? response?.Error;
      if (ok === false) {
        console.error("[YouTube Music para Discord] O host recusou a atualização:", error || "erro sem detalhes");
      } else {
        console.info("[YouTube Music para Discord] O host respondeu ao estado da faixa.");
      }
    });
    port.onDisconnect.addListener(() => {
      const error = chrome.runtime.lastError;
      nativePort = null;
      if (error) console.warn("Host C# desconectado:", error.message);
      scheduleReconnect();
    });
  } catch (error) {
    console.warn("Não foi possível iniciar o host C#:", error);
    nativePort = null;
    scheduleReconnect();
  }
}

function scheduleReconnect() {
  if (reconnectTimer || !latestPayload) return;
  reconnectTimer = setTimeout(() => {
    reconnectTimer = null;
    postLatest();
  }, 3000);
}

function sendClear() {
  latestPayload = { kind: "clear" };
  postLatest();
}

chrome.runtime.onMessage.addListener((message, sender) => {
  if (!message || message.source !== SOURCE || !message.payload) return;
  latestPayload = message.payload;
  latestTabId = sender.tab?.id ?? null;
  postLatest();
});

chrome.tabs.onRemoved.addListener((tabId) => {
  if (tabId === latestTabId) {
    latestTabId = null;
    sendClear();
  }
});

