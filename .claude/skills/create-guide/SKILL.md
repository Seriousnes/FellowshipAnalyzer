---
name: create-guide
description: "Create a Guide Razor component for a FellowshipAnalyzer analyzer. Use when: adding a guide section to the Guide tab, creating a per-analyzer guide view, or composing the hero's main Guide.razor page."
---

# Create Guide Component

A guide component is a Razor file in `Guides/` that renders analyzer state in the Guide tab. It injects the hero parser and reads source-generated module properties.

Keep state tracking, scoring, and durable analysis in the module. The guide may contain small display-shaping helpers that turn module state into shared component inputs.

## Procedure

### 1. Create The Feature Guide

Place at `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/Guides/{Name}Guide.razor`.

```razor
@namespace FellowshipAnalyzer.Heroes.{Hero}.Guides
@using FellowshipAnalyzer.Components
@using FellowshipAnalyzer.Heroes.{Hero}.Analysis
@inject {Hero}CombatLogParser Parser

<GuideSection Title="{Feature Name}">
    <ChildContent>
        <CastOverview Title="Overview" Stats="@BuildOverviewStats()" />
        <CastDetail Title="{Feature Name}" Casts="@BuildPerCastData()" />
    </ChildContent>
</GuideSection>

@code {
    private {Name}Analyzer Analyzer => Parser.{Name}!;

    private IEnumerable<OverviewStat> BuildOverviewStats()
    {
        return
        [
            new OverviewStat(
                $"{Analyzer.GoodCount}",
                "Good",
                "Successful usages detected by the analyzer."),
        ];
    }

    private IEnumerable<PerCastData> BuildPerCastData()
    {
        return Analyzer.Windows.Select(window => new PerCastData
        {
            Timestamp = Parser.FormatTimestamp(window.StartTimestamp),
            Performance = window.IsGood ? PerformanceTier.Good : PerformanceTier.Fail,
        });
    }
}
```

`_Imports.razor` should include the hero `Modules` namespace, as Rime does, so analyzer types are available to guides.

### 2. Add To The Hero Root Guide

Each hero has a root guide component at `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/{Hero}Guide.razor`.

```razor
@namespace FellowshipAnalyzer.Heroes.{Hero}.Guides
@using FellowshipAnalyzer.Heroes.{Hero}.Analysis
@inject {Hero}CombatLogParser Parser

@if (Parser.{Name} is not null)
{
    <{Name}Guide />
}
```

Null-check generated module properties because modules may be inactive.

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
| `FindingsList` | List of findings/suggestions. |
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
- Read module state via generated parser properties such as `Parser.BasicStCombo`.
- Keep scoring and event-derived state in modules.
- Null-check generated module properties before rendering a feature guide from the root guide.
- Use shared components from `FellowshipAnalyzer.Components` when possible.
- Use the `style-guide` skill before adding or changing component styles.

## Checklist

- [ ] File is at `Guides/{Name}Guide.razor`.
- [ ] Component injects the hero parser.
- [ ] Component reads analyzer state from `Parser.{Name}`.
- [ ] Feature guide is added to `{Hero}Guide.razor` with a null-check.
- [ ] Parser `GuideComponent` points to the root guide.