# Contributing

Thanks for helping with FellowshipAnalyzer. The project is designed so most gameplay contributions can stay inside one hero project, with shared infrastructure handled by the core libraries and source generators.

## Before You Start

- Read the relevant section of [README.md](README.md) for setup and project layout.
- Skim [.claude/instructions/FellowshipAnalyzer-Architecture-Overview.md](.claude/instructions/FellowshipAnalyzer-Architecture-Overview.md) if you are touching parser, module, normalizer, or UI integration code.
- Check [CombatMechanics.md](CombatMechanics.md) when analyzer behavior depends on game mechanics.
- Review [NOTICE.md](NOTICE.md) for the project's lineage from [WoWAnalyzer](https://github.com/WoWAnalyzer/WoWAnalyzer). FellowshipAnalyzer is distributed under the same AGPL-3.0 license, and contributions are accepted on those terms.
- Prefer small, focused changes. A single analyzer, guide section, normalizer, or bug fix is easier to review than a broad rewrite.

## Local Setup

Before running the app locally, install:

- .NET 10 SDK with the Blazor WebAssembly workload (`dotnet workload restore`)
- Docker, required by Aspire to run local service containers

From the repository root:

```powershell
dotnet workload restore
dotnet restore
dotnet build FellowshipAnalyzer.slnx -nologo --verbosity minimal
```

To run the app locally:

```powershell
dotnet run --project src/FellowshipAnalyzer.AppHost/FellowshipAnalyzer.AppHost.csproj
```

The local API needs Fellowship Logs credentials. The recommended setup is user secrets on the DevApi project:

```powershell
dotnet user-secrets set "FellowshipLogs:ClientId" "your-client-id" --project src/FellowshipAnalyzer/FellowshipAnalyzer.DevApi
dotnet user-secrets set "FellowshipLogs:ClientSecret" "your-client-secret" --project src/FellowshipAnalyzer/FellowshipAnalyzer.DevApi
```

Alternatively use environment variables with double-underscore notation (`FellowshipLogs__ClientId`). Aspire does not forward the AppHost's user secrets to child projects, so secrets set against the AppHost project will not work.

## Choosing Where Code Goes

Use these boundaries when deciding where to put a change:

- Hero-specific analyzer logic belongs in `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}`.
- Shared combat event types, parser infrastructure, filters, base analyzers, and source-generated spell data belong in `src/FellowshipAnalyzer.Core`.
- Reusable Razor UI belongs in `src/FellowshipAnalyzer.Core/UI` (with SCSS tokens/mixins in `src/FellowshipAnalyzer.Core/Styles`).
- Fellowship Logs API access belongs in `src/FellowshipAnalyzer.Core/FellowshipLogs` (client side) or `src/FellowshipAnalyzer/FellowshipAnalyzer.Api` with `Api.Core` / `Api.GraphQL` (server side).
- Source generator changes belong in `src/FellowshipAnalyzer.Generators`.

Do not add direct references from one hero project to another. If two heroes need the same behavior, move the genuinely shared piece into core.

## Hero Analyzer Contributions

Most contributors should start in the pre-created project for the hero they want to improve.

A normal analyzer change usually includes:

1. `Modules/Abilities.cs` updates for spell metadata, cooldowns, charges, costs, GCD behavior, or hidden related spell IDs.
2. A module under `Modules/` that inherits from `Analyzer`, `ResourceTracker`, `Auras`, or another existing base type.
3. An `[AddAnalyzer<T>]` entry on `{Hero}CombatLogParser.cs` for pull-lifetime gameplay analysis, or `[AddModule<T>]` / `[AddState<T>]` for fight-lifetime state.
4. A guide component under `Guides/` or statistics component under `Statistics/` when the result should be visible to users.
5. Tests in `tests/FellowshipAnalyzer.Heroes.{Hero}.Tests`.

Module lifecycle expectations:

- Do setup in the constructor and subscribe to events with `[On<TEvent>]` attributes on instance methods.
- Track state while events dispatch.
- Expose derived values as read-only computed properties; there is no finalization hook.
- Declare sibling-module dependencies with `[Uses<T>]` (or use `Owner.GetModule<T>()` for ad-hoc reads).

## Source Generation Rules

Analyzer discovery and DI registration are source-generated and reflection-free because the client runs as Blazor WebAssembly with AOT.

Use the existing attributes instead of runtime scanning:

```csharp
[HeroAnalyzer(HeroName.Rime)]
[AddState<WinterOrbTracker>]
[AddAnalyzer<SingleTargetEmbraceWindowAnalyzer>]
[AddAnalyzer<AoeEmbraceWindowAnalyzer>]
public sealed partial class RimeCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(RimeGuide);
}
```

If a future hero is added, reference its project from the client; the generated `AddFellowshipHeroAnalysis()` discovers `[HeroAnalyzer]` parsers at compile time and calls each hero's `Add{Hero}Analysis()`. Do not introduce runtime reflection for analyzer registration.

## Package And Dependency Rules

Package versions live in layered `Directory.Packages.props` files. The root file enables central package management, `src/Directory.Packages.props` owns source package versions, `src/Heroes/Directory.Packages.props` is reserved for hero-only browser-side packages, and `tests/Directory.Packages.props` owns test package versions. Do not add `Version` attributes to individual `PackageReference` entries.

Before adding a package, ask whether it must be downloaded by the browser. Blazor WebAssembly AOT makes dependency size expensive, especially in hero, client, core, and shared component projects.

Prefer:

- Existing framework APIs over new dependencies.
- Small helper methods over broad utility libraries.
- Server/API dependencies only in server/API projects.
- Shared components only when reuse is real.

## UI And Styling

Reusable display pieces belong in `FellowshipAnalyzer.Core/UI`; hero-specific guide and statistics components stay in the hero project.

When adding styles:

- Use `.razor.scss` alongside the component.
- Reuse component design tokens and existing patterns.
- Keep guide and statistics UI data-driven from analyzer state.
- Avoid adding large image or media assets unless the feature clearly needs them.

## Tests And Validation

At minimum, run a build before opening a pull request:

```powershell
dotnet build FellowshipAnalyzer.slnx -nologo --verbosity minimal
```

For analyzer work, add or update focused tests in the relevant hero test project. Run that project directly when possible:

```powershell
dotnet test tests/FellowshipAnalyzer.Heroes.Rime.Tests/FellowshipAnalyzer.Heroes.Rime.Tests.csproj --no-build
```

For source generator or shared core changes, also run the matching core/generator tests.

## Pull Request Checklist

Before submitting, check that:

- The solution builds.
- New analyzer behavior has focused test coverage or a clear reason why it cannot.
- Hero-specific code stays inside its hero project.
- Shared abstractions are justified by more than one current use case.
- New package versions were added to the nearest appropriate `Directory.Packages.props` file.
- Browser-facing dependencies and assets are necessary and size-conscious.
- User-facing missing data is surfaced as loading or error state, not hidden behind placeholder IDs or fallback labels.

## Need Help?

Open an issue or discussion with the report, hero, ability, and behavior you are trying to analyze. If you have a sample log, include enough context to reproduce the relevant event sequence without exposing private information.
