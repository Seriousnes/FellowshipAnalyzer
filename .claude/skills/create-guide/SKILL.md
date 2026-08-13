---
name: create-guide
description: "Create a Guide Razor component for a FellowshipAnalyzer analyzer. Use when: adding a guide section to the Guide tab, creating a per-analyzer guide view, or composing the hero's main Guide.razor page."
---

# Create Guide Component

A guide component is a Razor file in `Guides/` that renders analyzer state in the Guide tab. It inherits `ReportComponent<{Hero}CombatLogParser>` for its `Parser` and reads analyzer instances directly - dungeon-lifetime modules via generated parser properties, pull-lifetime analyzers via the generated pull read paths.

**Never `@inject` the parser.** Parsers are transient, one instance per analysis, so an injected one has analyzed nothing. `ReportComponent<TParser>` reads the parser that produced the analysis being rendered from the report shell's cascade, and carries the rest of the report scope with it: `DungeonTime`, `Result`, and `SelectedPull`.

The analyzer holds typed data (counts, rates, timestamps, typed entry records); the guide owns all presentation: prose, severity wording, and `PerformanceTier` judgments. Display-shaping helpers that turn analyzer state into shared component inputs live in the guide's `@code` block.

Reference implementation: `src/Heroes/FellowshipAnalyzer.Heroes.Tariq/Guides/FuryEconomyGuide.razor`.

## Procedure

### 1. Create The Feature Guide

Place at `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/Guides/{Name}Guide.razor`.

For a pull-lifetime analyzer (registered with `[AddAnalyzer<T>]`), read the cross-pull stream `Parser.{Name}Analyzers` - a list of `(Pull, Analyzer)` pairs:

```razor
@inherits ReportComponent<{Hero}CombatLogParser>

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

For a dungeon-lifetime module, read the generated nullable parser property (`Parser.WinterOrbTracker` style) and null-check it.

`_Imports.razor` should include the hero `Modules` namespace, as Rime does, so analyzer types are available to guides.

### Left panel voice

The shapes below are derived from the WoWAnalyzer retail corpus, which the owner contributes to. The
authoritative samples are the two the owner wrote by hand, `shaman/enhancement` and
`shaman/elemental` in that repository. Read `banned-vocabulary` alongside this section: it decides
which words a clause may use, this decides which clauses exist.

#### The three moves

A left panel is a role sentence, then directives, then a reading note. Only the directives are
mandatory.

```razor
<LeftPanel>
    <p>
        <strong><SpellLink Spell="Spells.{Ability}" /></strong> is your {ranking}.
        {Directive}, and {precondition to have ready}.
    </p>
