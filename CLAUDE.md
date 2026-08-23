# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Critical

### Memory
- **This file is the project's only durable memory, capped at 200 lines.** Session memory files are not used; a new durable fact earns its line here by making room, and style rulings live in the house-style skill.

### Comments
- **IMPORTANT** - Never include comments referencing design docs or plan points. Comments are reserved exclusively for API/usage notes.
- **Never** add inline comments or comments within methods for any reason.

### Commit messages and PR descriptions
- **Describe the change, nothing else.** No caveats, no disclaimers, no notes about what was not done, not verified, not seen running, or left for later. A scope boundary the reader already set is not news, and neither is the absence of a step nobody asked for.
- **Never flag your own work for scrutiny.** No "worth a look", no "push back if you disagree", no listing which decisions the reviewer should re-examine. The diff is the request for review.

### Analysis rules
- **`Core/Utility/CombatMath.cs` is the only place damage amplification or reduction is ever calculated.** This covers every hero: Engulfing Flames multipliers, armor and Toughness reductions, damage buffs, defensive abilities. A hero-local maths helper is banned, and so is an inline `raw - raw / (1 + increase)`. If `CombatMath` cannot express what is needed, ask before writing anything.
- **An ability or talent has at most one analyzer.** A new measurement goes into the ability's existing analyzer, never into a second module.
- **A conditionally active analyzer must use `[ActiveWhen<TPredicate>]`**, with a type implementing `IModuleActivePredicate`. Never write the check by hand inside a handler. It works on pull analyzers, and it expresses the inverse gates `[RequiresTalent]` cannot.
- **An analyzer may take a `ResourceTracker` as a `[Dependency<T>]`, but must never re-derive what the tracker owns.** Trackers expose windowed accessors (`SpentBetween`, `TimeByHolderBetween`, `BandsBetween`) and analyzers project them.
- **`[On<Event>]` is a last resort.** Use it only where there is literally no other way. Deriving a resource's state is never such a case.
- **Name an analyzer or guide for what it assesses.** Do not add a qualifier the domain does not need. A misleading name invents a false reason to split an analyzer in two.
- **An absorb's logged strength does not always equal the damage it prevents.** A damage event's `amount` may not be accurate when it is partially absorbed.

## Project

FellowshipAnalyzer parses and analyzes combat logs from the online RPG "Fellowship". Logs are uploaded to fellowshiplogs.com; this app calls the Fellowship Logs GraphQL API, runs hero-specific analyzers over combat events, and renders guide/statistics views.

- C# 14 / .NET 10. The product app is a standalone Blazor WebAssembly client that runs the whole analysis pipeline in the browser.
- Release WASM builds set `RunAOTCompilation`, so download size matters. See "Size discipline" below.
- Local orchestration via .NET Aspire (`FellowshipAnalyzer.AppHost`): dev API, static host for the WASM client, and an Azure Storage emulator.
- See [.claude/instructions/FellowshipAnalyzer-Architecture-Overview.md](.claude/instructions/FellowshipAnalyzer-Architecture-Overview.md) for the full analysis pipeline.
- See [CombatMechanics.md](CombatMechanics.md) for in-game combat mechanics relevant to analyzer work.

## Common commands

```powershell
# First-time setup
dotnet workload restore
dotnet restore

# Build the whole solution
dotnet build FellowshipAnalyzer.slnx -nologo --verbosity minimal

# Run the full app locally (WASM client host + API via Aspire)
dotnet run --project src/FellowshipAnalyzer.AppHost/FellowshipAnalyzer.AppHost.csproj

# Run all tests
dotnet test FellowshipAnalyzer.slnx --no-build
```

The local API requires Fellowship Logs credentials on the **DevApi** project (Aspire does not forward AppHost user secrets to children):

