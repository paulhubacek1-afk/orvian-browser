(() => {
  const state = { title: "", artist: "", isPlaying: false, url: location.href, updatedAt: 0 };
  let scheduled = false;

  const getText = (selector) => document.querySelector(selector)?.textContent?.trim() || "";

  function readNowPlaying() {
    const widget = document.querySelector('[data-testid="now-playing-widget"]');
    if (!widget) return null;

    const title = getText('[data-testid="context-item-link"]') || getText('a[href*="/track/"]');
    const artists = [...widget.querySelectorAll('a[href*="/artist/"]')]
      .map((node) => node.textContent?.trim())
      .filter(Boolean)
      .join(", ");
    const playButton = [...widget.querySelectorAll('button')]
      .find((button) => /play|pause/i.test(button.getAttribute('aria-label') || ""));

    return {
      title,
      artist: artists,
      isPlaying: /pause/i.test(playButton?.getAttribute('aria-label') || ""),
      url: location.href,
      updatedAt: Date.now()
    };
  }

  function publish() {
    scheduled = false;
    const next = readNowPlaying();
    if (!next) return;

    if (next.title === state.title && next.artist === state.artist && next.isPlaying === state.isPlaying && next.url === state.url) return;
    Object.assign(state, next);
    chrome.storage.local.set({ orvianSpotifyState: state });
  }

  function schedulePublish() {
    if (scheduled) return;
    scheduled = true;
    setTimeout(publish, 300);
  }

  const observer = new MutationObserver(schedulePublish);
  observer.observe(document.documentElement, { subtree: true, childList: true, attributes: true });
  setInterval(publish, 1600);
  publish();
})();
