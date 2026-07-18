/**
 * IndexedDB-backed cache for Fellowship Analyzer combat event data.
 *
 * Database: "fellowship-analyzer"
 *   Store "events": keyed by "{reportCode}/{fightId}/{playerId}"
 *     value: { eventsBlob: Blob, compression: 'gzip'|'identity', fightName: string|null,
 *              playerName: string|null, heroId: string|null, cachedAt: number (ms since epoch),
 *              expiresAt: number|null (ms since epoch; null = never expires) }
 *   Store "history": same key, same metadata minus eventsBlob (for listing without loading events)
 *     value: { reportCode, fightId, playerId, fightName, playerName, heroId, cachedAt }
 *   Store "masterdata": keyed by reportCode
 *     value: { masterDataJson: string }
 *
 *   Max 20 entries are kept; oldest (by cachedAt) are evicted on insert.
 * Version 4: added expiresAt to events entries.
 * Version 5: clears events/history; earlier broken server iterations returned
 *   double-encoded payloads that were cached as if they were plain JSON. Those
 *   entries hang on decompress and must be evicted.
 * Version 6: clears events/history; entries now hold the merged player-events + fight-scoped
 *   death stream. Earlier entries carry only the player's own kills, an incomplete death set.
 */

const DB_NAME = 'fellowship-analyzer';
const DB_VERSION = 6;
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

            // Version 3 changes cached events from raw JSON strings to compressed binary blobs.
            // Old entries cannot be read without reintroducing the giant-string path, so evict them.
            if (event.oldVersion > 0 && event.oldVersion < 3) {
                if (db.objectStoreNames.contains(EVENTS_STORE)) {
                    event.target.transaction.objectStore(EVENTS_STORE).clear();
                }
                if (db.objectStoreNames.contains(HISTORY_STORE)) {
                    event.target.transaction.objectStore(HISTORY_STORE).clear();
                }
            }
            // Version 4 adds expiresAt to events entries — existing entries remain valid
            // (they simply have no expiry and will be served until evicted by LRU).

            // Version 5: evict events/history that may contain double-gzipped payloads
            // produced by earlier broken server iterations.
            // Version 6: evict player-only event streams cached before deaths were merged in.
            if (event.oldVersion > 0 && event.oldVersion < 6) {
                if (db.objectStoreNames.contains(EVENTS_STORE)) {
                    event.target.transaction.objectStore(EVENTS_STORE).clear();
                }
                if (db.objectStoreNames.contains(HISTORY_STORE)) {
                    event.target.transaction.objectStore(HISTORY_STORE).clear();
                }
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

function toUint8Array(bytes) {
    if (bytes instanceof Uint8Array) return bytes;
    if (bytes instanceof ArrayBuffer) return new Uint8Array(bytes);
    if (Array.isArray(bytes)) return new Uint8Array(bytes);
    if (ArrayBuffer.isView(bytes)) {
        return new Uint8Array(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    }
    throw new TypeError('Expected a Uint8Array-compatible value.');
}

async function compressBytes(bytes) {
    const data = toUint8Array(bytes);
    if (!('CompressionStream' in globalThis)) {
        return {
            blob: new Blob([data], { type: 'application/json' }),
            compression: 'identity'
        };
    }

    const compressedStream = new Blob([data])
        .stream()
        .pipeThrough(new CompressionStream('gzip'));

    return {
        blob: await new Response(compressedStream).blob(),
        compression: 'gzip'
    };
}

async function decompressBlob(blob, compression) {
    if (compression === 'gzip') {
        if (!('DecompressionStream' in globalThis)) {
            throw new Error('This browser cannot decompress cached gzip event data.');
        }

        const decompressedStream = blob
            .stream()
            .pipeThrough(new DecompressionStream('gzip'));
        return new Uint8Array(await new Response(decompressedStream).arrayBuffer());
    }

    return new Uint8Array(await blob.arrayBuffer());
}

/**
 * Retrieve cached UTF-8 events JSON bytes for the given key.
 * Returns null on cache miss or if the entry has exceeded its server-provided expiry.
 * @returns {Promise<Uint8Array|null>} Raw events JSON bytes, or null on miss/expired.
 */
export async function getCachedEventsBytes(reportCode, fightId, playerId) {
    const db = await openDb();
    const key = `${reportCode}/${fightId}/${playerId}`;
    const t = txn(db, [EVENTS_STORE, HISTORY_STORE], 'readwrite');
    const entry = await promisify(t.objectStore(EVENTS_STORE).get(key));
    if (!entry?.eventsBlob) return null;

    // Respect server-provided expiry: treat as a miss and evict if the entry has expired.
    if (entry.expiresAt != null && Date.now() >= entry.expiresAt) {
        t.objectStore(EVENTS_STORE).delete(key);
        t.objectStore(HISTORY_STORE).delete(key);
        return null;
    }

    try {
        return await decompressBlob(entry.eventsBlob, entry.compression ?? 'identity');
    } catch (err) {
        // Corrupt/incompatible cache entry — evict and treat as a miss so the
        // caller can fall back to the network instead of hanging on a broken stream.
        console.warn('[report-cache] decompress failed; evicting cached entry', { key, err });
        try {
            const evictTxn = txn(db, [EVENTS_STORE, HISTORY_STORE], 'readwrite');
            evictTxn.objectStore(EVENTS_STORE).delete(key);
            evictTxn.objectStore(HISTORY_STORE).delete(key);
        } catch {
            // Best-effort cleanup.
        }
        return null;
    }
}

/**
 * Store events + metadata for a completed fight.
 * Evicts the oldest entry when the store exceeds MAX_ENTRIES.
 *
 * @param {string} reportCode
 * @param {number} fightId
 * @param {number} playerId
 * @param {Uint8Array} eventsJsonBytes    UTF-8 EventsResult JSON bytes
 * @param {string|null} fightName
 * @param {string|null} playerName
 * @param {string|null} heroId
 * @param {number|null} expiresAtMs  Server-provided expiry as epoch milliseconds, or null.
 */
export async function cacheEventsBytes(reportCode, fightId, playerId, eventsJsonBytes, fightName, playerName, heroId, expiresAtMs) {
    const db = await openDb();
    const key = `${reportCode}/${fightId}/${playerId}`;
    const cachedAt = Date.now();
    const { blob: eventsBlob, compression } = await compressBytes(eventsJsonBytes);
    const expiresAt = (expiresAtMs != null && expiresAtMs > 0) ? expiresAtMs : null;

    const meta = { reportCode, fightId, playerId, fightName, playerName, heroId, cachedAt };

    const t = txn(db, [EVENTS_STORE, HISTORY_STORE], 'readwrite');
    const eventsStore = t.objectStore(EVENTS_STORE);
    const historyStore = t.objectStore(HISTORY_STORE);

    eventsStore.put({ eventsBlob, compression, expiresAt, ...meta }, key);
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
