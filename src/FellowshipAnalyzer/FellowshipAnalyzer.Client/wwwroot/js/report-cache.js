/**
 * IndexedDB-backed cache for Fellowship Analyzer combat event data.
 *
 * Database: "fellowship-analyzer"
 *   Store "events": keyed by "{reportCode}/{fightId}/{playerId}"
 *     value: { eventsJson: string, fightName: string|null, playerName: string|null,
 *              heroId: string|null, cachedAt: number (ms since epoch) }
 *   Store "history": same key, same metadata minus eventsJson (for listing without loading events)
 *     value: { reportCode, fightId, playerId, fightName, playerName, heroId, cachedAt }
 *   Store "masterdata": keyed by reportCode
 *     value: { masterDataJson: string }
 *
 * Max 20 entries are kept; oldest (by cachedAt) are evicted on insert.
 */

const DB_NAME = 'fellowship-analyzer';
const DB_VERSION = 2;
const EVENTS_STORE = 'events';
const HISTORY_STORE = 'history';
const MASTERDATA_STORE = 'masterdata';
const MAX_ENTRIES = 20;

let _db = null;

function openDb() {
    if (_db) return Promise.resolve(_db);

    return new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, DB_VERSION);

        req.onupgradeneeded = (event) => {
            const db = event.target.result;
            if (!db.objectStoreNames.contains(EVENTS_STORE)) {
                db.createObjectStore(EVENTS_STORE);
            }
            if (!db.objectStoreNames.contains(HISTORY_STORE)) {
                db.createObjectStore(HISTORY_STORE);
            }
            if (!db.objectStoreNames.contains(MASTERDATA_STORE)) {
                db.createObjectStore(MASTERDATA_STORE);
            }
        };

        req.onsuccess = (event) => {
            _db = event.target.result;
            resolve(_db);
        };

        req.onerror = () => reject(req.error);
    });
}

function txn(db, stores, mode) {
    return db.transaction(stores, mode);
}

function promisify(req) {
    return new Promise((resolve, reject) => {
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
    });
}

/**
 * Retrieve cached events JSON for the given key.
 * @returns {Promise<string|null>} Raw events JSON string, or null on miss.
 */
export async function getCachedEvents(reportCode, fightId, playerId) {
    const db = await openDb();
    const key = `${reportCode}/${fightId}/${playerId}`;
    const t = txn(db, [EVENTS_STORE], 'readonly');
    const entry = await promisify(t.objectStore(EVENTS_STORE).get(key));
    return entry?.eventsJson ?? null;
}

/**
 * Store events + metadata for a completed fight.
 * Evicts the oldest entry when the store exceeds MAX_ENTRIES.
 *
 * @param {string} reportCode
 * @param {number} fightId
 * @param {number} playerId
 * @param {string} eventsJson    Serialized events array JSON
 * @param {string|null} fightName
 * @param {string|null} playerName
 * @param {string|null} heroId
 */
export async function cacheEvents(reportCode, fightId, playerId, eventsJson, fightName, playerName, heroId) {
    const db = await openDb();
    const key = `${reportCode}/${fightId}/${playerId}`;
    const cachedAt = Date.now();

    const meta = { reportCode, fightId, playerId, fightName, playerName, heroId, cachedAt };

    const t = txn(db, [EVENTS_STORE, HISTORY_STORE], 'readwrite');
    const eventsStore = t.objectStore(EVENTS_STORE);
    const historyStore = t.objectStore(HISTORY_STORE);

    eventsStore.put({ eventsJson, ...meta }, key);
    historyStore.put(meta, key);

    // Evict oldest entries if over the limit
    const allKeys = await promisify(historyStore.getAllKeys());
    if (allKeys.length > MAX_ENTRIES) {
        const allValues = await promisify(historyStore.getAll());
        // Sort ascending by cachedAt; evict the oldest
        const sorted = allKeys
            .map((k, i) => ({ key: k, cachedAt: allValues[i]?.cachedAt ?? 0 }))
            .sort((a, b) => a.cachedAt - b.cachedAt);

        const toEvict = sorted.slice(0, allKeys.length - MAX_ENTRIES);
        for (const { key: evictKey } of toEvict) {
            eventsStore.delete(evictKey);
            historyStore.delete(evictKey);
        }
    }

    await new Promise((resolve, reject) => {
        t.oncomplete = resolve;
        t.onerror = () => reject(t.error);
        t.onabort = () => reject(t.error);
    });
}

/**
 * Returns all history entries sorted newest-first.
 * @returns {Promise<Array>}
 */
export async function getHistory() {
    const db = await openDb();
    const t = txn(db, [HISTORY_STORE], 'readonly');
    const all = await promisify(t.objectStore(HISTORY_STORE).getAll());
    return all.sort((a, b) => b.cachedAt - a.cachedAt);
}

/**
 * Remove a single entry from both stores.
 */
export async function removeEntry(reportCode, fightId, playerId) {
    const db = await openDb();
    const key = `${reportCode}/${fightId}/${playerId}`;
    const t = txn(db, [EVENTS_STORE, HISTORY_STORE], 'readwrite');
    t.objectStore(EVENTS_STORE).delete(key);
    t.objectStore(HISTORY_STORE).delete(key);
    await new Promise((resolve, reject) => {
        t.oncomplete = resolve;
        t.onerror = () => reject(t.error);
    });
}

/**
 * Retrieve cached master data JSON for the given report code.
 * @returns {Promise<string|null>}
 */
export async function getCachedMasterData(reportCode) {
    const db = await openDb();
    const t = txn(db, [MASTERDATA_STORE], 'readonly');
    const entry = await promisify(t.objectStore(MASTERDATA_STORE).get(reportCode));
    return entry?.masterDataJson ?? null;
}

/**
 * Store master data JSON for a report.
 * @param {string} reportCode
 * @param {string} masterDataJson
 */
export async function cacheMasterData(reportCode, masterDataJson) {
    const db = await openDb();
    const t = txn(db, [MASTERDATA_STORE], 'readwrite');
    t.objectStore(MASTERDATA_STORE).put({ masterDataJson }, reportCode);
    await new Promise((resolve, reject) => {
        t.oncomplete = resolve;
        t.onerror = () => reject(t.error);
    });
}
