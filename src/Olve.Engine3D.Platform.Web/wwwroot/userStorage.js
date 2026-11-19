const StoreName = 'entries';
const databases = new Map();

function completion(request) {
    return new Promise((resolve, reject) => {
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

function database(name) {
    let opened = databases.get(name);
    if (!opened) {
        const request = indexedDB.open(name, 1);
        request.onupgradeneeded = () => request.result.createObjectStore(StoreName, { keyPath: ['area', 'key'] });
        opened = completion(request);
        databases.set(name, opened);
    }
    return opened;
}

async function inTransaction(name, mode, act) {
    const transaction = (await database(name)).transaction(StoreName, mode);
    const done = new Promise((resolve, reject) => {
        transaction.oncomplete = resolve;
        transaction.onerror = () => reject(transaction.error);
        transaction.onabort = () => reject(transaction.error);
    });
    const result = await act(transaction.objectStore(StoreName));
    await done;
    return result;
}

export function list(name, area) {
    return inTransaction(name, 'readonly', async store => {
        const entries = await completion(store.getAll(IDBKeyRange.bound([area, ''], [area, '￿'])));
        return JSON.stringify(entries.map(entry => ({ key: entry.key, modified: entry.modified })));
    });
}

export function read(name, area, key) {
    return inTransaction(name, 'readonly', async store => (await completion(store.get([area, key])))?.bytes ?? null);
}

export function write(name, area, key, bytes, modified) {
    const copy = bytes.slice();
    return inTransaction(name, 'readwrite', store => completion(store.put({ area, key, bytes: copy, modified })));
}

export function remove(name, area, key) {
    return inTransaction(name, 'readwrite', async store => {
        const counted = completion(store.count([area, key]));
        const deleted = completion(store.delete([area, key]));
        const existed = (await counted) > 0;
        await deleted;
        return existed;
    });
}

export function length(bytes) {
    return bytes.length;
}

export function copy(source, target) {
    target.set(source);
}