```powershell
dotnet user-secrets set "FellowshipLogs:ClientId" "..." --project src/FellowshipAnalyzer/FellowshipAnalyzer.DevApi
dotnet user-secrets set "FellowshipLogs:ClientSecret" "..." --project src/FellowshipAnalyzer/FellowshipAnalyzer.DevApi
```

AppHost pins DevHost to its `http` launch profile, so the app is at `http://fellowshipanalyzer.dev.localhost:5120`. Aspire assigns the API port at runtime; take it from the Aspire dashboard. The client reaches the API through DevHost's `/api` reverse proxy and Aspire service discovery.

## Architecture

Per-report, per-hero analysis pipeline:

```
FellowshipLogs GraphQL JSON (player events, plus the dungeon death stream merged in by EventStreamMerger)
  → event deserialization (polymorphic on Event)
  → CombatLogParser.Analyze
  → IEventNormalizer passes in Priority order (bookend pulls, rescale resources, attach master data, link casts)
  → dungeon-lifetime modules constructed; RegisterSubscriptions() wires their [On<TEvent>] handlers
  → EventEmitter dispatches; PullStartEvent / PullEndEvent open and close each pull,
    constructing a fresh set of [ForPull] analyzers per pull and retaining them afterwards
  → modules and analyzers expose derived metrics as computed get-only properties over retained state
  → HeroAnalysisResult → Razor guide and statistics components
```

