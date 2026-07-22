# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Critical

### Comments
- **IMPORTANT** - Never include comments referencing design docs or plan.md points. Comments are reserved exclusively for API/usage notes.
- **Never** add inline comments or comments within methods for any reason.

## Project

FellowshipAnalyzer parses and analyzes combat logs from the online RPG "Fellowship". Logs are uploaded to fellowshiplogs.com; this app calls the Fellowship Logs GraphQL API, runs hero-specific analyzers over combat events, and renders guide/statistics views.

- C# 14 / .NET 10, Blazor Interactive-Auto (Blazor Server first, then transitions to WebAssembly).
- Local orchestration via .NET Aspire (`FellowshipAnalyzer.AppHost`).
- Release WebAssembly builds use AOT, so download size matters — see "Size discipline" below.
- See [.claude/instructions/FellowshipAnalyzer-Architecture-Overview.md](.claude/instructions/FellowshipAnalyzer-Architecture-Overview.md) for the full analysis pipeline.
- See [CombatMechanics.md](CombatMechanics.md) for in-game combat mechanics relevant to analyzer work.

## Common commands

```powershell
# First-time setup
dotnet workload restore
dotnet restore

# Build the whole solution
dotnet build FellowshipAnalyzer.slnx -nologo --verbosity minimal

# Run the full app locally (Blazor host + API via Aspire)
dotnet run --project src/FellowshipAnalyzer.AppHost/FellowshipAnalyzer.AppHost.csproj

# Run all tests
dotnet test FellowshipAnalyzer.slnx --no-build

# Run tests for a single hero
dotnet test tests/FellowshipAnalyzer.Heroes.Rime.Tests/FellowshipAnalyzer.Heroes.Rime.Tests.csproj --no-build

# Run a single test by name filter
dotnet test tests/FellowshipAnalyzer.Heroes.Rime.Tests/FellowshipAnalyzer.Heroes.Rime.Tests.csproj --no-build --filter "FullyQualifiedName~SomeTestName"
```

The local API requires Fellowship Logs credentials on the **DevApi** project (Aspire does not forward AppHost user secrets to children):

```powershell
dotnet user-secrets set "FellowshipLogs:ClientId" "..." --project src/FellowshipAnalyzer/FellowshipAnalyzer.DevApi
dotnet user-secrets set "FellowshipLogs:ClientSecret" "..." --project src/FellowshipAnalyzer/FellowshipAnalyzer.DevApi
```

Default URLs via AppHost: app at `http://fellowshipanalyzer.dev.localhost:5120`, API at `http://localhost:5123`.

## Architecture

Per-report, per-hero analysis pipeline:

```
FellowshipLogs API JSON
  → event deserialization (polymorphic on Event)
  → CombatLogParser.Analyze
  → IEventNormalizer pass (mutate/reorder/fabricate events)
  → Module.Initialize (subscribe via Events.* fluent filters)
  → EventEmitter dispatches events to subscribers
  → Module.Complete (compute derived metrics)
  → HeroAnalysisResult → Razor guide / statistics components
```

