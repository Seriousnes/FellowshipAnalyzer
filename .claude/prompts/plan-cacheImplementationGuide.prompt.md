# Plan: FellowshipLogs Caching Architecture (Refined)

Introduce a **server-side persistent cache layer** behind the existing `IMemoryCache` in `FellowshipAnalyzer.Api.Core`, exposed through a single DI-injected interface (`IPersistentCache`). The interface lives in `Api.Core` and is consumed by the source-generated endpoint adapters and `FellowshipLogsApiHandler`. Two implementations:

- **`DevApi`**: real `BlobPersistentCache` pointed at the **Azurite emulator** orchestrated by Aspire — full prod parity in dev.
- **`Api`** (Azure Functions): same `BlobPersistentCache` pointed at a real Azure Storage account.

All three caching tiers (Game Data, Report Metadata, Report Events) use **Azure Blob Storage** uniformly. Cosmos DB is dropped: per-document TTL is not worth a second SDK, second cost line, and second emulator. TTL is implemented via blob metadata (`expiresAt` lazy-expire on read) and lifecycle management policies for bulk cleanup. Storage account stays Standard_LRS Cool tier where appropriate to keep cost near zero.

## Phase 1 — Shared abstraction in `Api.Core`

1. New folder `FellowshipAnalyzer.Api.Core/Caching/` containing:
   - `IPersistentCache` interface — partition + key based, stream-oriented to avoid buffering large event payloads:
     - `ValueTask<PersistentCacheEntry?> GetAsync(CachePartition partition, string key, CancellationToken ct)`
     - `ValueTask SetAsync(CachePartition partition, string key, ReadOnlyMemory<byte> bytes, PersistentCacheWriteOptions options, CancellationToken ct)`
     - `ValueTask SetStreamAsync(CachePartition partition, string key, Stream payload, PersistentCacheWriteOptions options, CancellationToken ct)`
     - `ValueTask RemoveAsync(CachePartition partition, string key, CancellationToken ct)`
   - `PersistentCacheEntry` record — `Stream Content`, `long Length`, `DateTimeOffset? ExpiresAt`, `string? ContentEncoding`, `IReadOnlyDictionary<string,string> Metadata`.
   - `PersistentCacheWriteOptions` record — `DateTimeOffset? ExpiresAt`, `string? ContentType`, `string? ContentEncoding`, `IReadOnlyDictionary<string,string>? Metadata`.
   - `CachePartition` enum — `GameData`, `Metadata`, `Events`. Maps 1:1 to a blob container name.
2. Add `BlobPersistentCache` (also in `Api.Core` so both hosts share it) — depends on `BlobServiceClient` (registered by the host). Implements:
   - **Read**: `BlobClient.DownloadStreamingAsync` → if `Metadata["expiresAt"]` in the past, return null and fire-and-forget delete; otherwise return entry with stream + content-encoding preserved.
   - **Write**: `BlobClient.UploadAsync(stream, new BlobUploadOptions { Metadata, HttpHeaders { ContentType, ContentEncoding } })`.
   - Container creation happens lazily once per partition via `BlobContainerClient.CreateIfNotExistsAsync` on first use, gated by an `AsyncLazy<>` per partition.
3. Update `FellowshipLogsServiceCollectionExtensions.AddFellowshipLogsApi` so it **does not** register `IPersistentCache` itself — leave that to each host. It can offer an optional `AddBlobPersistentCache(this IServiceCollection, Action<BlobPersistentCacheOptions>)` helper that both hosts call.
4. Update `CacheKeys` (or add `BlobCacheKeys`) so each key includes a stable, URL-safe form per partition.

*Steps 1–4 are sequential within Phase 1 but can land in one PR.*

## Phase 2 — Wire `BlobPersistentCache` into the request path

5. Modify `FellowshipLogsApiHandler` (`src/FellowshipAnalyzer/FellowshipAnalyzer.Api.Core/FellowshipLogsApiHandler.cs`) so each endpoint now layers:
   - **L1: `IMemoryCache`** (in-process; unchanged for `analysis`/`character`; **skip for `events`** — payloads too large to retain in process).
   - **L2: `IPersistentCache`** — new layer between memory cache and the upstream call.
   - **L3: upstream FellowshipLogs** via `FellowshipLogsService`.