**Solution layout** (only the bits that aren't obvious from `ls`):

- `src/FellowshipAnalyzer.Core` - events, parser infrastructure, base modules/normalizers, spell registries, JSON source-generated context, shared Razor UI under `UI/` (Components, Charts, Guides, Timeline, Diagnostics, Theming) and SCSS under `Styles/`. Shared by every hero; code added here is downloaded by every client.
- `src/FellowshipAnalyzer.Core.Contracts` - DTOs/interfaces that cross the API/client boundary, the shared vocabulary the offline spell-data tooling and the runtime both speak (`Spell`, `FSLID`, `HeroName`, `ResourceTypes`, `MagicSchool`, `AbilityCategory`), and the C# design tokens (`FaTheme`, `FaPalette`, `FaToken`, `FaVar`). It references nothing, so `FellowshipAnalyzer.SpellData` builds without Core and `rebuild-spelldb` runs even when Core does not compile.
- `src/FellowshipAnalyzer.Generators` - Roslyn **source** generators (parser ctor, typed module accessors, pull-analyzer surfaces, module/normalizer type lists, spell registries, talent id constants, hero DI manifest). Hero registration is **reflection-free** for AOT.
- `src/FellowshipAnalyzer.Analyzers` - Roslyn **diagnostic** analyzers (FA00xx), distinct from gameplay "analyzers".
- `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}` - one Razor class library per hero. **Ardeos and Elarion are the most built-out; Rime is the compact reference that covers modules, guides, and statistics.**
- `src/FellowshipAnalyzer/FellowshipAnalyzer` - the Blazor WASM client. `FellowshipAnalyzer.Api` (Azure Functions), `Api.Core`, and `Api.GraphQL` cover Fellowship Logs access; `DevApi` and `DevHost` are the Aspire-wired local variants.
- `src/FellowshipAnalyzer.SpellData` and `src/FellowshipAnalyzer.SpellStudio` - offline merge engine that produces `data/spelldb.json`, plus the Blazor Server app for curating `data/overrides.json`.
- `src/FellowshipAnalyzer.DesignSystem` - Blazor Server showcase hosting the runtime design-token editor.
- `src/FellowshipAnalyzer.Tools` - file-based `dotnet` scripts. Use the **run-tool** skill.
- `src/FellowshipAnalyzer.AppHost` and `src/FellowshipAnalyzer.ServiceDefaults` - Aspire orchestration and shared service defaults.

**Hero parser pattern** - small and declarative; the source generator emits the rest:

```csharp
[HeroAnalyzer(HeroName.Ardeos)]
[AddModule<Abilities>]
[AddAnalyzer<CinderEmberTracker>]
[AddAnalyzer<WildfireComboAnalyzer>]
[AddAnalyzer<SearingBlazeSpreadAnalyzer>]
[AddAnalyzer<SearingBlazeUptimeAnalyzer>]
public sealed partial class ArdeosCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(ArdeosGuide);
}
```

The generator emits the constructor, strongly-typed module properties (for example `.CinderEmberTracker`), the per-surface `PullAnalyzer<T>` lists, `GetModuleTypes()`, `GetNormalizerTypes()`, and an `AddArdeosAnalysis()` DI extension. `AddFellowshipHeroAnalysis()` in the client is generated from the `[GenerateHeroManifest]` marker and calls every hero's `Add{Hero}Analysis()`, so a new hero is picked up by adding its project reference. Keep it reflection-free.

**Module and analyzer lifecycle**

- `[AddAnalyzer<T>]` registers every type that subscribes to events, which is every `Analyzer`. `[AddModule<T>]` and `[AddState<T>]` register everything that is not one, and are exact synonyms.
- `[ForPull(PullKind.Single | PullKind.Multi, Boss = PullBoss.Boss)]` declared directly on the registered type is the only thing that decides lifetime. With it, a fresh instance is constructed for each matching pull and retained on the pull read surfaces; without it, one instance serves the whole report and is reachable through the generated `Parser.{Name}` accessor. It is valid only on a concrete `Analyzer`: an abstract base declares the shape, each concrete subclass its own filter.
- Subscribe with `[On<TEvent>(By = Actor.Player, Spell = ...)]` on handler methods; the generator wires each into `EventEmitter` with inlined predicates. Any other setup goes in the constructor.
- Accumulate during dispatch and expose results as get-only computed properties over retained state. An analyzer reads its own `Pull` property.
- Modules are constructed by a generator-emitted factory. `Owner` is assigned by the parser afterwards, so do **not** accept `CombatLogParser` in a module constructor. Declare a sibling-module dependency with `[Dependency<T>]` and read the generated accessor.
- Gate a module on a talent with `[RequiresTalent(ArdeosTalents.RollingFlames)]`, using the generated `{Hero}Talents` constants.
- Order modules relative to each other with `[Before<T>]` / `[After<T>]`. The guarantee is pairwise only: `[After<SpellUsable>]` means `SpellUsable` has seen the event by the time this module does, and nothing more. Modules with no constraint between them run in no guaranteed order. `Priority` is a design-time `virtual` override, not something the parser assigns.
- Razor reads analyzer instances directly: `Parser.SearingBlazeAnalyzers` for the cross-pull list, `Parser.For(pull).{Surface}` or `pull.{Surface}` for a single pull. Share a surface across pull shapes with a marker interface deriving from `IAnalyzerSurface`.
- A module contributes a statistics card by overriding `StatisticsComponentType => typeof(SomeStatistics)`; the parser collects those into `HeroAnalysisResult.Statistics` and the report page renders each with the module cascaded in.

**Spell data** - `data/spelldb.json` is produced offline by `FellowshipAnalyzer.SpellData` and turned into per-hero `Spells` registries by the source generator. Curate `data/overrides.json` (via SpellStudio or by hand) and rerun `rebuild-spelldb` rather than editing generated registries. Per-hero `Talents.cs` files stay hand-written. Spell ids are `FSLID` values, which offset effect, talent, and weapon ids into their own million-ranges.

The merge reads the highest-numbered `data/v*` export folder (`entities.jsonl` plus `settings.json`). A hero's kit is every ability record whose `heroes` array names that hero; an effect joins that ability through `partOf` and takes its member name from the ability's member name plus its `role`. An effect with a `partOf` but no `role` is not generated and belongs in `data/overrides.json`. Icons come from `abilities.json` at the repo root keyed by FSLID, not from the export's own `icon` values, which name `.png` files where the CDN serves `.jpg`.

## Where code goes

- Hero-specific logic → `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}`.
- Cross-hero shared analysis → `FellowshipAnalyzer.Core`.
- Reusable UI and SCSS → `src/FellowshipAnalyzer.Core/UI` and `src/FellowshipAnalyzer.Core/Styles`.
- Design tokens → `src/FellowshipAnalyzer.Core.Contracts/Design`, then run the `emit-palette` tool to regenerate `_palette.scss`; a drift test enforces that they match.
- Fellowship Logs API access → `FellowshipAnalyzer.Api` (or `Api.Core` / `Api.GraphQL`).
- Source generator changes → `FellowshipAnalyzer.Generators`.

**Never** add a project reference between two hero projects. If two heroes share behavior, move it into Core.

## Size discipline (Blazor WASM + AOT)

Every dependency in Core, Core.Contracts, the client, or a hero project is downloaded by the browser and AOT-compiled. Before adding a `PackageReference`:

- Ask whether it must run in the browser. Server-only deps belong in the API, AppHost, or dev-host projects.
- Prefer framework APIs and small helpers over utility libraries.

Package versions are **centrally managed** via layered `Directory.Packages.props` files (root enables CPM; `src/`, `src/Heroes/`, `tests/`, and `src/FellowshipAnalyzer.Tools/` each own their version sets). Project files use versionless `PackageReference` entries.

## Skills

When creating or modifying analysis modules, use the appropriate skill:

- **create-analyzer** - new event-driven analyzer (talent / ability / feature).
- **create-guide** - guide Razor component for the Guide tab.
- **create-statistics** - auto-collected statistics component.
- **create-resource-tracker** - new `ResourceTracker` subclass (orbs, mana, charges, energy).
- **create-normalizer** - `IEventNormalizer` for event preprocessing (reorder, link, fabricate).
- **create-hero** - scaffold a whole new hero project.
- **run-tool** - invoke a `src/FellowshipAnalyzer.Tools/` file-based dotnet tool.
- **analyze-event-schema** / **analyze-log-resources** - inspect raw report JSON to validate event classes or resource handling.

When adding CSS/SCSS to any component, creating a new Razor component with styles, or reviewing existing component styles for consistency, use:

- **style-guide** - SCSS setup, design tokens, class naming, scoped vs global styling, component patterns.

When writing or editing any rendered or documented text (guide prose, stat labels, tooltips, XML doc comments, public identifiers), use:

- **house-style** - the voice, clause types, grammar, and vocabulary for every text surface.

## Knowledge

- Every group is exactly 4 players; one (actorId, instance) can be rostered in two pulls, and a death is dispatched to every pull naming that unit.
- A pull ends when all its enemies are dead. GetEvents is complete for events with the player as source or target; deaths are the exception: the player stream logs only player-caused deaths, the deaths query's hostilityType is decided by the deceased, and anonymous report codes take the `a:` prefix.
- `filterExpression` knows only `target.name`, `ability.id`, and `type`; any other field is silently null.
- `CastEvent.Activation` marks the activation half of a cast and its doc comment states the opposite; skipping activations discards ~99% of casts.
- Flat percentage stats are additive; Spirit of Heroism is the +30% haste, under four ids; gem flat ratings are already inside combatantinfo totals; blessing ids are per-hero loadout nodes, matched by name.
- The FSL damage `type` field is not a school. `spelldb.json` is the sole school source, read with `Enum.Parse` and no fallback.
- `CooldownReducedByHaste` is hand-set and drifts, and per-hero `Talents.cs` lags a season; check both against the current `data/v*` before trusting a talent gate or a cooldown-readiness metric.
- Verify a mechanic in the fellowship-codex MCP before concluding behaviour from spelldb, flags, or hero_data Constants; a talent can override them entirely. Never reason from a duration or cooldown; a defensive is judged on uptime and on holding for heavy incoming damage.
- Ardeos: Cinder events need normalization before the tracker reads them, and each DoT runs its own aura model.
- Elarion: the mark is a stack pool Salvo spends; Impending Heartseeker owns the unlogged Barrage reset.
- Tariq: the execute gate is 30%; Chain Lightning is window-only; one Hammer Storm channel is 3 spins; Spirit is readable from events.
- Gunde: Rend conversion is additive percent-of-damage, Serrated Edge increases the whole cast's damage, Exsanguinate applies no Rend, and the Serrated Edge consumer order is build-dependent.
- Helena: s3 numbers take precedence over s2 on conflict, and Hold the Line owns the 10s row. Mara: Malevolence 2+2 is unreachable, so score by the better-stacked finisher.
- Xavian: Invictus and Rising Sun reset or shorten the Solar cooldowns, so SpellUsable fabricates holds.
- The six non-DPS heroes are wired stubs; missing tank and healer Core primitives come first. Ally HP exists via TargetResources and Overheal; ally damage taken, threat, and block-as-hit-type do not. Port HotTracker's attribution half only.
- Core's TestParser runs no normalizers: a test supplies its own bookend events, and a missing DungeonEndEvent fails as a silent zero. A tracker reading zero can also mean the generated subscriptions omitted a base `[On<>]` handler.
- Generated files under obj/ are stale; force an emit to scratch with `-t:Rebuild` before reading any `.g.cs`.
- A test harness must copy Program.cs's JsonSerializerOptions or events deserialize empty; case-insensitive property matching is required there.
- Razor `@<tag>` with an empty element name crashes the renderer; only `@<text>` is markup-only. Modules are ComponentBase: markup goes in `@<>` templates, never RenderTreeBuilder.
- An unstyled dev page is a cached styles shim, so hard reload; a `_content` import failing to fetch is a stopped DevHost backend.
- Fabricated events are never back-dated, a natural cooldown expiry fires at its true instant, and dungeon bookends are positional: DungeonStartEvent first, DungeonEndEvent last, never by timestamp.
- Rendered prose is analysis of this report: no guide-site content, no APL or rotation checking, no comparison to other players, no external sources named; method.gg is research input only.
- No Finding, Report, or ScoreCard concepts; typed data lives in analyzers, prose and tiers in Razor. The Statistics tab is optional interesting information, never a guide summary, and a gem card is one per rank effect.
- Files under src/Heroes contain zero comments of any kind, XML docs included. No stubs or polyfills, including a stub written only to get a build passing. Enums take no explicit integer values; a lazy property is `T Prop => field ??= Compute()` with a record class payload.
- A new data/v* export is intentional: update the stale fixture and rerun rebuild-spelldb, never revert. Legendary item ids are hero-shared by design. `/api/*` stays anonymous by design, and the visitor-facing privacy page names no backend vendors.
- The wire layer keeps the old API vocabulary on purpose (a `fight` is a Dungeon, a `dungeonPull` a Pull); player pets are deliberately removed, and `DungeonNpc.PetOwner` means enemy summons. Refute before asserting; batch uncertain domain wording into one round of questions.

## Reference / Inspiration

The project is loosely based on [WoWAnalyzer](https://github.com/WoWAnalyzer/WoWAnalyzer) (TypeScript/React). Some principles and patterns are followed, but the architecture is designed to take advantage of modern C# features. Always consider using the latest C# features when adapting patterns from WoWAnalyzer, and feel free to deviate from their architecture when it makes sense in the context of C# and Blazor. See [NOTICE.md](NOTICE.md) for credits.

**[WoWAnalyzer port-priority audit](https://claude.ai/code/artifact/ec122b55-fb51-4bd6-bf4d-9f1068cb9a41)** - owner-reviewed 0-10 port priorities for every WoWAnalyzer shared module, parser UI, and guide component, with per-item rationales, verified already-have equivalents in this codebase, shippable feature bundles, and a dependency-ordered build plan. Consult it when planning shared-infrastructure or hero-analyzer work, and keep it updated as modules are merged.
