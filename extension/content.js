(() => {
  const SOURCE = "ytmusic-discord-presence";
  let lastFingerprint = "";

  function clean(value) {
    return (value || "").replace(/\s+/g, " ").trim();
  }

  function sendToExtension(payload) {
    const runtime = globalThis.chrome?.runtime;
    if (!runtime || typeof runtime.sendMessage !== "function") {
      console.error("API chrome.runtime indisponível no content script. Recarregue a extensão e a aba do YouTube Music.");
      return;
    }

    try {
      const pending = runtime.sendMessage({ source: SOURCE, payload });
      pending?.catch(() => {});
    } catch (error) {
      // A recarga da extensão pode invalidar um content script que ainda está na página.
      if (!String(error).includes("Extension context invalidated")) {
        console.warn("Falha ao enviar o estado da música para a extensão:", error);
      }
    }
  }

  function playerText(selectors) {
    for (const selector of selectors) {
      const element = document.querySelector(selector);
      const value = clean(element?.getAttribute("title") || element?.textContent);
      if (value) return value;
    }
    return "";
  }

  function getArtworkUrl() {
    const playerBar = document.querySelector("ytmusic-player-bar");
    if (!playerBar) return "";

    const preferred = [
      ".thumbnail-image img",
      "yt-img-shadow img#img",
      "img#img",
      "img.image"
    ];
    const images = [];
    for (const selector of preferred) {
      const image = playerBar.querySelector(selector);
      if (image && !images.includes(image)) images.push(image);
    }
    for (const image of playerBar.querySelectorAll("img")) {
      if (!images.includes(image)) images.push(image);
    }

    for (const image of images) {
      const candidate = image.currentSrc || image.src || image.getAttribute("src") || "";
      try {
        const parsed = new URL(candidate, location.href);
        const host = parsed.hostname.toLowerCase();
        const trustedImageHost = ["googleusercontent.com", "ytimg.com", "ggpht.com"]
          .some(domain => host === domain || host.endsWith(`.${domain}`));
        if (parsed.protocol !== "https:" || !trustedImageHost) continue;
        return parsed.href.replace(/=w\d+-h\d+[^?]*/i, "=w512-h512");
      } catch {
        // Ignore placeholders and malformed image URLs.
      }
    }
    return "";
  }

  function getPayload() {
    const video = document.querySelector("ytmusic-player video, video");
    const title = playerText([
      "ytmusic-player-bar .title",
      "ytmusic-player-bar .title yt-formatted-string",
      "ytmusic-player-bar [slot='title']"
    ]);

    if (!video || !title || title === "YouTube Music") return { kind: "clear" };

    const byline = playerText([
      "ytmusic-player-bar .byline",
      "ytmusic-player-bar .subtitle",
      "ytmusic-player-bar [slot='subtitle']"
    ]);
    const artist = clean(byline.split(/\s*[•·]\s*/)[0]) || "Artista desconhecido";
    const currentTime = Number.isFinite(video.currentTime) ? video.currentTime : 0;
    const duration = Number.isFinite(video.duration) ? video.duration : 0;
    const artworkUrl = getArtworkUrl();

    return {
      kind: "track",
      title,
      artist,
      playing: !video.paused && !video.ended,
      currentTime,
      duration,
      artworkUrl,
      url: location.href
    };
  }

  function publish() {
    const payload = getPayload();
    const fingerprint = payload.kind === "track"
      ? [payload.title, payload.artist, payload.playing, payload.artworkUrl, Math.floor(payload.currentTime / 20)].join("\u001f")
      : "clear";
    if (fingerprint === lastFingerprint) return;
    lastFingerprint = fingerprint;
    sendToExtension(payload);
  }

  publish();
  setInterval(publish, 2000);
  window.addEventListener("pagehide", () => {
    sendToExtension({ kind: "clear" });
  });
  new MutationObserver(publish).observe(document.documentElement, {
    subtree: true,
    childList: true,
    characterData: true
  });
})();