6. On L3 hit, write back through L2 (and L1 where applicable) honouring the existing `FellowshipLogsCacheOptions` durations:
   - `GetEventsAsync`: write to `CachePartition.Events` only when `!InProgress`; preserve gzip end-to-end (set `ContentEncoding = "gzip"` on blob; serve `Content-Encoding: gzip` from the response).
   - `GetAnalysisAsync`: write to `CachePartition.Metadata` with `ExpiresAt = now + GetAnalysisPreloadCacheDuration(...)`.
   - `GetCharacterReportsAsync`: write to `CachePartition.Metadata` with `ExpiresAt = now + RecentReportMetadataCacheDuration`.
7. Game data endpoint (does not exist yet — currently abilities ship via `abilities.json`/local tool): defer creation, but reserve `CachePartition.GameData` and a `GET /api/gamedata/{patch}` route. Add an `[ApiEndpoint]`-decorated method only when the upstream game-data API is wired up.

*Step 5 blocks 6. Step 7 is parallel and optional.*

## Phase 3 — Stream optimization for events (Tier 3)

8. Refactor `FellowshipLogsService.GetRawEventsAsync` to expose a streaming write path. Two acceptable shapes:
   - **(a)** Keep existing `byte[]` return, add `WriteRawEventsAsync(Stream destination, ...)` overload that writes directly to a target stream.
   - **(b)** Always return an awaitable that produces `(Stream content, bool inProgress)` using a `RecyclableMemoryStream` so we don't allocate a fresh `byte[]` per request.
   - Recommend **(b)** with `Microsoft.IO.RecyclableMemoryStreamManager` (one new dependency, well-supported, minimal code change). Keeps the existing JSON reshape (`{inProgress, events:[…]}`) intact and lets us tee to both the HTTP response and the blob writer without double-buffering.
9. In `GetEventsAsync`, on cache miss + `!InProgress`:
   - Acquire a `RecyclableMemoryStream` from the manager.
   - Write the reshaped JSON into it once.
   - Compress to gzip into a second `RecyclableMemoryStream`.
   - Upload gzip stream to blob with `ContentEncoding = "gzip"`, `ContentType = "application/json"`.
   - Return the gzip stream to the client with matching `Content-Encoding` header.
10. On cache hit: `Results.Stream(entry.Content, "application/json")` after setting `Content-Encoding: gzip` from the entry metadata.

*Step 8 blocks 9–10.*

## Phase 4 — Aspire orchestration & host wiring

11. AppHost (`src/FellowshipAnalyzer.AppHost/AppHost.cs`):
    - Add NuGet `Aspire.Hosting.Azure.Storage`.
    - `var storage = builder.AddAzureStorage("storage").RunAsEmulator();`
    - `var blobs = storage.AddBlobs("blobs");`
    - Apply `.WithReference(blobs).WaitFor(blobs)` to the `fellowshipanalyzerapi` (DevApi) project.
12. `DevApi/Program.cs`:
    - `builder.AddAzureBlobClient("blobs");` (Aspire client integration).
    - `builder.Services.AddBlobPersistentCache(o => { o.GameDataContainer = "gamedata"; o.MetadataContainer = "metadata"; o.EventsContainer = "events"; });`
13. `Api/Program.cs` (Functions Isolated Worker):
    - `builder.AddAzureBlobClient("BlobsConnection");` (named connection string from Functions app settings; in prod use Managed Identity via `BlobServiceClient(Uri, DefaultAzureCredential)` overload).
    - Same `AddBlobPersistentCache(...)` call as DevApi.
14. Document in `appsettings.json` / `local.settings.json.example` that the connection string `BlobsConnection` is the cache backing store (separate from `AzureWebJobsStorage` for clarity, but can be aliased in dev).

*Step 11 enables 12. Step 13 is parallel with 12.*

## Phase 5 — Client (no architectural change required)

15. Confirm existing `IndexedDbReportCacheService` (`src/FellowshipAnalyzer/FellowshipAnalyzer/Services/IndexedDbReportCacheService.cs`) already covers the per-user IndexedDB story for events + master data. Only client change: when the API response includes a new `X-FellowshipAnalyzer-ExpiresAt` header (added by Phase 2), surface it through `ReportAnalysisService` and respect it on the IndexedDB read path.
16. No `Blazored.LocalStorage`/`Dexie` dependency — keep using the existing JS module + `IJSRuntime`.

