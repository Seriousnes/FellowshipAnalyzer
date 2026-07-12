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
@namespace FellowshipAnalyzer.Heroes.{Hero}.Guides
@inject {Hero}CombatLogParser Parser

<GuideSection Title="{Feature Name}">
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
        Parser.{Name}Analyzers.Select(entry => new PerCastData
        {
            Timestamp = Parser.FormatTimestamp(entry.Pull.StartTime),
            Group = entry.Pull.Name,
            Performance = entry.Analyzer.GoodShare >= 0.75 ? PerformanceTier.Good : PerformanceTier.Fail,
        });
}
```

For a fight-lifetime module, read the generated nullable parser property (`Parser.WinterOrbTracker` style) and null-check it.

`_Imports.razor` should include the hero `Modules` namespace, as Rime does, so analyzer types are available to guides.

### 2. Add To The Hero Root Guide

Each hero has a root guide component at `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/{Hero}Guide.razor`.

```razor
@namespace FellowshipAnalyzer.Heroes.{Hero}.Guides
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

From `FellowshipAnalyzer.Components`:

| Component | Purpose |
|-----------|---------|
| `GuideSection` | Titled collapsible guide section wrapper. |
| `CastOverview` | Summary stats across all occurrences. |
| `CastDetail` | Per-cast breakdown with performance boxes and optional sequence/details. |
| `GradiatedPerformanceBar` | Color-graded performance bar. |
| `PassFailBar` | Binary pass/fail bar. |
| `PerformanceBoxRow` | Row of colored performance boxes. |
| `SpellSequence` | Filmstrip of spell casts. |
| `StackedBar` | Stacked horizontal bar chart. |
| `StatCard` | Statistics card container; normally used by statistics components. |

## Key Rules

- Guide components go in `Guides/`.
- The hero root guide lives at the hero project root as `{Hero}Guide.razor`.
- Inject the hero parser with `@inject`.
- Read pull analyzers via `Parser.{Name}Analyzers` / `Parser.For(pull).{Name}` and fight-lifetime modules via generated properties such as `Parser.WinterOrbTracker`.
- Keep event-derived state in modules; keep prose, severity wording, and `PerformanceTier` mapping here.
- Use shared components from `FellowshipAnalyzer.Components` when possible.
- Use the `style-guide` skill before adding or changing component styles.

## Checklist

- [ ] File is at `Guides/{Name}Guide.razor`.
- [ ] Component injects the hero parser.
- [ ] Component reads analyzer state via the generated read paths.
- [ ] Feature guide is added to `{Hero}Guide.razor` with a gate.
- [ ] Parser `GuideComponent` points to the root guide.
