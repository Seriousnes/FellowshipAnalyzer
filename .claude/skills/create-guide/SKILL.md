---
name: create-guide
description: "Create a Guide Razor component for a FellowshipAnalyzer analyzer. Use when: adding a guide section to the Guide tab, creating a per-analyzer guide view, or composing the hero's main Guide.razor page."
---

# Create Guide Component

A guide component is a Razor file in `Guides/` that renders analyzer state in the Guide tab. It injects the hero parser and reads analyzer instances directly - fight-lifetime modules via generated parser properties, pull-lifetime analyzers via the generated pull read paths.

The analyzer holds typed data (counts, rates, timestamps, typed entry records); the guide owns all presentation: prose, severity wording, and `PerformanceTier` judgments. Display-shaping helpers that turn analyzer state into shared component inputs live in the guide's `@code` block.

Reference implementation: `src/Heroes/FellowshipAnalyzer.Heroes.Tariq/Guides/FuryEconomyGuide.razor`.

## Procedure

### 1. Create The Feature Guide

Place at `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/Guides/{Name}Guide.razor`.

For a pull-lifetime analyzer (registered with `[AddAnalyzer<T>]`), read the cross-pull stream `Parser.{Name}Analyzers` - a list of `(Pull, Analyzer)` pairs:

```razor
@inject {Hero}CombatLogParser Parser

<GuideSection Title="{Feature Name}">
    <Explanation>
        <p>What this section measures and why it matters. Prose belongs here, in the guide layer.</p>
    </Explanation>
    <ChildContent>
        <CastOverview Title="Overview" Stats="@BuildOverviewStats()" />
        <CastDetail Title="Per-Pull {Feature Name}" Casts="@BuildPerPullData()" />
    </ChildContent>
</GuideSection>

@code {
    private IEnumerable<OverviewStat> BuildOverviewStats()
    {
        var analyzers = Parser.{Name}Analyzers.Select(entry => entry.Analyzer).ToList();
        var good = analyzers.Sum(analyzer => analyzer.GoodCount);

        return
        [
            new OverviewStat($"{good}", "Good", "Successful usages across all pulls."),
        ];
    }

    private IEnumerable<PerCastData> BuildPerPullData() =>
        Parser.{Name}Analyzers.ToPullRows(Parser, (analyzer, pull) => new PerCastRow
        {
            Performance = PerformanceTiers.FromThresholds(analyzer.GoodSharePercent, 75, 50, 25),
            Stats = [new PerCastStat($"{analyzer.GoodCount}", "Good", "Successful usages this pull.")],
        });
}
```

Project rows with the `ToPullRows` / `ToItemRows` extensions (in `FellowshipAnalyzer.Core.UI.Guides`) rather than hand-building each `PerCastData`. The row builder returns a `PerCastRow` with only the varying fields (`Performance`, `Stats`, and optionally `Sequence` / `AdditionalContent` / `Details` / `Tooltip`); the extension fills the pull-derived grouping (the `FormatTimestamp` label, `Group` name, and `PullBanner`) for you. Use `ToPullRows` for one row per analyzer (a per-pull aggregate); use `ToItemRows(Parser, a => a.Windows, (window, pull) => ...)` to flatten an inner collection into one row per item. When a row's timestamp is not the pull start (e.g. a window start), set `PerCastRow.Timestamp`. Map thresholds to a `PerformanceTier` with `PerformanceTiers.FromThresholds(value, perfect, good, ok)` instead of hand-writing the ladder.

For a fight-lifetime module, read the generated nullable parser property (`Parser.WinterOrbTracker` style) and null-check it.

`_Imports.razor` should include the hero `Modules` namespace, as Rime does, so analyzer types are available to guides.

### Merging analyzers across pull shapes

When independent analyzers answer different questions for different pull shapes (e.g. boss DoT uptime vs trash DoT spread), expose them under one surface so the guide reads a single stream. Give both analyzers a shared **surface marker interface** (no shared base class or behaviour):

```csharp
public interface ISearingBlazeAnalyzer : IAnalyzerSurface;

[ForPull(PullKind.Single, Boss = PullBoss.Boss)]
public sealed partial class SearingBlazeUptimeAnalyzer : Analyzer, ISearingBlazeAnalyzer { /* ... */ }

[ForPull(PullKind.Multi)]
public sealed partial class SearingBlazeSpreadAnalyzer : Analyzer, ISearingBlazeAnalyzer { /* ... */ }
```

