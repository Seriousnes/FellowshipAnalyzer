---
name: create-statistics
description: "Create an auto-collected statistics Razor component for a FellowshipAnalyzer analyzer. Use when: adding a statistics card to the Statistics tab, creating an overview statistic for an analyzer module."
---

# Create Statistics Component

A statistics component is a **Razor file** in the `Statistics/` folder that renders a summary card on the Statistics tab. It receives its analyzer via `[CascadingParameter]` and is auto-collected — no manual composition needed.

## Procedure

### 1. Create the statistics component

Place at `src/FellowshipAnalyzer.Heroes.{Hero}/Statistics/{Name}Statistics.razor`.

```razor
@using FellowshipAnalyzer.Components
@using FellowshipAnalyzer.Heroes.{Hero}.Analyzers

@inherits AnalyzerStatistic<{Name}Analyzer>

<StatCard Title="{Feature Name}">
    <p>@Analyzer.Generated generated, @Analyzer.Wasted wasted</p>
    <GradiatedPerformanceBar Score="@Analyzer.EfficiencyScore" />
</StatCard>
```

`AnalyzerStatistic<T>` provides:
```csharp
public abstract class AnalyzerStatistic<T> : ComponentBase where T : Module
{
    [CascadingParameter] public Module Module { get; set; }
    protected T Analyzer => (T)Module;
}
```

### 2. Link from the analyzer

In the analyzer class, set `StatisticsComponentType`:

```csharp
public sealed class {Name}Analyzer(CombatLogParser parser) : Analyzer(parser)
{
    public override Type? StatisticsComponentType => typeof({Name}Statistics);
    // ... rest of analyzer
}
```

That's it. The framework auto-collects statistics from active modules that have a non-null `StatisticsComponentType` and renders them via:

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
| `StatCard` | Card with title and content slot |
| `GradiatedPerformanceBar` | Color-graded 0–100 bar |
| `PassFailBar` | Binary pass/fail bar |
| `StackedBar` | Stacked horizontal bar with segments |

## Key Rules

- Statistics components go in `Statistics/` folder
- Inherit `AnalyzerStatistic<T>` where `T` is the analyzer type
- Access analyzer data via `Analyzer` property (typed, provided by CascadingParameter)
- No `@inject` needed — the analyzer comes via cascading value
- Keep statistics components simple — summary cards, not detailed breakdowns (those belong in guides)
- The analyzer must set `StatisticsComponentType` for auto-collection to work

## Checklist

- [ ] File is at `Statistics/{Name}Statistics.razor`
- [ ] Inherits `AnalyzerStatistic<{Name}Analyzer>`
- [ ] Uses `Analyzer` property to access data
- [ ] Analyzer's `StatisticsComponentType` returns `typeof({Name}Statistics)`
