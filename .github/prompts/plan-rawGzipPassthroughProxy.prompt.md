## Plan: Raw Gzip Passthrough Proxy

Server becomes a zero-deserialization streaming proxy: authenticate with Fellowship Logs, pipe the raw gzip bytes to the browser. A new WASM-side `IFellowshipLogsClient` implementation handles GraphQL unwrapping and reshaping. Razor pages stay clean — they inject `IFellowshipLogsClient` and get the same public types as today.

### Confirmed: SSR is disabled
App.razor uses `InteractiveWebAssemblyRenderMode(prerender: false)` — Report/ReportInfo pages only run in WASM. No risk of the server calling its own endpoints.

### Phase 1: Server — Streaming proxy

**Step 1:** New `FellowshipLogsProxy` service in `FellowshipAnalyzer.FellowshipLogs`
- Owns a non-decompressing HttpClient and token cache
- Contains GraphQL query strings (from ReportFunction/EventsFunction)
- Two methods: `ProxyReportAsync(reportCode, HttpContext)` and `ProxyEventsAsync(reportCode, playerId, fightId, HttpContext)`
- Each: builds GraphQL POST, adds Bearer token + Accept-Encoding: gzip, streams raw response bytes to HttpContext.Response.Body preserving Content-Encoding header

**Step 2:** Register in DI
- File: `ServiceCollectionExtensions.cs`
- Add named HttpClient `"FellowshipLogsProxy"` (no AutomaticDecompression)
- Register `FellowshipLogsProxy` as singleton

**Step 3:** Rewrite Program.cs endpoints
- `/api/report/{reportCode}` and `/api/events` call the proxy methods
- Forward upstream HTTP status codes on error

### Phase 2: WASM Client — New service (SRP)

**Step 4:** Add GraphQL wrapper types
- File: new `GraphQLResponseModels.cs` in `FellowshipAnalyzer.Core/FellowshipLogs/`
- Public versions of existing internal types: `GraphQLResponse<T>`, `GraphQLReportResponse`, `GraphQLReportData`, `GraphQLReport`, `GraphQLEventsData`, `GraphQLReportFight`, `GraphQLReportMasterData`, `GraphQLReportActor`, `GraphQLEventFightStatus`

**Step 5:** New `FellowshipLogsProxyClient` service
- File: new `FellowshipLogsProxyClient.cs` in `FellowshipAnalyzer.Client/Services/`
- Implements `IFellowshipLogsClient` (existing interface from Core)
- Calls `/api/report/{code}` and `/api/events?...` via HttpClient
- Deserializes GraphQL wrapper → reshapes into public types (FellowshipLogsReportInfo, EventsResult)
- Contains inner classes implementing IReportFunction and IEventsFunction

**Step 6:** Register in Client DI
- File: `FellowshipAnalyzer.Client/Program.cs`
- `builder.Services.AddScoped<IFellowshipLogsClient, FellowshipLogsProxyClient>();`

**Step 7:** Update Report.razor
- Inject `IFellowshipLogsClient` instead of `HttpClient` + `JsonSerializerOptions`
- Call `FellowshipLogs.Report.GetAsync()` and `FellowshipLogs.Events.GetAsync()`
- Return types unchanged — no other changes needed

**Step 8:** Update ReportInfo.razor
- Same: inject `IFellowshipLogsClient`, call `Report.GetAsync()`

### Phase 3: Cleanup (optional)

**Step 9:** Remove old server-side deserialization pipeline
- `ApiClient`, `ReportFunction`, `EventsFunction`, `ApiRequestExecutor`, internal `FellowshipLogsResponseModels.cs` become unused by server endpoints
- Can keep for testing or remove entirely
- `IFellowshipLogsClient` stays (used by both server and WASM client)

### Files to create
- `src/FellowshipAnalyzer.FellowshipLogs/FellowshipLogsProxy.cs`
- `src/FellowshipAnalyzer.Core/FellowshipLogs/GraphQLResponseModels.cs`
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Client/Services/FellowshipLogsProxyClient.cs`

### Files to modify
- `src/FellowshipAnalyzer.FellowshipLogs/Extensions/ServiceCollectionExtensions.cs` — register proxy + HttpClient
- `src/FellowshipAnalyzer/FellowshipAnalyzer/Program.cs` — rewrite endpoints
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Client/Program.cs` — register FellowshipLogsProxyClient
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Client/Pages/Report.razor` — inject IFellowshipLogsClient
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Client/Pages/ReportInfo.razor` — inject IFellowshipLogsClient

### Verification
1. `dotnet build` succeeds
2. Run app → DevTools Network → confirm `Content-Encoding: gzip` on `/api/*` responses
3. Confirm transfer size matches upstream (no inflate-then-recompress)
4. Verify analysis works end-to-end, IndexedDB caching still works

### Decisions
- Pagination dropped (single GraphQL page per request)
- SSR not a concern (`prerender: false` confirmed in App.razor)
- GraphQL wrapper types in Core (referenced by both server and client)
- Razor pages inject IFellowshipLogsClient — never see GraphQL shapes (SRP)
- Existing server-side IFellowshipLogsClient pipeline kept for now
