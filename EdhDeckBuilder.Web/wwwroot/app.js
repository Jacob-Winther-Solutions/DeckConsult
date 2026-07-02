function setCookie(name, value, days) {
    const expires = new Date(Date.now() + days * 864e5).toUTCString();
    document.cookie = `${encodeURIComponent(name)}=${encodeURIComponent(value)}; expires=${expires}; path=/; SameSite=Strict`;
}

function getCookie(name) {
    const encodedName = encodeURIComponent(name);
    const pair = document.cookie.split('; ').find(c => c.startsWith(encodedName + '='));
    return pair ? decodeURIComponent(pair.substring(encodedName.length + 1)) : null;
}

function deleteCookie(name) {
    document.cookie = `${encodeURIComponent(name)}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; SameSite=Strict`;
}

function setLocalStorage(key, value) {
    try { localStorage.setItem(key, value); } catch { }
}

function getLocalStorage(key) {
    return localStorage.getItem(key);
}

// Saves a deck result and evicts the oldest entries beyond maxResults.
// The index (edh-deck-index) tracks insertion order so we know what to evict.
// maxResults is passed from the server so it can be subscription-driven later.
function saveDeckResult(key, value, maxResults) {
    const INDEX_KEY = 'edh-deck-index';

    let index = [];
    try { index = JSON.parse(localStorage.getItem(INDEX_KEY) || '[]'); } catch { }

    // Prune stale references (entries removed outside this function, e.g. manual clear).
    index = index.filter(k => localStorage.getItem(k) !== null);

    // Add new key (GUIDs are unique, but guard against duplicates).
    if (!index.includes(key)) index.push(key);

    // Evict oldest entries that exceed the limit.
    while (index.length > maxResults) {
        localStorage.removeItem(index.shift());
    }

    localStorage.setItem(INDEX_KEY, JSON.stringify(index));
    try { localStorage.setItem(key, value); } catch { }
}

function downloadTextFile(filename, content) {
    const blob = new Blob([content], { type: 'text/markdown;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(() => URL.revokeObjectURL(url), 100);
}
