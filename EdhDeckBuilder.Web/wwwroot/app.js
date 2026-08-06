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

// Shared helper: saves a result entry and evicts the oldest beyond maxResults.
// indexKey tracks insertion order so we know what to evict.
function _saveResult(key, value, maxResults, indexKey) {
    let index = [];
    try { index = JSON.parse(localStorage.getItem(indexKey) || '[]'); } catch { }

    // Prune stale references (entries removed outside this function, e.g. manual clear).
    index = index.filter(k => localStorage.getItem(k) !== null);

    // Add new key (GUIDs are unique, but guard against duplicates).
    if (!index.includes(key)) index.push(key);

    // Evict oldest entries that exceed the limit.
    while (index.length > maxResults) {
        localStorage.removeItem(index.shift());
    }

    localStorage.setItem(indexKey, JSON.stringify(index));
    try { localStorage.setItem(key, value); } catch { }
}

function saveDeckResult(key, value, maxResults) {
    _saveResult(key, value, maxResults, 'edh-deck-index');
}

function saveAnalysisResult(key, value, maxResults) {
    _saveResult(key, value, maxResults, 'edh-analysis-index');
}

// Returns the ordered array of localStorage keys for the given index, newest-first.
function getResultIndex(indexKey) {
    try {
        const index = JSON.parse(localStorage.getItem(indexKey) || '[]');
        return index.filter(k => localStorage.getItem(k) !== null).reverse();
    } catch { return []; }
}

function downloadTextFile(filename, content, mimeType = 'text/plain') {
    const blob = new Blob([content], { type: mimeType + ';charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(() => URL.revokeObjectURL(url), 100);
}