</LeftPanel>
```

Fill the placeholders from the hero's own registry and the directive you were given. Do not carry a
resource or ability name across from another hero.

**1. Role sentence.** The ability name in bold, then where it ranks in the hero's kit. A comparative
or superlative is the point of this sentence: "your highest damage-per-Focus spender", "your primary
filler while the cooldown is unavailable", "the strongest Blood spender". A tooltip cannot rank, so a
ranking is decision content and belongs here.

**2. Directives.** One to three sentences: when to press it, what to spend it on where there is a
choice, what to have ready first. Live example, `SerratedEdgeGuide.razor:7`: "Avoid consuming Serrated
Edge on filler abilities, try to consume it with Grim Carve for AoE or Heart Splitter for single
target or priority targets."

**3. Reading note.** Only where it changes how the number should be read. A scoring scope ("the only
casts flagged here are those pressed with the buff already available") and an exclusion ("Blood spent
during Slaughter is not evaluated") both qualify. Where nothing changes the reading, the panel ends
after the directives.

A panel may be a `<ul>` instead of paragraphs where the directives partition cleanly and each bullet
opens on a bolded verb, as `FuryEconomyGuide.razor:9` does with **Build** and **Spend**. Use it for a
resource economy panel; use paragraphs everywhere else.

#### Mechanics: the tooltip test

A clause stating a mechanic is deleted where a single ability's tooltip carries it, and kept where no
single tooltip does.

| Verdict | Shape | Example |
|---|---|---|
| Delete | One ability's own behaviour | "Every Flame Shock tick has a chance to proc Lava Surge, which resets the cooldown on Lava Burst" |
| Keep | An interaction across two abilities | "Each hit from Primordial Storm is considered a Main-Hand attack, and can trigger Windfury Weapon separately" |
| Keep | A ranking among abilities | "is the most efficient way to reduce the cooldown of Sanctify" |

Correcting a detail inside a clause the tooltip already carries is the signal to delete the clause.

#### Register

Mixed, set by how much the pull can prevent compliance.

| Strength | Words | Use for |
|---|---|---|
| Absolute | `Never`, `Always` | A state that is wrong regardless of build: a generator pressed at cap, a spender below its floor |
| Target | `Try to`, `Aim to`, `Attempt to` | A percentage or count the pull can deny: uptime, window fill, cast efficiency |
| Plain | bare imperative | Everything else: "Spend Blood at five stacks", "Open the window with Owed in Blood ready" |

Second person is available and is not the default. `You` earns its place where the sentence reports
the reader's own result ("You overcapped 47 Fury during this pull"), not where it pads a directive.

#### Sentence templates

Each of these recurs across the corpus. Fill and use them rather than inventing a shape.

| Shape | Template |
|---|---|
| Resource opener | "{Hero}'s primary resource is {resource}. Avoid capping {resource}, lost {resource} generation is lost damage." |
| Builder and spender pair | "Never use a builder at maximum {resource}, and always wait until {N} to use a spender." |
| Role plus directive | "**{Ability}** is your {ranking}. {Directive}." |
| Window fill | "During {window}, cast as many {ability} as possible. Enter it with {precondition} so you can begin immediately." |
| Uptime target | "Keep {aura} active on the target at all times. Try to maintain {N}% uptime." |
| Cooldown holding | "{Hero}'s cooldowns should not be held for long. Press each as soon as it becomes available, as long as it can reach a target." |
| Chart pointer | "The chart below shows your {quantity} through {pull}." |
| Graph legend | "{Graph name} - this graph shows {what it plots}. Grey segments show {neutral state}, yellow segments show {busy state}. Red segments highlight {the missed opportunity}." |
| Tier legend | "Perfect - {condition}. Good - {condition}. Ok - {condition}. Fail - {condition}." |
| Concession | "{Absolute directive}. It will occasionally be impossible to {comply}, while handling mechanics or during {phase type}." |
| Permission | "{Doing X} briefly is fine, but {the condition that makes it a failure}." |
| Named non-goal | "This section is about {what it measures}, not {the adjacent quantity it does not}." |
| Measurement boundary | "This section flags only {the one condition measured}. {The other case a reader would expect to be judged} is not flagged, and is treated as acceptable." |
| Window ceiling | "You {pressed} {n} of a maximum of {max} this window, from {entry state}, {gains during it}, and {gains that arrived too late to convert}." |
| Defensive tolerance | "{Defensive} usage varies from pull to pull, and may need to be delayed for specific mechanics. Any amount of usage is good, and anywhere you could fit another usage is a theoretical loss." |
| Context, not a verdict | "This section is informative only and is not suggestive of poor performance." |

Absolute directives take the concession in the same paragraph, never in one of their own.

A proc or window analyzer that scores timing takes four in order: the interaction, the directive, the
named non-goal, then the permission clause. Naming the non-goal is what stops a reader reading a
timing score as a throughput score. `SerratedEdgeGuide.razor:12` already carries the permission form:
"A sub-optimal consumer is better than a missed cast opportunity, so avoid holding Blood Arc for too
long."

The window ceiling template exists to keep a per-window maximum honest. Derive it from that window's
own entry state and length, never from the best window elsewhere in the report.

#### Numbers in prose

The left panel may state a measured value inline: "You overcapped 47 Fury during this pull." Where
the value is the panel's whole point, this is preferred to a bare directive beside a table that
repeats it. The tier is carried by the stat in the right panel, not by the prose.

#### Legends and reading keys

A tier legend is declared once per section, best first, as label, dash, condition, and every condition
is absolute game state: a stack count, a time remaining, a target count. It sits under the data it
explains, in a `TipBox`, not in the left panel. A per-cast colour caption is a leading-dash fragment
under `CastDetail`: "- Green is a good cast, Yellow is an ok cast, Red is a bad cast."

A `SubSection` may carry its heading as a bolded inline lead-in instead of a `Title`, which is how the
graph legend template opens: "**Fury Over Time** - this graph shows...". Pick one per section and do
not do both.

#### Nothing to report

Three states, three forms. An individual passing cast gets its tier colour and no praise.

| State | Form |
|---|---|
| Scored, nothing failed | The only place praise belongs: "All of your casts of this ability were good!" |
| The log recorded nothing | A `TipBox` with `Variant="TipBoxVariant.Info"`, stating it plainly, as `SerratedEdgeGuide.razor:19` does: "No Serrated Edge buff was recorded on any pull." |
| Not built yet | A plain statement ending in a period: "Per-cast breakdown for this ability is not built yet." |

#### The lexicon

| Say | For |
|---|---|
| `wasted` | A resource or proc that expired, overcapped, or was overwritten |
| `overcap`, `capping` | Generating a resource above its ceiling, where naming the cause matters |
| `uptime` | Share of the pull an aura was active |
| `on cooldown` | Pressed the moment it becomes available |
| `window` | A bounded period during which presses are counted |
| `pool`, `pooling` | Accumulating a resource ahead of a window |
| `filler` | A low-priority press that occupies a global |
| `expired`, `overwritten`, `unused` | The three ways a proc is lost |

Write no metaphor for an event the game names. `munched`, `sniping` and `smuggling` all appear in the
corpus and none of them port.

#### What stays out

- **A priority list.** No `Action Priority List` section, no ordered chain of ability categories, and
  no rotation prose the log did not produce.
- **A comparison to other players.** "compare your analysis against a top 100 log" tiers against a
  moving reference. Tier against a stack count, a setup condition, or a game cap.
- **An external source.** No guide site, wiki, community channel or simulation tool named in rendered
  prose. Research input is not UI content.
- **Any mention of the log.** Not "the log records no", not "the log attaches no cost to a cast", not
  "limited to what is present in your combat log". There is no blessed rewording: the word appearing
  in a clause is the signal that the clause goes, and usually the sentence with it. A stat says what
  was counted; why something was not counted is never stated.

  "Conversions made while Bloodbound Spirit was active. Reported as context: the log records nothing
  about when Spirit was about to become available." becomes **"Conversions made while Bloodbound
  Spirit was active."** "Reported as context rather than scored, because an ally leaves the radius for
  reasons the log does not record." is deleted with no replacement.

  `Fellowship Logs`, the service the report came from, is a product name and is unaffected.
- **Em and en dashes**, including the `&mdash;` and `&ndash;` entities. Use a hyphen or a comma.

#### Panel width

`GuideSection` splits at `LeftPanelPercent`, default 40, and collapses to one column under 768px, so
write "the table", never "the table to the right". A left panel holding one role sentence and one
directive beside a full data panel is the normal case, not a defect.

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
2. **Core abilities.** The presses the rotation is built from, most decisive first.
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

@if (Parser.{Name}Analyzers.Count > 0)
{
    <{Name}Guide />
}
```

