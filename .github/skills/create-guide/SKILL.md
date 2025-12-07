---
name: create-guide
description: "Create a Guide Razor component for a FellowshipAnalyzer analyzer. Use when: adding a guide section to the Guide tab, creating a per-analyzer guide view, or composing the hero's main Guide.razor page."
---

# Create Guide Component

A guide component is a **Razor file** in the `Guides/` folder that renders analysis results in the Guide tab. It `@inject`s the hero's CombatLogParser to access analyzer state. Guide components are manually composed in the hero's main `Guide.razor` page.

## Procedure

### 1. Create the guide component

Place at `src/FellowshipAnalyzer.Heroes.{Hero}/Guides/{Name}Guide.razor`.

```razor
@using FellowshipAnalyzer.Heroes.{Hero}.Analysis
@using FellowshipAnalyzer.Heroes.{Hero}.Analyzers
@using FellowshipAnalyzer.Components

@inject {Hero}CombatLogParser Parser

@{ var analyzer = Parser.{Name}!; }

<GuideSection Title="{Feature Name}">
    <CastOverview Stats="@analyzer.BuildOverviewStats()" />
    <CastDetail Casts="@analyzer.BuildPerCastData()" />
</GuideSection>
```

The guide component:
- Accesses the analyzer via `Parser.{Name}` (the source-generated typed property)
- Uses shared UI widgets from `FellowshipAnalyzer.Components` (`GuideSection`, `CastOverview`, `CastDetail`, `FindingsList`, `PerformanceBoxRow`, etc.)
- Contains **no analysis logic** — it only reads state from the analyzer and renders it

### 2. Add to the hero's main Guide.razor

Each hero has a mandatory `Guides/{Hero}Guide.razor` that manually composes all guide sections:

```razor
@using FellowshipAnalyzer.Heroes.{Hero}.Analysis
@inject {Hero}CombatLogParser Parser

<Section Title="Single Target Combo">
    @if (Parser.BasicStCombo is not null)
    {
        <BasicStComboGuide />
    }
</Section>

<Section Title="{Feature Name}">
    @if (Parser.{Name} is not null)
    {
        <{Name}Guide />
    }
</Section>
```

Null-check the analyzer property — modules can be inactive.

### 3. (If it doesn't exist) Create the hero's main Guide.razor

The hero's `GuideComponentType` property points to this page:

```razor
@using FellowshipAnalyzer.Heroes.{Hero}.Analysis
@inject {Hero}CombatLogParser Parser

<!-- Compose all guide sections in desired order -->
@if (Parser.BasicStCombo is not null)
{
    <BasicStComboGuide />
}

@if (Parser.WinterOrbTracker is not null)
{
    <WinterOrbGuide />
}
```

Register it on the parser:
```csharp
public override Type GuideComponentType => typeof({Hero}Guide);
```

## Available UI Widgets

From `FellowshipAnalyzer.Components`:

| Component | Purpose |
|-----------|---------|
| `GuideSection` | Titled collapsible section wrapper |
| `CastOverview` | Summary bar with overview stats |
| `CastDetail` | Per-cast breakdown with performance boxes |
| `FindingsList` | List of findings/suggestions |
| `GradiatedPerformanceBar` | Color-graded performance bar (0–100) |
| `PassFailBar` | Binary pass/fail bar |
| `PerformanceBoxRow` | Row of colored performance boxes |
| `SpellSequence` | Filmstrip of spell casts |
| `StatCard` | Card with title and content |
| `StackedBar` | Stacked horizontal bar chart |

## Key Rules

- Guide components go in `Guides/` folder, never `Analyzers/`
- Access the parser via `@inject`, not `[CascadingParameter]`
- Access analyzer data via `Parser.{Name}` — the source-generated typed property
- No analysis logic in guide components — they only render
- Always null-check `Parser.{Name}` before rendering (module may be inactive)
- Guide composition order is controlled manually in `{Hero}Guide.razor`

## Checklist

- [ ] File is at `Guides/{Name}Guide.razor`
- [ ] `@inject`s the hero's CombatLogParser
- [ ] Reads analyzer state via `Parser.{Name}` (no analysis logic in the component)
- [ ] Added to the hero's main `{Hero}Guide.razor` with null-check
