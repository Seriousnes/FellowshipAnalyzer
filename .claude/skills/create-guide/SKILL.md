---
name: create-guide
description: "Create a Guide Razor component for a FellowshipAnalyzer analyzer. Use when: adding a guide section to the Guide tab, creating a per-analyzer guide view, or composing the hero's main Guide.razor page."
---

# Create Guide Component

A guide component is a Razor file in `Guides/` that renders analyzer state in the Guide tab. It inherits `GuideComponent<{Hero}CombatLogParser>` for its `Parser` and reads analyzer instances directly - dungeon-lifetime modules via generated parser properties, pull-lifetime analyzers via the generated pull read paths.

**A guide decides for itself whether it has anything to show.** `GuideComponent<TParser>` declares `protected abstract bool IsActive()`; the guide overrides it with the condition on its own analyzer surface and writes its markup normally. The base handles suppression: it overrides `SetParametersAsync`, applies the parameters, and returns without queueing a render when `IsActive()` is false, so an inactive guide contributes no frames and its lifecycle methods never run. This is the only point at which a base class can gate a Razor component, because a derived component overrides `BuildRenderTree` and `ShouldRender()` is bypassed on the first render. A guide therefore never writes an `@if` around its own body, and the hero root guide renders every feature guide unconditionally.

Because an inactive guide is never rendered, a lifecycle override (`OnInitialized`, `OnParametersSet`, `OnAfterRender`) in a guide will not run when the guide is inactive.

**Never `@inject` the parser.** Parsers are transient, one instance per analysis, so an injected one has analyzed nothing. `GuideComponent<TParser>` derives from `ReportComponent<TParser>`, which reads the parser that produced the analysis being rendered from the report shell's cascade and carries the rest of the report scope with it: `DungeonTime`, `Result`, and `SelectedPull`.

The analyzer holds typed data (counts, rates, timestamps, typed entry records); the guide owns all presentation: prose, severity wording, and `PerformanceTier` ratings. Display-shaping helpers that turn analyzer state into shared component inputs live in the guide's `@code` block.

Reference implementation: `src/Heroes/FellowshipAnalyzer.Heroes.Tariq/Guides/FuryEconomyGuide.razor`.

## Voice

Read `.claude/skills/banned-vocabulary/SKILL.md` before writing anything. No banned words or phrases should appear in the guide content, tooltips, statistics, or any other user-facing text. Never mention logs or raw data.

## Procedure

### 1. Create The Feature Guide

Place at `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/Guides/{Name}Guide.razor`.

For a pull-lifetime analyzer (registered with `[AddAnalyzer<T>]`), read the cross-pull stream `Parser.{Name}Analyzers` - a list of `(Pull, Analyzer)` pairs:

```razor
@inherits GuideComponent<{Hero}CombatLogParser>

<GuideSection Title="{Feature Name}">
    <LeftPanel>
        <p>
            <strong><SpellLink Spell="Spells.{Ability}" /></strong> is your {role}. {Directive}.
        </p>
    </LeftPanel>
    <RightPanel>
        <CastOverview Title="Overview" Stats="@BuildOverviewStats()" />
        <CastDetail Title="Per-Pull {Feature Name}" Casts="@BuildPerPullData()" />
    </RightPanel>
</GuideSection>

@code {
    protected override bool IsActive() => Parser.{Name}Analyzers.Count > 0;

    private IEnumerable<OverviewStat> BuildOverviewStats()
    {
        var analyzers = Parser.{Name}Analyzers.Select(entry => entry.Analyzer).ToList();
        var good = analyzers.Sum(analyzer => analyzer.GoodCount);
        var uptime = analyzers.Average(analyzer => analyzer.UptimePercent);

        return
        [
            new OverviewStat($"{good}", "Good Casts"),
            new OverviewStat(
                $"{uptime:0.#}%",
                "Uptime",
                "Share of combat with the effect active on at least one enemy, counting a moment once however many enemies carried it."),
        ];
    }

    private IEnumerable<PerCastData> BuildPerPullData() =>
        Parser.{Name}Analyzers.ToPullRows(Parser, (analyzer, pull) => new PerCastRow
        {
            Performance = PerformanceTiers.FromThresholds(analyzer.GoodSharePercent, 75, 50, 25),
            Stats =
            [
                new PerCastStat($"{analyzer.GoodCount}", "Good Casts"),
                new PerCastStat($"{analyzer.Overwritten}", "Overwritten"),
            ],
        });
}
```

Project rows with the `ToPullRows` / `ToItemRows` extensions (in `FellowshipAnalyzer.Core.UI.Guides`) rather than hand-building each `PerCastData`. The row builder returns a `PerCastRow` with only the varying fields (`Performance`, `Stats`, and optionally `Sequence` / `AdditionalContent` / `Details` / `Tooltip`); the extension fills the pull-derived grouping (the `FormatTimestamp` label, `Group` name, and `PullBanner`) for you. Use `ToPullRows` for one row per analyzer (a per-pull aggregate); use `ToItemRows(Parser, a => a.Windows, (window, pull) => ...)` to flatten an inner collection into one row per item. When a row's timestamp is not the pull start (e.g. a window start), set `PerCastRow.Timestamp`. Set `Sequence` when the window the row assesses can contain an arbitrary number of casts, when it expects a certain order of three or more spells, or when multiple spells should occur within a known period and their order or count is important and can be rated. Map thresholds to a `PerformanceTier` with `PerformanceTiers.FromThresholds(value, perfect, good, ok)` instead of hand-writing the ladder.

For a dungeon-lifetime module, read the generated nullable parser property (`Parser.WinterOrbTracker` style) and null-check it.

