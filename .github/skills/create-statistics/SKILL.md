---
name: create-statistics
description: "Create an auto-collected statistics Razor component for a FellowshipAnalyzer analyzer. Use when: adding a statistics card to the Statistics tab, creating an overview statistic for an analyzer module."
---

# Create Statistics Component

A statistics component is a Razor file in `Statistics/` that renders a summary card on the Statistics tab. It receives its module through a cascading value and is auto-collected from active modules with a non-null `StatisticsComponentType`.

## Procedure

### 1. Create The Statistics Component

Place at `src/FellowshipAnalyzer.Heroes.{Hero}/Statistics/{Name}Statistics.razor`.

```razor
@namespace FellowshipAnalyzer.Heroes.{Hero}.Statistics
@using FellowshipAnalyzer.Components
@inherits AnalyzerStatistic<{Name}Analyzer>

<StatCard Title="{Feature Name}">
    <CastOverview Title="Overview" Stats="@BuildOverviewStats()" />
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

`AnalyzerStatistic<T>` provides a typed `Analyzer` property from the cascading module value:

```csharp
public abstract class AnalyzerStatistic<T> : ComponentBase where T : Module
{
    [CascadingParameter] public Module Module { get; set; } = null!;
    protected T Analyzer => (T)Module;
}
```

### 2. Link From The Module

In the analyzer or tracker class, set `StatisticsComponentType`:

```csharp
public sealed class {Name}Analyzer : Analyzer
{
    public override Type? StatisticsComponentType => typeof({Name}Statistics);
}
```

The framework auto-collects statistics from active modules and renders them with `DynamicComponent`:

```razor
@foreach (var (module, componentType) in _result.Statistics)
{
    <CascadingValue Value="@module">
        <DynamicComponent Type="@componentType" />
    </CascadingValue>
}
```

## Available UI Widgets

| Component | Purpose |
|-----------|---------|
| `StatCard` | Card with title and content slot. |
| `CastOverview` | Summary stat group. |
| `GradiatedPerformanceBar` | Color-graded performance bar. |
| `PassFailBar` | Binary pass/fail bar. |
| `StackedBar` | Stacked horizontal bar with segments. |

## Key Rules

- Statistics components go in `Statistics/`.
- Inherit `AnalyzerStatistic<T>` where `T` is the module type.
- Access module data through the `Analyzer` property.
- Do not inject the parser; the module comes from the cascading value.
- Keep statistics components summary-focused. Detailed per-cast analysis belongs in guide components.
- The module must set `StatisticsComponentType` for auto-collection to work.
- Use the `style-guide` skill before adding or changing component styles.

## Checklist

- [ ] File is at `Statistics/{Name}Statistics.razor`.
- [ ] Component inherits `AnalyzerStatistic<{Name}Analyzer>` or the matching tracker/module type.
- [ ] Component uses `Analyzer` to access state.
- [ ] Module `StatisticsComponentType` returns `typeof({Name}Statistics)`.