## Out of scope / explicit exclusions

- **No Cosmos DB.** Dropped from the original plan; re-evaluate only if metadata payloads grow past the point where blob-metadata-based TTL becomes inefficient.
- **No game data endpoint implementation** in this work — only the partition + container reservation.
- **No multi-region / GZRS storage.** Single Standard_LRS account.
- **No CDN** in front of blobs; the Functions app remains the only egress path so we can add request-level rate limiting and authn later.
- **No changes to existing in-memory cache TTL logic** — the persistent cache uses the same durations from `FellowshipLogsCacheOptions`.

## Relevant files

- `src/FellowshipAnalyzer/FellowshipAnalyzer.Api.Core/FellowshipLogsApiHandler.cs` — add L2 lookups, write-through, switch events path to streaming + gzip.
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Api.Core/FellowshipLogsService.cs` — add streaming overload (`WriteRawEventsAsync` / return `Stream`).
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Api.Core/FellowshipLogsServiceCollectionExtensions.cs` — add `AddBlobPersistentCache` helper.
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Api.Core/CacheKeys.cs` — partition-aware key shaping.
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Api.Core/Caching/` — new folder for `IPersistentCache`, `BlobPersistentCache`, supporting records.
- `src/FellowshipAnalyzer/FellowshipAnalyzer.DevApi/Program.cs` — `AddAzureBlobClient("blobs")` + `AddBlobPersistentCache`.
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Api/Program.cs` — same registration; production connection via Managed Identity.
- `src/FellowshipAnalyzer.AppHost/AppHost.cs` — Azurite emulator + blob container references.
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Api.Core/FellowshipAnalyzer.Api.Core.csproj` — add `Azure.Storage.Blobs`, `Microsoft.IO.RecyclableMemoryStream`, `Aspire.Azure.Storage.Blobs`.
- `src/FellowshipAnalyzer.AppHost/FellowshipAnalyzer.AppHost.csproj` — add `Aspire.Hosting.Azure.Storage`.
- `src/FellowshipAnalyzer/FellowshipAnalyzer/Services/ReportAnalysisService.cs` — read new `ExpiresAt` response header.

## Verification

1. `dotnet build FellowshipAnalyzer.slnx` — clean build with new packages.
2. `dotnet test tests/FellowshipAnalyzer.Core.Tests` — existing suite stays green.
3. New unit tests in `tests/FellowshipAnalyzer.Core.Tests` (or new `Api.Core.Tests` project) covering `BlobPersistentCache`:
    - Round-trip write + read with metadata + content-encoding preserved.
    - Expired entry returns null on read and removes blob.
    - Stream write does not buffer entire payload in memory (assert via `RecyclableMemoryStreamManager` event counters).
4. Aspire run (`dotnet run --project src/FellowshipAnalyzer.AppHost`) — confirm Azurite container starts, three blob containers created lazily on first request.
5. Manual: hit `GET /api/analysis/{reportCode}` twice — second response has `X-FellowshipAnalyzer-Cache: HIT` and originates from blob (kill the in-process app between calls to bypass `IMemoryCache`).
6. Manual: hit `GET /api/events?...` for a completed fight; inspect `events` blob container in Azurite Explorer — single gzipped blob, correct metadata. Confirm second call returns `Content-Encoding: gzip` and matches blob bytes.
7. Functions deploy to a dev SWA slot; confirm `BlobServiceClient` resolves via `DefaultAzureCredential` and the Function app's managed identity has `Storage Blob Data Contributor` on the storage account.

## Decisions

- **DevApi implementation: Azurite via Aspire** (per user) — full prod parity, rejected `NullPersistentCache` and filesystem cache.
- **Storage backend: Azure Blob Storage uniformly** (per user, with cost framing) — Cosmos dropped.
- **Tier 3 (events): cache server-side too** (per user) — overrides original "pass-through only" guidance. Server stores gzip blobs, browser still decompresses.
- **TTL strategy**: per-blob `expiresAt` metadata + lazy expiration on read + container lifecycle policy for bulk cleanup. No background job.
- **Storage tiering for cost**: `gamedata` Hot (rarely changes, hit often), `metadata` Hot (small, hot), `events` Cool (large, single-user rereads dominated by client IndexedDB).
- **Production credentials**: Managed Identity (`DefaultAzureCredential`) — no connection strings in app settings.
