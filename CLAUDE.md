# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Critical

### Comments
- **IMPORTANT** - Never include comments referencing design docs or plan points. Comments are reserved exclusively for API/usage notes.
- **Never** add inline comments or comments within methods for any reason.

## Project

FellowshipAnalyzer parses and analyzes combat logs from the online RPG "Fellowship". Logs are uploaded to fellowshiplogs.com; this app calls the Fellowship Logs GraphQL API, runs hero-specific analyzers over combat events, and renders guide/statistics views.

- C# 14 / .NET 10. The product app is a standalone Blazor WebAssembly client that runs the whole analysis pipeline in the browser.
- Release WASM builds set `RunAOTCompilation`, so download size matters. See "Size discipline" below.
- Local orchestration via .NET Aspire (`FellowshipAnalyzer.AppHost`): dev API, static host for the WASM client, and an Azure Storage emulator.
- See [.claude/instructions/FellowshipAnalyzer-Architecture-Overview.md](.claude/instructions/FellowshipAnalyzer-Architecture-Overview.md) for the full analysis pipeline.
- See [CombatMechanics.md](CombatMechanics.md) for in-game combat mechanics relevant to analyzer work.

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
- **Never** add a project reference between two hero projects. If two heroes share behavior, move it into Core.

**Spell data** - `data/spelldb.json` is produced offline by `FellowshipAnalyzer.SpellData` and turned into per-hero `Spells` registries by the source generator. Curate `data/overrides.json` (via SpellStudio or by hand) and rerun `rebuild-spelldb` rather than editing generated registries. Per-hero `Talents.cs` files stay hand-written. Spell ids are `FSLID` values, which offset effect, talent, and weapon ids into their own million-ranges.

The merge reads the highest-numbered `data/v*` export folder (`entities.jsonl` plus `settings.json`). A hero's kit is every ability record whose `heroes` array names that hero; an effect joins that ability through `partOf` and takes its member name from the ability's member name plus its `role`. An effect with a `partOf` but no `role` is not generated and belongs in `data/overrides.json`. Icons come from `abilities.json` at the repo root keyed by FSLID, not from the export's own `icon` values, which name `.png` files where the CDN serves `.jpg`.

## Size discipline (Blazor WASM + AOT)

Every dependency in Core, Core.Contracts, the client, or a hero project is downloaded by the browser and AOT-compiled. Before adding a `PackageReference`:

- Ask whether it must run in the browser. Server-only deps belong in the API, AppHost, or dev-host projects.
- Prefer framework APIs and small helpers over utility libraries.

Package versions are **centrally managed** via layered `Directory.Packages.props` files (root enables CPM; `src/`, `src/Heroes/`, `tests/`, and `src/FellowshipAnalyzer.Tools/` each own their version sets). Project files use versionless `PackageReference` entries.