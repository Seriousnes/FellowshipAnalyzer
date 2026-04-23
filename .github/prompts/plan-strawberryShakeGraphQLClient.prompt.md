# Plan: Strawberry Shake GraphQL Client

**TL;DR**: Replace the entire manual GraphQL HTTP stack (`BaseFunction`, `ReportFunction`, `EventsFunction`, `ApiRequestExecutor`, `FellowshipLogsResponseModels`, `ApiClient`) with Strawberry Shake source-generated typed operations. Three `.graphql` operation files define the query surface; a `FellowshipLogsGraphQLWrapper` maps SB-generated result types to existing domain types. Server proxy endpoints change from streaming raw bytes to returning serialized domain JSON, simplifying the WASM proxy client significantly.

---

## Steps

### Phase 1 — Schema & Operations (independent of each other)

**Step 1 — `schema.graphql`** at `FellowshipAnalyzer.FellowshipLogs/schema.graphql`
- Minimal SDL covering only the fields we query: `Query → reportData → report`, `Report`, `ReportFight`, `ReportDungeonPull`, `ReportEventPaginator`, `ReportMasterData`, `ReportAbility`, `ReportActor`, plus scalars `Float Boolean Int String JSON`
- Source: hand-written from the fields confirmed in `schema.json` introspection

**Step 2 — `schema.extensions.graphql`** at same location
- SB client config via `extend schema @strawberryShake(name: "FellowshipLogsApiClient", namespace: "FellowshipAnalyzer.FellowshipLogs.API", accessibility: "internal")`
- JSON scalar binding: `scalar JSON @binding(name: "System.Text.Json.JsonElement")`

**Step 3 — `API/GraphQL/GetReport.graphql`** *(parallel with Steps 4–5)*
- Replaces `ReportQueryString` — selects title, startTime, endTime, fights (full field set), masterData actors (for boss icon lookup)

**Step 4 — `API/GraphQL/GetEvents.graphql`**
- Replaces `EventsFunction.Query` — fight status subset + events data

**Step 5 — `API/GraphQL/GetAnalysisPreload.graphql`** *(supersedes the separate MasterData query)*
- Combined query: title + startTime + endTime + fights (full) + masterData (abilities + actors)
- This is the single query `Report.razor` will use, eliminating the current two-request pattern

**Step 6 — Update `.csproj`**
- Add `<PackageReference Include="StrawberryShake" Version="16.0.0-rc.1.40" />`
- Add `<GraphQL Include="schema.graphql" />`, `<GraphQL Include="schema.extensions.graphql" />`, `<GraphQL Include="API/GraphQL/**/*.graphql" />` ItemGroup

---

### Phase 2 — Auth Extraction (*parallel with Phase 1*)

**Step 7 — Extract `ClientCredentialsTokenCache`** to `API/ClientCredentialsTokenCache.cs`
- Remove dependency on `IApiRequestExecutor`; accept `HttpClient` directly (named `"FellowshipLogsAuth"`)
- OAuth POST logic stays identical — `PostAsync` + `JsonSerializer.DeserializeAsync<OAuthTokenResponse>`

**Step 8 — Create `API/BearerTokenHandler.cs`**
- `DelegatingHandler` accepting `ClientCredentialsTokenCache`
- Sets `Authorization: Bearer {token}` on every outgoing SB HTTP request

---

### Phase 3 — New Domain Types (*parallel with Phases 1–2*)

**Step 9 — Update `IFellowshipLogsClient.cs`** (in Core)
- Add `public sealed record FellowshipLogsAnalysisPreload(FellowshipLogsReportInfo ReportInfo, FellowshipLogsMasterData MasterData)`
- Add `public interface IAnalysisPreloadFunction { Task<FellowshipLogsAnalysisPreload> GetAsync(string reportCode, CancellationToken ct = default); }`
- Add `IAnalysisPreloadFunction AnalysisPreload { get; }` property to `IFellowshipLogsClient`

---

### Phase 4 — SB Client Wrapper (*depends on Phase 1 build completing, Phase 2, Phase 3*)

