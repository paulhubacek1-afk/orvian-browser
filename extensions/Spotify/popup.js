const title = document.getElementById('title');
const artist = document.getElementById('artist');
const playButton = document.getElementById('play');
const list = document.getElementById('list');
const input = document.getElementById('input');
let queue = [];

async function activeSpotifyTab() {
  const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
  return tabs.find((tab) => (tab.url || '').startsWith('https://open.spotify.com/')) || tabs[0];
}

async function runInSpotify(code) {
  const tab = await activeSpotifyTab();
  if (!tab?.id) return;
  try { await chrome.scripting?.executeScript({ target: { tabId: tab.id }, func: code }); } catch (_) {}
}

function renderQueue() {
  list.replaceChildren();
  queue.forEach((item, index) => {
    const li = document.createElement('li');
    li.textContent = item;
    li.title = 'In Spotify öffnen';
    li.addEventListener('click', async () => {
      const query = encodeURIComponent(item);
      const tab = await activeSpotifyTab();
      if (tab?.id) chrome.tabs.update(tab.id, { url: `https://open.spotify.com/search/${query}` });
      else chrome.tabs.create({ url: `https://open.spotify.com/search/${query}` });
    });
    list.appendChild(li);
  });
}

async function refreshState() {
  const data = await chrome.storage.local.get(['orvianSpotifyState', 'orvianSpotifyQueue']);
  queue = Array.isArray(data.orvianSpotifyQueue) ? data.orvianSpotifyQueue : [];
  const state = data.orvianSpotifyState || {};
  title.textContent = state.title || 'Spotify öffnen …';
  artist.textContent = state.artist || '';
  playButton.textContent = state.isPlaying ? '❚❚' : '▶';
  renderQueue();
}

playButton.addEventListener('click', () => runInSpotify(() => {
  const widget = document.querySelector('[data-testid="now-playing-widget"]');
  const button = [...(widget?.querySelectorAll('button') || [])]
    .find((item) => /play|pause/i.test(item.getAttribute('aria-label') || ''));
  button?.click();
}));

document.getElementById('next').addEventListener('click', () => runInSpotify(() => {
  const widget = document.querySelector('[data-testid="now-playing-widget"]');
  const button = [...(widget?.querySelectorAll('button') || [])]
    .find((item) => /next/i.test(item.getAttribute('aria-label') || ''));
  button?.click();
}));

document.getElementById('prev').addEventListener('click', () => runInSpotify(() => {
  const widget = document.querySelector('[data-testid="now-playing-widget"]');
  const button = [...(widget?.querySelectorAll('button') || [])]
    .find((item) => /previous/i.test(item.getAttribute('aria-label') || ''));
  button?.click();
}));

document.getElementById('form').addEventListener('submit', async (event) => {
  event.preventDefault();
  const value = input.value.trim();
  if (!value) return;
  queue.push(value);
  await chrome.storage.local.set({ orvianSpotifyQueue: queue });
  input.value = '';
  renderQueue();
});

document.getElementById('clear').addEventListener('click', async () => {
  queue = [];
  await chrome.storage.local.set({ orvianSpotifyQueue: [] });
  renderQueue();
});

refreshState();
setInterval(refreshState, 1000);
