---
name: create-statistics
description: "Create an auto-collected statistics Razor component for a FellowshipAnalyzer analyzer. Use when: adding a statistics card to the Statistics tab, creating an overview statistic for an analyzer module."
---

# Create Statistics Component

A statistics component is a Razor file in `Statistics/` that renders a summary card on the Statistics tab. It receives its module through a cascading value and is auto-collected from active dungeon-lifetime modules with a non-null `StatisticsComponentType`.

Statistics surface optional, interesting information the Guide tab does not already show: dungeon-level resource totals, top contributors, item-proc counts, aggregate health. Before adding one, ask whether the Guide already covers it; if it does, do not add it. Per-cast scoring, rotation checklists and "did the player play this right" content belong on the Guide tab. Typed data stays in the module; prose and `QualitativePerformance` mapping live here in the component.

## Procedure

### 1. Create The Statistics Component

Place at `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/Statistics/{Name}Statistics.razor`.

```razor
@inherits AnalyzerStatistic<{Name}Tracker>

<StatCard Title="{Feature Name}">
    <Info>What this card measures, revealed by the info affordance.</Info>
    <ChildContent>
        <CastOverview Title="Overview" Stats="@BuildOverviewStats()" />
    </ChildContent>
</StatCard>

@code {
    private IEnumerable<OverviewStat> BuildOverviewStats()
    {
        return
        [
            new OverviewStat(
                $"{Analyzer.Generated}",
                "Generated",
                "Total resource generated during the encounter."),
        ];
    }
}
```

No `@using` lines are needed: the hero `_Imports.razor` already imports `FellowshipAnalyzer.Core.UI.Components`, `.UI.Guides` and the hero's own `Statistics` namespace (see Rime's `_Imports.razor`; `WinterOrbStatistics.razor` opens straight with `@inherits`).

`AnalyzerStatistic<T>` lives in `FellowshipAnalyzer.Core.Game` (`src/FellowshipAnalyzer.Core/Game/AnalyzerStatistic.cs`); `T` is constrained to `Module`, so any `Module`, `Analyzer` or `ResourceTracker` subclass works. It provides a typed `Analyzer` property from the cascading module value:

```csharp
public abstract class AnalyzerStatistic<T> : ComponentBase where T : Module
{
    [CascadingParameter] public Module Module { get; set; } = null!;
    protected T Analyzer => (T)Module;
}
```

`StatCard` takes a `Header` fragment (typically a `SpellLink`, taking precedence over `Title`) or a plain `Title`, an optional `Meta` suffix, an `Info` tooltip fragment, `Size`, and `Wide`/`UltraWide` span controls. See `RollingFlamesStatistics.razor` (Ardeos) for the `Header`/`Info`/`ChildContent` form.

### 2. Link From The Module

In the module class, set `StatisticsComponentType`, and place the card with `StatisticCategory` and `StatisticOrder`:

```csharp
public sealed partial class {Name}Tracker : Analyzer
{
    public override Type? StatisticsComponentType => typeof({Name}Statistics);
    public override StatisticCategory StatisticCategory => StatisticCategory.General;
    public override StatisticOrder StatisticOrder => StatisticOrder.Default;
}
```

The module must be marked `partial` (so the generator can wire its `[On<>]` handlers) and registered with `[AddAnalyzer<T>]` when it subscribes to events, or `[AddModule<T>]` / `[AddState<T>]` when it does not, in both cases with no `[ForPull]`. Statistics are collected only from the parser's active-module set, which is every registered type with no `[ForPull]`; a pull-lifetime analyzer is never in that set, so it never contributes a card. Surface per-pull work through a guide instead.

`Report.razor` groups the collected `StatisticEntry(Module, ComponentType, StatisticCategory, StatisticOrder)` entries by category, orders each group by `StatisticOrder`, renders a `StatisticsSectionTitle` and wraps the group in `StatisticsPanel` (which runs the masonry pass in `_content/FellowshipAnalyzer.Core/js/statistics-masonry.js`), then renders each entry as:

```razor
<CascadingValue Value="@entry.Module">
    <DynamicComponent Type="@entry.ComponentType" />
</CascadingValue>
```

Do not assume a fixed card width: the panel packs cards, and `StatCard` exposes `Wide` / `UltraWide` for cards that need to span.

## Available UI Widgets

| Component | Purpose |
|-----------|---------|
| `StatCard` | Card with `Header` fragment or `Title`, optional `Meta`, `Info` tooltip, `Size`, `Wide`/`UltraWide` spans. |
| `CastOverview` | Summary stat group. |
| `GradiatedPerformanceBar` | Color-graded performance bar. |
| `PassFailBar` | Binary pass/fail bar. |
| `StackedBar` | Stacked horizontal bar with segments. |

## Key Rules

- Statistics components go in `Statistics/`.
- Inherit `AnalyzerStatistic<T>` where `T` is the module type.
- Access module data through the `Analyzer` property.
- Do not inject the parser; the module comes from the cascading value.
- Statistics are optional, interesting information the Guide tab does not show. Never duplicate a guide section as a statistic.
- The module must be a registration with no `[ForPull]`, and must expose its card, for auto-collection to work.
- Use the `style-guide` skill before adding or changing component styles.

## Checklist

- [ ] File is at `Statistics/{Name}Statistics.razor`.
- [ ] Component inherits `AnalyzerStatistic<{Name}Tracker>` or the matching module type.
- [ ] Component uses `Analyzer` to access state.
- [ ] The module is `partial`, registered with no `[ForPull]`, and exposes its `{Name}Statistics` card.
- [ ] `StatisticCategory` and `StatisticOrder` place the card sensibly.
- [ ] The card does not duplicate Guide tab content.