**Step 10 — Create `API/FellowshipLogsGraphQLWrapper.cs`**
- `internal sealed class FellowshipLogsGraphQLWrapper : IFellowshipLogsClient`
- Constructor accepts the SB-generated `IFellowshipLogsApiClient`
- `IReportFunction.GetAsync` → calls `client.GetReportAsync(code)` → maps SB result to `FellowshipLogsReportInfo`
- `IMasterDataFunction.GetAsync` → calls `client.GetAnalysisPreloadAsync(code)` → maps masterData portion only
- `IAnalysisPreloadFunction.GetAsync` → calls `client.GetAnalysisPreloadAsync(code)` → maps both portions to `FellowshipLogsAnalysisPreload`
- `IEventsFunction.GetAsync` → calls `client.GetEventsAsync(code, fightIDs, sourceID)` → reads `events.data` as `JsonElement`, deserializes polymorphic events via `JsonSerializer.Deserialize<List<Event>>(data.GetRawText(), _jsonOptions)` with `WCLJsonConverter` — returns `EventsResult(events, inProgress)`
- The `WCLJsonConverter`-enabled `JsonSerializerOptions` is injected from DI (same instance already registered in `ServiceCollectionExtensions.cs`)

---

### Phase 5 — DI & Proxy Wiring (*depends on Phase 4*)

**Step 11 — Update `ServiceCollectionExtensions.cs`**
- Remove: `"FellowshipLogs"` and `"FellowshipLogsProxy"` named HttpClient registrations, `IApiRequestExecutor`, `ApiClient` registrations
- Add: `"FellowshipLogsAuth"` named HttpClient (for OAuth token endpoint)
- Register `ClientCredentialsTokenCache` as scoped, inject `"FellowshipLogsAuth"` client
- Register `BearerTokenHandler` as transient
- SB registration: `services.AddFellowshipLogsApiClient().ConfigureHttpClient(c => c.BaseAddress = new Uri(options.GraphQlEndpoint)).AddHttpMessageHandler<BearerTokenHandler>()`
- Register `IFellowshipLogsClient` → `FellowshipLogsGraphQLWrapper` (scoped)

**Step 12 — Delete old files** (after Step 11 compiles)
- `API/ApiRequestExecutor.cs`, `API/FellowshipLogsResponseModels.cs`, `API/ApiClient.cs`
- `API/Functions/` directory (all 4 files)
- `IFellowshipLogsProxy.cs`

---

### Phase 6 — Server & WASM Adaptation (*parallel after Phase 5 builds clean*)

**Step 13 — Update `FellowshipAnalyzer.Api/Program.cs`** *(parallel with Step 14)*
- Remove `IFellowshipLogsProxy` injection from endpoints
- `GET /api/report/{reportCode}` → inject `IFellowshipLogsClient`, call `client.Report.GetAsync(...)`, `Results.Json(result, jsonOptions)`
- `GET /api/events?...` → call `client.Events.GetAsync(...)`, `Results.Json(result, jsonOptions)`
- `GET /api/masterdata/{reportCode}` → call `client.MasterData.GetAsync(...)`, `Results.Json(result, jsonOptions)`
- Add `GET /api/analysis/{reportCode}` → call `client.AnalysisPreload.GetAsync(...)`, `Results.Json(result, jsonOptions)`

**Step 14 — Simplify `FellowshipLogsProxyClient.cs`** (WASM)
- All four inner functions become simple `GetFromJsonAsync<T>(..., jsonOptions, ct)` calls
- No more `GraphQLResponse<GraphQLReportResponse>` wrapper deserialization — gets domain types directly
- Wire `AnalysisPreload = new ProxyAnalysisFunction(http, jsonOptions)`
- `EventsResult` deserialization uses existing `jsonOptions` (which already includes `WCLJsonConverter`) — polymorphic events still work