`_Imports.razor` should include the hero `Modules` namespace, as Rime does, so analyzer types are available to guides.

### Left panel voice

Read `.claude/skills/banned-vocabulary/SKILL.md` before writing anything.

### When a stat takes a tooltip

`OverviewStat` and `PerCastStat` take a `Label` always, and a `Tooltip` where there is a counting
rule or a game rule to state. Those two are the whole set:

- **A counting rule** names which cases fall inside the number, where a reader would otherwise pick
  a different set. `86/122 | Full MSW` takes "Tempest casts consumed at full Maelstrom Weapon
  stacks. Free casts from Thorim's Invocation are counted as full." - the second sentence is the
  rule, and the tooltip exists for it.
- **A game rule** names what the quantity does in game terms. `10 | Stacks spent` takes "Maelstrom
  Weapon stacks consumed, providing 3.0s of CDR to Stormstrike and Lava Lash."

Both examples open by naming the quantity in full ability names and then state the rule. That
opening clause is the tooltip's frame, not its reason for existing: a stat with neither rule to
state takes the two-argument form, whatever its label reads like. A plain sum such as `"Damage
Absorbed"` or `"Good Casts"` is finished at two arguments.

A tooltip states the game, never the tool. It never says where a number came from in the log, what
the log does or does not carry, or that a value is reconstructed, estimated or approximate. Keep
that on the analyzer member's XML doc, where the reader is a developer. A metric that cannot be
measured for a cast is left out for that cast, with no note.

Where a two-argument stat also carries a tier, pass it as the named `Performance:` argument, since
`Tooltip` is the third positional parameter.

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
It is a flat list of feature guides, so the order of that list is the only composition decision it
carries. Place a new guide by this order:

1. **Resource economy.** What the hero generates and spends, and what it lost at the cap.
2. **Core abilities.** The casts the rotation is built from, most decisive first.
3. **Cooldowns.** Long cooldowns and the windows they open.
4. **Defensives and utility**, where the hero has analyzed ones.
5. **Downtime.**

`TariqGuide.razor` is the reference: `FuryEconomyGuide`, then `EmpowermentGuide`,
`FocusedWrathGuide`, `HammerStormGuide`, `CullingStrikeGuide`. Abilities before cooldowns, not the
reverse. `RimeGuide.razor` opens on `WintersEmbraceGuide` ahead of its own `WinterOrbGuide` and does
not follow this order.

```razor
@using FellowshipAnalyzer.Heroes.{Hero}.Analysis
@inherits ReportComponent<{Hero}CombatLogParser>

<{Name}Guide />

<{Other}Guide />
```

The root guide inherits `ReportComponent`, not `GuideComponent`: it composes and orders, it does not gate. Every feature guide is a bare element. The condition that decides whether a guide has anything to show belongs in that guide's own `IsActive()`: a non-empty stream for a pull analyzer, a non-null generated property for a dungeon-lifetime module.

### 3. Ensure The Parser Points To The Root Guide

The hero parser exposes the root guide through `GuideComponent`:

```csharp
public override Type? GuideComponent => typeof({Hero}Guide);
```

## Available UI Widgets

From `FellowshipAnalyzer.Core.UI.Guides`:

| Component | Purpose |
|-----------|---------|
| `GuideSection` | Titled two-column guide section: `LeftPanel` prose, `RightPanel` data, split by `LeftPanelPercent` (default 40). |
| `TipBox` | Callout box for a tier legend or a reading key. `Variant` is Info, Note, Success, Warning or Error; `HideIcon` drops the glyph. |
| `HelperText` | Inline muted note beside a chart or table. |
| `SubSection` | Titled block inside a `GuideSection` right panel. |
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
- Reach the hero parser by inheriting `GuideComponent<{Hero}CombatLogParser>`, never by injecting it. The hero root guide inherits `ReportComponent<{Hero}CombatLogParser>`.
- Override `IsActive()` with the guide's own activation condition. The base suppresses the whole component when it returns false, so never write an `@if` around the markup body and never gate a feature guide from the root guide.
- Read pull analyzers via `Parser.{Name}Analyzers`, `Parser.For(pull).{Name}Analyzer` or the `pull.{Name}Analyzer` extension (the member is named after the surface type, with a leading `I` stripped for a marker interface). Read dungeon-lifetime modules via generated properties such as `Parser.WinterOrbTracker`, where the `Analyzer` suffix is stripped.
- Keep event-derived state in modules; keep prose, severity wording, and `PerformanceTier` mapping here.
- Project per-pull rows with the `ToPullRows` / `ToItemRows` extensions returning `PerCastRow`; use `PerformanceTiers.FromThresholds` for tier ladders.
- Use shared components from `FellowshipAnalyzer.Core.UI.Guides` when possible.
- Use the `style-guide` skill before adding or changing component styles.

## Checklist

- [ ] File is at `Guides/{Name}Guide.razor`.
- [ ] Component inherits `GuideComponent<{Hero}CombatLogParser>`.
- [ ] Component overrides `IsActive()`, and its markup body carries no `@if` gate of its own.
- [ ] Component reads analyzer state via the generated read paths.
- [ ] `<LeftPanel>` follows the house style's three moves: ranking, directives, then a reading note only where one is needed.
- [ ] Every stat carries a `Label` in the house style's grammar and lexicon, and a `Tooltip` only where there is a counting rule or a game rule to state.
- [ ] No stat string, `<LeftPanel>` or `HelperText` says how a number was obtained from the log, or calls a value reconstructed, estimated or approximate.
- [ ] Feature guide is added to `{Hero}Guide.razor` as a bare element, with no gate there.
- [ ] Parser `GuideComponent` points to the root guide.
