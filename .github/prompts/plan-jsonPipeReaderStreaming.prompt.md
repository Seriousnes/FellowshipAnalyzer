## Plan: JSON Streaming / PipeReader

### Streaming / PipeReader analysis

> **Conclusion: the proxy path is already optimal. True end-to-end streaming requires a protocol change that is probably not worthwhile.**

#### Current data flow

```
Fellowship Logs API
  → [gzip bytes, single JSON response]
  → FellowshipAnalyzer.Api  StreamUpstreamResponseAsync
      upstream.Content.CopyToAsync(ctx.Response.Body)   ← already raw passthrough, no buffering
  → Browser/WASM  FellowshipLogsProxyClient
      GetFromJsonAsync<GraphQLResponse<…>>               ← streams HTTP → STJ deserializer
      returns List<Event>                                ← must be fully materialized
  → CombatLogParser.AnalyzeAsync
      EventEmitter.DispatchEventsAsync(events)
          events.Sort(…)                                 ← requires full list before dispatch
```

#### Where `PipeReader`/`PipeWriter` could and could not help

| Layer | Could PipeReader help? | Verdict |
|---|---|---|
| **Fellowship Logs API → ASP.NET Core proxy** | `CopyToAsync` already uses `PipeWriter` internally via `HttpResponse.BodyWriter`; replacing it with an explicit `PipeReader`/`PipeWriter` copy loop would be a zero-benefit micro-optimisation | ❌ No meaningful gain |
| **ASP.NET Core proxy → browser** | ASP.NET Core already writes to `HttpResponse.BodyWriter` (a `PipeWriter`); `CopyToAsync(ctx.Response.Body)` resolves to the same pipe | ❌ Already optimal |
| **WASM `HttpClient` response → STJ deserializer** | `GetFromJsonAsync` calls `ReadFromJsonAsync` which calls `JsonSerializer.DeserializeAsync(stream, …)` — this is already zero-copy streaming from the HTTP response pipe into the JSON parser. The bottleneck is not buffering, it is that we must return `List<Event>` | ⚠️ Already streaming bytes, but list materialisation is unavoidable |
| **STJ `DeserializeAsyncEnumerable`** | Could yield `Event` objects as the JSON array is parsed token-by-token, avoiding the full `List<Event>` in memory before analysis starts | ✅ Theoretically possible, but blocked — see below |

#### Why `DeserializeAsyncEnumerable` is blocked

`EventEmitter.DispatchEventsAsync` calls `events.Sort(…)` before processing. Sorting requires the complete list. Fellowship Logs returns events in log order, which is **not guaranteed to be timestamp order** (normalizers reorder events). Therefore:
- Even if we parsed events as an `IAsyncEnumerable<Event>`, we could not begin dispatching until all events are collected.
- We would still need to materialise into `List<Event>` and sort — the same work as today.

Streaming deserialization would only reduce peak memory slightly (overlapping download + allocation), not change the algorithmic requirement.

#### What would be required for genuine streaming

The only scenario where streaming would provide real benefit is if the Fellowship Logs API returned events in a **stream-friendly format** (NDJSON / newline-delimited JSON, or Server-Sent Events) **and** the sort requirement was removed (i.e., Fellowship Logs guaranteed timestamp order). Neither is currently true.

Steps that would be required (out of scope, future investigation):

1. **Fellowship Logs API side**: request that the events endpoint supports `Accept: application/x-ndjson` or an SSE/chunked transfer response where each event is a standalone JSON line.
2. **Proxy passthrough**: `StreamUpstreamResponseAsync` already passes bytes through; would work unchanged.
3. **WASM deserializer**: replace `GetFromJsonAsync<GraphQLResponse<…>>` with `JsonSerializer.DeserializeAsyncEnumerable<Event>(stream, …)` reading from the raw response stream, yielding events as they arrive.
4. **Sort removal**: either trust Fellowship Logs event order, or buffer only the minimum needed for normalizers (potentially a sliding window), then dispatch in arrival order.
5. **`CombatLogParser` change**: accept `IAsyncEnumerable<Event>` in `AnalyzeAsync`, removing the `events.Sort()` call in `DispatchEventsAsync`.

This is a significant architectural change touching the upstream API contract, the proxy, the WASM client, and the analysis engine. It is not part of the JSON modernization plan.