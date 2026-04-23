# Plan: Pull/Time Filter for FellowshipAnalyzer Report Page

`dungeonPulls` is already queried in GraphQL but silently dropped during deserialization. The filter runs display-time only via a `CascadingValue<AnalysisFilter?>` — no analysis re-run, no module changes.

---

## Phase 1 — Wire DungeonPulls Through the Data Model
*(parallel with Phase 2)*

1. **Add `FellowshipLogsDungeonPull` record** to `src/FellowshipAnalyzer.Core/FellowshipLogs/IFellowshipLogsClient.cs`:
   - Properties: `int EncounterId`, `double StartTime`, `double EndTime`, `string Name`
   - Boss = `EncounterId != 0`

2. **Update `FellowshipLogsFight`** (same file) — add `IReadOnlyList<FellowshipLogsDungeonPull>? DungeonPulls = null`

3. **Add internal `FellowshipLogsReportDungeonPull`** to `src/FellowshipAnalyzer.FellowshipLogs/API/FellowshipLogsResponseModels.cs` with `[JsonPropertyName]` attributes (`encounterID`, `startTime`, `endTime`, `name`). Add `List<FellowshipLogsReportDungeonPull>? DungeonPulls` to `FellowshipLogsReportFight`.

4. **Update fight mapping** in `src/FellowshipAnalyzer.FellowshipLogs/API/Functions/ReportFunction.cs` — pass `DungeonPulls` through the `select` projection.

5. **Check `src/FellowshipAnalyzer.FellowshipLogs.Http/`** for its own internal response models — update if present, otherwise add `[JsonPropertyName("dungeonPulls")]` to the public record. *(potential blocker — structure not yet confirmed)*

---

## Phase 2 — Analysis Filter Record
*(parallel with Phase 1)*

6. **Add `AnalysisFilter` record** to `src/FellowshipAnalyzer.Core/Analysis/`:
   ```csharp
   /// <summary>Times are report-relative milliseconds, matching Event.Timestamp.</summary>
   public sealed record AnalysisFilter(long StartMs, long EndMs);
   ```

---

## Phase 3 — Filter UI Component
*(depends on Phases 1 + 2)*

7. **Add `PullFilter.razor`** (+ `.css`) to `src/FellowshipAnalyzer.Components/`:
   - `PullOption` record inlined (not a separate file): `(string Name, bool IsBoss, long StartMs, long EndMs)`
   - Parameters: `IReadOnlyList<PullOption>? Pulls`, `long FightStartMs`, `long FightEndMs`, `EventCallback<AnalysisFilter?> FilterChanged`
   - "By Pull" tab: "All Pulls" (null) + individual pulls with boss pulls visually distinct
   - "By Time" tab: M:SS start/end inputs, converts to report-relative ms on submit

8. **Wire into `Report.razor`**:
   - Add `AnalysisFilter? _activeFilter` field
   - After fight is resolved, store `_dungeonPulls = fight.DungeonPulls`
   - Render `<PullFilter>` above the tab nav, mapped from `_dungeonPulls`
   - Wrap tab content in `<CascadingValue Value="@_activeFilter">` — modules opt in via `[CascadingParameter] AnalysisFilter? ActiveFilter` when they're ready

---

## Relevant Files

| File | Change |
|---|---|
| `src/FellowshipAnalyzer.Core/FellowshipLogs/IFellowshipLogsClient.cs` | Add `FellowshipLogsDungeonPull`, update `FellowshipLogsFight` |
| `src/FellowshipAnalyzer.FellowshipLogs/API/FellowshipLogsResponseModels.cs` | Add internal pull model + update fight model |
| `src/FellowshipAnalyzer.FellowshipLogs/API/Functions/ReportFunction.cs` | Update fight projection to include DungeonPulls |
| `src/FellowshipAnalyzer.FellowshipLogs.Http/` | Investigate + update if needed |
| `src/FellowshipAnalyzer.Core/Analysis/AnalysisFilter.cs` | New file |
| `src/FellowshipAnalyzer.Components/PullFilter.razor` (+ `.css`) | New component |
| `src/FellowshipAnalyzer/FellowshipAnalyzer.Client/Pages/Report.razor` | Filter state + `<PullFilter>` + `CascadingValue` |

---

## Key Decisions

- `AnalysisFilter` uses report-relative ms (matches `Event.Timestamp` values); "By Time" UI shows fight-relative M:SS and converts on input
- Module filtering is opt-in later — existing modules continue showing full-fight data with null filter
- No boss phases in scope (Fellowship is Mythic+ style; `PhaseEvent`/`PhaseConfig` infrastructure already exists but is not applicable here)
- `PullOption` is inlined in `PullFilter.razor`, not a separate file

---

## Verification

1. `dotnet build` — no errors
2. Load a dungeon report → pull dropdown appears, individual pulls listed, boss pulls visually distinct
3. "All Pulls" resets filter to null, modules show unchanged full-fight data
4. "By Time" inputs produce correct report-relative ms in `AnalysisFilter`