Both register with their own `[AddAnalyzer<T>]` on the parser and keep their own `[ForPull]` (which must be disjoint - FA0016 enforces it). The generator then emits one `Parser.SearingBlazeAnalyzers` stream and one `pull.SearingBlazeAnalyzer` accessor, both typed as the interface. The guide reads the single stream and switches on the concrete type per row:

```csharp
private IEnumerable<PerCastData> BuildPerCastData() =>
    Parser.SearingBlazeAnalyzers.ToPullRows(Parser, (analyzer, pull) => analyzer switch
    {
        SearingBlazeUptimeAnalyzer uptime => BuildBossRow(uptime),
        SearingBlazeSpreadAnalyzer spread => BuildTrashRow(spread),
        _ => throw new InvalidOperationException($"Unexpected {analyzer.GetType().Name}"),
    });
```

Each per-shape builder returns a `PerCastRow` from its own analyzer's members; overview stats partition the one stream by concrete type (`.OfType<SearingBlazeUptimeAnalyzer>()`). Gate the section in the root guide on the single stream being non-empty. Reference: `SearingBlazeGuide.razor` in Ardeos.

For a shared-surface family that *does* share machinery (one abstract base, shape-specialized subclasses; see create-analyzer), read the single merged stream and pattern-match the evaluation subtypes per row instead. Reference: `WintersEmbraceGuide.razor` in Rime.

### 2. Add To The Hero Root Guide

Each hero has a root guide component at `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/{Hero}Guide.razor`.

```razor
@using FellowshipAnalyzer.Heroes.{Hero}.Analysis
@inject {Hero}CombatLogParser Parser

@if (Parser.{Name}Analyzers.Count > 0)
{
    <{Name}Guide />
}
```

Gate pull-analyzer guides on a non-empty stream; null-check generated module properties for fight-lifetime modules (modules may be inactive).

### 3. Ensure The Parser Points To The Root Guide

The hero parser exposes the root guide through `GuideComponent`:

```csharp
public override Type? GuideComponent => typeof({Hero}Guide);
```

## Available UI Widgets

From `FellowshipAnalyzer.Core.UI.Guides`:

| Component | Purpose |
|-----------|---------|
| `GuideSection` | Titled two-column guide section: `Explanation` prose on the left, `ChildContent` data on the right, split by `ExplanationPercent` (default 40). |
| `CastOverview` | Summary stats across all occurrences. |
| `CastDetail` | Per-cast breakdown with performance boxes and optional sequence/details. |
| `GradiatedPerformanceBar` | Color-graded performance bar. |
| `PassFailBar` | Binary pass/fail bar. |
| `PerformanceBoxRow` | Row of colored performance boxes. |
| `SpellSequence` | Filmstrip of spell casts. |
| `StackedBar` | Stacked horizontal bar chart. |

`StatCard` (statistics card container, normally used by statistics components) lives in `FellowshipAnalyzer.Core.UI.Components`, not in `.UI.Guides`.

## Key Rules

- Guide components go in `Guides/`.
- The hero root guide lives at the hero project root as `{Hero}Guide.razor`.
- Inject the hero parser with `@inject`.
- Read pull analyzers via `Parser.{Name}Analyzers`, `Parser.For(pull).{Name}Analyzer` or the `pull.{Name}Analyzer` extension (the member is named after the surface type, with a leading `I` stripped for a marker interface). Read fight-lifetime modules via generated properties such as `Parser.WinterOrbTracker`, where the `Analyzer` suffix is stripped.
- Keep event-derived state in modules; keep prose, severity wording, and `PerformanceTier` mapping here.
- Project per-pull rows with the `ToPullRows` / `ToItemRows` extensions returning `PerCastRow`; use `PerformanceTiers.FromThresholds` for tier ladders.
- Use shared components from `FellowshipAnalyzer.Core.UI.Guides` when possible.
- Use the `style-guide` skill before adding or changing component styles.

## Checklist

- [ ] File is at `Guides/{Name}Guide.razor`.
- [ ] Component injects the hero parser.
- [ ] Component reads analyzer state via the generated read paths.
- [ ] Feature guide is added to `{Hero}Guide.razor` with a gate.
- [ ] Parser `GuideComponent` points to the root guide.
