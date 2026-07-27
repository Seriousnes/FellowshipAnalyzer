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
FellowshipLogs GraphQL JSON (player events, plus the fight death stream merged in by EventStreamMerger)
  → event deserialization (polymorphic on Event)
  → CombatLogParser.Analyze
  → IEventNormalizer passes in Priority order (bookend pulls, rescale resources, attach master data, link casts)
  → parse-lifetime modules constructed; RegisterSubscriptions() wires their [On<TEvent>] handlers
  → EventEmitter dispatches; PullStartEvent / PullEndEvent open and close each pull,
    constructing a fresh set of [AddAnalyzer] analyzers per pull and retaining them afterwards
  → modules and analyzers expose derived metrics as computed get-only properties over retained state
  → HeroAnalysisResult → Razor guide and statistics components
```

**Solution layout** (only the bits that aren't obvious from `ls`):

- `src/FellowshipAnalyzer.Core` - events, parser infrastructure, base modules/normalizers, spell registries, JSON source-generated context, shared Razor UI under `UI/` (Components, Charts, Guides, Timeline, Diagnostics, Theming) and SCSS under `Styles/`. Shared by every hero; code added here ships to every client.
- `src/FellowshipAnalyzer.Core.Contracts` - DTOs/interfaces that cross the API/client boundary, the `FSLID` spell-id struct, and the C# design tokens (`FaTheme`, `FaPalette`, `FaToken`, `FaVar`).
- `src/FellowshipAnalyzer.Generators` - Roslyn **source** generators (parser ctor, typed module accessors, pull-analyzer surfaces, module/normalizer type lists, spell registries, talent id constants, hero DI manifest). Hero registration is **reflection-free** for AOT.
- `src/FellowshipAnalyzer.Analyzers` - Roslyn **diagnostic** analyzers (FA00xx), distinct from gameplay "analyzers".
- `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}` - one Razor class library per hero. **Ardeos and Elarion are the most built-out; Rime is the compact reference that covers modules, guides, and statistics.**
- `src/FellowshipAnalyzer/FellowshipAnalyzer` - the Blazor WASM client. `FellowshipAnalyzer.Api` (Azure Functions), `Api.Core`, and `Api.GraphQL` cover Fellowship Logs access; `DevApi` and `DevHost` are the Aspire-wired local variants.
- `src/FellowshipAnalyzer.SpellData` and `src/FellowshipAnalyzer.SpellStudio` - offline merge engine that produces `data/spelldb.json`, plus the Blazor Server app for curating `data/overrides.json`.
- `src/FellowshipAnalyzer.DesignSystem` - Blazor Server showcase hosting the runtime design-token editor.
- `src/FellowshipAnalyzer.Tools` - file-based `dotnet` scripts. Use the **run-tool** skill.
- `src/FellowshipAnalyzer.AppHost` and `src/FellowshipAnalyzer.ServiceDefaults` - Aspire orchestration and shared service defaults.

**Hero parser pattern** - small and declarative; the source generator does the heavy lifting:

```csharp
[HeroAnalyzer(HeroName.Ardeos)]
[AddModule<Abilities>]
[AddModule<CinderEmberTracker>]
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

- `[AddState<T>]` and `[AddModule<T>]` register parse-lifetime modules. `[AddAnalyzer<T>]` registers pull-lifetime `Analyzer`s, constructed fresh for each pull and selected by `[ForPull(PullKind.Single | PullKind.Multi, Boss = PullBoss.Boss)]`.
- Subscribe with `[On<TEvent>(By = Actor.Player, Spell = ...)]` on handler methods; the generator wires each into `EventEmitter` with inlined predicates. Any other setup goes in the constructor.
- Accumulate during dispatch and expose results as get-only computed properties over retained state. An analyzer reads its own `Pull` property.
- Modules are constructed by a generator-emitted factory. `Owner` and `Priority` are assigned by the parser afterwards, so do **not** accept `CombatLogParser` in a module constructor. Declare a sibling-module dependency with `[Dependency<T>]` and read the generated accessor.
- Gate a module on a talent with `[RequiresTalent(ArdeosTalents.RollingFlames)]`, using the generated `{Hero}Talents` constants.
- Order modules relative to each other with `[Before<T>]` / `[After<T>]`; declaration order is the tie-break.
- Razor reads analyzer instances directly: `Parser.SearingBlazeAnalyzers` for the cross-pull list, `Parser.For(pull).{Surface}` or `pull.{Surface}` for a single pull. Share a surface across pull shapes with a marker interface deriving from `IAnalyzerSurface`.
- A module contributes a statistics card by overriding `StatisticsComponentType => typeof(SomeStatistics)`; the parser collects those into `HeroAnalysisResult.Statistics` and the report page renders each with the module cascaded in.

**Spell data** - `data/spelldb.json` is produced offline by `FellowshipAnalyzer.SpellData` and turned into per-hero `Spells` registries by the source generator. Curate `data/overrides.json` (via SpellStudio or by hand) and rerun `rebuild-spelldb` rather than editing generated registries. Per-hero `Talents.cs` files stay hand-written. Spell ids are `FSLID` values, which offset effect, talent, and weapon ids into their own million-ranges.

## Where code goes

- Hero-specific logic → `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}`.
- Cross-hero shared analysis → `FellowshipAnalyzer.Core`.
- Reusable UI and SCSS → `src/FellowshipAnalyzer.Core/UI` and `src/FellowshipAnalyzer.Core/Styles`.
- Design tokens → `src/FellowshipAnalyzer.Core.Contracts/Design`, then run the `emit-palette` tool to regenerate `_palette.scss`; a drift test enforces that they match.
- Fellowship Logs API access → `FellowshipAnalyzer.Api` (or `Api.Core` / `Api.GraphQL`).
- Source generator changes → `FellowshipAnalyzer.Generators`.

**Never** add a project reference between two hero projects. If two heroes share behavior, lift it into Core.

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

## Reference / Inspiration

The project is loosely based on [WoWAnalyzer](https://github.com/WoWAnalyzer/WoWAnalyzer) (TypeScript/React). Some principles and patterns are followed, but the architecture is designed to take advantage of modern C# features. Always consider using the latest C# features when adapting patterns from WoWAnalyzer, and feel free to deviate from their architecture when it makes sense in the context of C# and Blazor. See [NOTICE.md](NOTICE.md) for credits.

**[WoWAnalyzer port-priority audit](https://claude.ai/code/artifact/ec122b55-fb51-4bd6-bf4d-9f1068cb9a41)** - owner-reviewed 0-10 port priorities for every WoWAnalyzer shared module, parser UI, and guide component, with per-item rationales, verified already-have equivalents in this codebase, shippable feature bundles, and a dependency-ordered build roadmap. Consult it when planning shared-infrastructure or hero-analyzer work, and keep it updated as modules land.