Gate pull-analyzer guides on a non-empty stream; null-check generated module properties for dungeon-lifetime modules (modules may be inactive).

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
- Reach the hero parser by inheriting `ReportComponent<{Hero}CombatLogParser>`, never by injecting it.
- Read pull analyzers via `Parser.{Name}Analyzers`, `Parser.For(pull).{Name}Analyzer` or the `pull.{Name}Analyzer` extension (the member is named after the surface type, with a leading `I` stripped for a marker interface). Read dungeon-lifetime modules via generated properties such as `Parser.WinterOrbTracker`, where the `Analyzer` suffix is stripped.
- Keep event-derived state in modules; keep prose, severity wording, and `PerformanceTier` mapping here.
- Write every `<LeftPanel>` in the voice above: role sentence, directives, then a reading note only where it changes how the number reads.
- Project per-pull rows with the `ToPullRows` / `ToItemRows` extensions returning `PerCastRow`; use `PerformanceTiers.FromThresholds` for tier ladders.
- Use shared components from `FellowshipAnalyzer.Core.UI.Guides` when possible.
- Use the `style-guide` skill before adding or changing component styles.

## Checklist

- [ ] File is at `Guides/{Name}Guide.razor`.
- [ ] Component inherits `ReportComponent<{Hero}CombatLogParser>`.
- [ ] Component reads analyzer state via the generated read paths.
- [ ] `<LeftPanel>` opens on the ability's ranking, states the directives, and adds a reading note only where one is needed.
- [ ] Every mechanic clause states something no single ability tooltip carries.
- [ ] Absolutes use Never or Always; targets the pull can deny use Try to or Aim to.
- [ ] No priority list, no comparison to other players, no external source, no em or en dash.
- [ ] Feature guide is added to `{Hero}Guide.razor` with a gate.
- [ ] Parser `GuideComponent` points to the root guide.