**Solution layout** (only the bits that aren't obvious from `ls`):

- `src/FellowshipAnalyzer.Core` — events, parser infrastructure, base modules/normalizers, spell data, JSON source-generated context. Shared by every hero; adding code here ships to every client.
- `src/FellowshipAnalyzer.Core.Contracts` — DTOs/interfaces that cross the API ↔ client boundary.
- `src/FellowshipAnalyzer.Generators` — Roslyn **source** generators (parser ctor, typed module accessors, module/normalizer type lists, DI registration). Hero analyzers are registered **reflection-free** for AOT.
- `src/FellowshipAnalyzer.Analyzers` — Roslyn **diagnostic** analyzers (distinct from gameplay "analyzers").
- `src/FellowshipAnalyzer.Components` — shared Razor components and SCSS tokens/mixins.
- `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}` — one Razor class library per hero. Shipped heroes: Aeona, Ardeos, Elarion, Helena, Mara, Meiko, Rime, Sylvie, Tariq, Vigour, Xavian. **Rime is the most complete reference.**
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Api` — Azure Functions API for Fellowship Logs. `DevApi` / `DevHost` are local-dev variants wired into Aspire.
- `src/FellowshipAnalyzer.Tools` — file-based `dotnet` scripts (update-spells, fetch-abilities). Use the **run-tool** skill.

**Hero parser pattern** — small and declarative; the source generator does the heavy lifting:

```csharp
[HeroAnalyzer(HeroName.Rime)]
[AddState<WinterOrbTracker>]
[AddAnalyzer<SingleTargetRimeCombo>]
[AddAnalyzer<AoERimeCombo>]
[AddModule<Modules.Abilities>]
public sealed partial class RimeCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(RimeGuide);
}
```

The generator emits the constructor, strongly-typed module properties (e.g. `.WinterOrbTracker`), `GetModuleTypes()`, `GetNormalizerTypes()`, and an `AddRimeAnalysis()` DI extension. When adding a new hero, register its `Add{Hero}Analysis()` inside `AddFellowshipHeroAnalysis()` in the client — **do not** introduce runtime reflection.

**Module lifecycle** — modules are scoped DI services. `Owner` and `Priority` are assigned by the parser after DI resolution, so do **not** accept `CombatLogParser` in module constructors. Subscribe in `Initialize()`, accumulate during dispatch, finalize in `Complete()`, expose read-only state to UI. Use `Owner.GetModule<T>()` or generated parser properties for module-to-module access.

## Where code goes

- Hero-specific logic → `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}`.
- Cross-hero shared analysis → `FellowshipAnalyzer.Core`.
- Reusable UI → `FellowshipAnalyzer.Components`.
- Fellowship Logs API access → `FellowshipAnalyzer.Api` (or `Api.Core` / `Api.GraphQL`).
- Source generator changes → `FellowshipAnalyzer.Generators`.

**Never** add a project reference between two hero projects — if two heroes share behavior, lift it to core or components.

## Size discipline (Blazor WASM + AOT)

Every dependency in core, components, client, or a hero project is downloaded by the browser and AOT-compiled. Before adding a `PackageReference`:

- Ask whether it must run in the browser. Server-only deps go in the API/host projects.
- Prefer framework APIs and small helpers over utility libraries.

Package versions are **centrally managed** via layered `Directory.Packages.props` files (root enables CPM; `src/`, `src/Heroes/`, and `tests/` each own their version sets). Project files use versionless `PackageReference` entries.

## Skills

When creating or modifying analysis modules, use the appropriate skill:

- **create-analyzer** — new event-driven analyzer (talent / ability / feature).
- **create-guide** — guide Razor component for the Guide tab.
- **create-statistics** — auto-collected statistics component.
- **create-resource-tracker** — new `ResourceTracker` subclass (orbs, mana, charges, energy).
- **create-normalizer** — `IEventNormalizer` for event preprocessing (reorder, link, fabricate).
- **create-hero** — scaffold a whole new hero project.
- **run-tool** — invoke a `src/FellowshipAnalyzer.Tools/` file-based dotnet tool.
- **analyze-event-schema** / **analyze-log-resources** — inspect raw `raw-report.json` files to validate event classes or resource handling.

When adding CSS/SCSS to any component, creating a new Razor component with styles, or reviewing existing component styles for consistency, use:

- **style-guide** — SCSS setup, design tokens, class naming, scoped vs global styling, component patterns.

## Reference / Inspiration

The project is loosely based on [WoWAnalyzer](https://github.com/WoWAnalyzer/WoWAnalyzer) (TypeScript/React). Some principles and patterns are followed, but the architecture is designed to take advantage of modern C# features. Always consider using the latest C# features when adapting patterns from WoWAnalyzer, and feel free to deviate from their architecture when it makes sense in the context of C# and Blazor. See [NOTICE.md](NOTICE.md) for credits.

**[WoWAnalyzer port-priority audit](https://claude.ai/code/artifact/ec122b55-fb51-4bd6-bf4d-9f1068cb9a41)** - owner-reviewed 0-10 port priorities for every WoWAnalyzer shared module, parser UI, and guide component, with per-item rationales, verified already-have equivalents in this codebase, shippable feature bundles, and a dependency-ordered build roadmap. Consult it when planning shared-infrastructure or hero-analyzer work, and keep it updated as modules land.