**Step 15 — Update `Report.razor`**
- Inject `ReportNavigationState NavState`
- Check `NavState.TryGet(ReportCode, out var preload)` first (deep-link warmup)
- On miss: `var preload = await FellowshipLogs.AnalysisPreload.GetAsync(ReportCode)` → stores both `ReportInfo` and `MasterData` in a single call
- Replace `var reportTask = FellowshipLogs.Report.GetAsync(...)` + `var masterDataTask = FellowshipLogs.MasterData.GetAsync(...)` + `await both`

---

## Relevant Files

| File | Change |
|---|---|
| `FellowshipAnalyzer.FellowshipLogs/schema.graphql` | **New** — minimal SDL |
| `FellowshipAnalyzer.FellowshipLogs/schema.extensions.graphql` | **New** — SB client config + JSON scalar binding |
| `FellowshipAnalyzer.FellowshipLogs/API/GraphQL/GetReport.graphql` | **New** |
| `FellowshipAnalyzer.FellowshipLogs/API/GraphQL/GetEvents.graphql` | **New** |
| `FellowshipAnalyzer.FellowshipLogs/API/GraphQL/GetAnalysisPreload.graphql` | **New** |
| `FellowshipAnalyzer.FellowshipLogs/FellowshipAnalyzer.FellowshipLogs.csproj` | Add SB package + GraphQL items |
| `FellowshipAnalyzer.FellowshipLogs/API/ClientCredentialsTokenCache.cs` | **New** — extracted, HttpClient-based |
| `FellowshipAnalyzer.FellowshipLogs/API/BearerTokenHandler.cs` | **New** |
| `FellowshipAnalyzer.FellowshipLogs/API/FellowshipLogsGraphQLWrapper.cs` | **New** — SB result mapper |
| `FellowshipAnalyzer.FellowshipLogs/Extensions/ServiceCollectionExtensions.cs` | Complete rewrite of registrations |
| `FellowshipAnalyzer.Core/FellowshipLogs/IFellowshipLogsClient.cs` | Add preload record + interface + property |
| `FellowshipAnalyzer/FellowshipAnalyzer.Api/Program.cs` | Inject `IFellowshipLogsClient`, add `/api/analysis/` endpoint |
| `FellowshipAnalyzer/FellowshipAnalyzer.Client/Services/FellowshipLogsProxyClient.cs` | Simplify all inner classes |
| `FellowshipAnalyzer/FellowshipAnalyzer.Client/Pages/Report.razor` | Single preload call |
| `API/ApiRequestExecutor.cs` | **Deleted** |
| `API/FellowshipLogsResponseModels.cs` | **Deleted** |
| `API/ApiClient.cs` | **Deleted** |
| `API/Functions/` (4 files) | **Deleted** |
| `IFellowshipLogsProxy.cs` | **Deleted** |

---

## Verification

1. `dotnet build FellowshipAnalyzer.slnx` — zero errors (SB source generator emits typed client)
2. SB generates `IFellowshipLogsApiClient` with `GetReportAsync`, `GetEventsAsync`, `GetAnalysisPreloadAsync` — confirm in `obj/` generated code
3. Run via Aspire; deep-link to `/report/{code}/{fight}/{player}` — DevTools: exactly 2 requests (`/api/analysis/...` + `/api/events?...`)
4. Navigate breadcrumb to `/report/{code}` — no network request (NavState hit)
5. Fresh navigation to `/report/{code}` — 1 request to `/api/analysis/`, fight groups and boss icons render
6. Confirm `events.data` polymorphic deserialization — at least one `CastEvent` and one `BuffEvent` appear in Timeline tab

---

## Decisions

- `GetMasterData.graphql` is not created — `GetAnalysisPreload` covers all its fields; `IMasterDataFunction.GetAsync` on the wrapper calls the preload query and discards the report portion (one API call, extra fields are negligible)
- `ReportInfo.razor` continues calling `FellowshipLogs.Report.GetAsync` on cache miss — it over-fetches masterData actors, which is acceptable since NavState is warm in the common path
- `GetReport.graphql` still exists for `ReportInfo.razor`'s cold-path fetch (report list page)
- The `FellowshipLogsFixtureDeserializer.cs` test helper is unaffected — it deserializes event JSON directly and does not touch the GraphQL layer
