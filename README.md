# FellowshipAnalyzer

FellowshipAnalyzer is a combat log analysis tool for the online RPG Fellowship. It reads reports from Fellowship Logs, runs hero-specific analyzers over combat events, and presents guide and statistics views that help players understand their performance.

The project is inspired by WoWAnalyzer, but it is built as a modern C# and Blazor application with a stronger compile-time separation between hero analyzers.

## Project Shape

The solution is split into a few major areas:

- `src/FellowshipAnalyzer/FellowshipAnalyzer` - the Blazor host application.
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Client` - the Blazor WebAssembly client that performs analysis in the browser.
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Api` - the Azure Functions API for Fellowship Logs.
- `src/FellowshipAnalyzer.Core` - combat events, parser infrastructure, shared analysis modules, normalizers, spell data, and source-generated JSON context.
- `src/FellowshipAnalyzer.Components` - shared Razor components and styling used by guides and statistics.
- `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}` - one Razor class library per hero analyzer.
- `src/FellowshipAnalyzer.Generators` - source generators for analyzer registration, parser constructors, module accessors, event metadata, and spell registry data.
- `tests/` - unit and analyzer tests, including one test project per hero.

Hero projects are intentionally pre-created and separate. That is a contributor-experience choice as much as an architecture choice: contributors should be able to open the project for a hero, add a module, and stay inside that boundary. Cross-hero sharing should go through `FellowshipAnalyzer.Core` or `FellowshipAnalyzer.Components`, not direct references between hero projects.

For a deeper technical overview, see [.github/instructions/FellowshipAnalyzer-Architecture-Overview.md](.github/instructions/FellowshipAnalyzer-Architecture-Overview.md).

## Prerequisites

Install these before running the app locally:

- .NET 10 SDK
- Blazor WebAssembly workload, restored with `dotnet workload restore`
- Azure Functions Core Tools v4, required by the local API when running through Aspire
- Docker, required by Aspire to run local service containers
- Optional: Visual Studio or VS Code with C# Dev Kit

The API needs Fellowship Logs credentials. For local development, set them as user secrets on the DevApi project:

```powershell
dotnet user-secrets set "FellowshipLogs:ClientId" "your-client-id" --project src/FellowshipAnalyzer/FellowshipAnalyzer.DevApi
dotnet user-secrets set "FellowshipLogs:ClientSecret" "your-client-secret" --project src/FellowshipAnalyzer/FellowshipAnalyzer.DevApi
```

Alternatively, use environment variables or a launch profile with double-underscore notation (`FellowshipLogs__ClientId`). Configuration also supports `FellowshipLogs:TokenEndpoint` and `FellowshipLogs:GraphQlEndpoint` if you need to override the defaults.


## Getting Started

From the repository root:

```powershell
dotnet workload restore
dotnet restore
dotnet build FellowshipAnalyzer.slnx -nologo --verbosity minimal
```

Run the full local app through Aspire:

```powershell
dotnet run --project src/FellowshipAnalyzer.AppHost/FellowshipAnalyzer.AppHost.csproj
```

The app host wires together the Blazor host and Azure Functions API. The default HTTP app URL is `http://fellowshipanalyzer.dev.localhost:5120`, and the API runs on `http://localhost:5123`.

## Working On A Hero Analyzer

Most gameplay work happens in `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}`. The current layout is:

```text
src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/
  {Hero}CombatLogParser.cs
  {Hero}Guide.razor
  _Imports.razor
  Modules/
    Abilities.cs
    {Feature}Analyzer.cs
    {Resource}Tracker.cs
  Guides/
    {Feature}Guide.razor
  Statistics/
    {Feature}Statistics.razor
  Normalizers/
    {Feature}Normalizer.cs
```

A typical analyzer contribution looks like this:

1. Add or update spell metadata in `Modules/Abilities.cs`.
2. Add an analyzer module under `Modules/`.
3. Register the module with `[AddModule<T>]` on `{Hero}CombatLogParser.cs`.
4. Add a guide component under `Guides/` or a statistics component under `Statistics/` when there is player-facing output.
5. Add focused tests in `tests/FellowshipAnalyzer.Heroes.{Hero}.Tests`.

Modules are scoped services resolved per analysis run. Put event subscriptions in `Initialize()`, compute final derived values in `Complete()`, and expose read-only state for guide and statistics components.

## Source Generation And Registration

Hero analyzers are registered without runtime reflection. The parser source generator reads attributes like `[HeroAnalyzer(HeroName.Rime)]` and `[AddModule<T>]` and emits strongly typed constructors, module accessors, DI registration, and keyed `IHeroAnalyzer` registration.

The WebAssembly client calls a single reflection-free aggregate registration method in `FellowshipAnalyzer.Client`:

```csharp
builder.Services.AddCoreAnalysisServices();
builder.Services.AddCoreAnalysis();
builder.Services.AddFellowshipHeroAnalysis();
```

When adding a hero in the future, add its generated `Add{Hero}Analysis()` call to `AddFellowshipHeroAnalysis()`.

## Package Versions

Package versions are centrally managed with layered `Directory.Packages.props` files. The root file enables central package management, [src/Directory.Packages.props](src/Directory.Packages.props) owns source package versions, [src/Heroes/Directory.Packages.props](src/Heroes/Directory.Packages.props) is reserved for hero-only browser-side packages, and [tests/Directory.Packages.props](tests/Directory.Packages.props) owns test package versions. Project files should use versionless `PackageReference` entries and keep only project-specific metadata such as `PrivateAssets`, `OutputItemType`, or `IncludeAssets`.

## Download Size Discipline

FellowshipAnalyzer is a Blazor WebAssembly app and release builds use AOT. That makes initial download size especially important.

When contributing:

- Avoid adding package references to the client, components, or hero projects unless they are clearly needed in the browser.
- Keep server-only dependencies in the API or host projects.
- Prefer existing shared components and core services over new UI or analysis libraries.
- Compress and minimize static assets before adding them.
- Be careful moving code into `FellowshipAnalyzer.Core`; core code is shared by every analyzer.
- Treat the per-hero project boundary as a future-friendly point for lazy loading or packaging improvements.

## Useful Commands

```powershell
# Restore workloads and packages
dotnet workload restore
dotnet restore

# Build everything
dotnet build FellowshipAnalyzer.slnx -nologo --verbosity minimal

# Run all tests
dotnet test FellowshipAnalyzer.slnx --no-build

# Run the Aspire app host
dotnet run --project src/FellowshipAnalyzer.AppHost/FellowshipAnalyzer.AppHost.csproj
```

## More Detail

- [CONTRIBUTING.md](CONTRIBUTING.md) explains contribution expectations and pull request checks.
- [.github/instructions/FellowshipAnalyzer-Architecture-Overview.md](.github/instructions/FellowshipAnalyzer-Architecture-Overview.md) describes the analysis pipeline in more depth.
- [CombatMechanics.md](CombatMechanics.md) documents Fellowship combat mechanics relevant to analyzer work.
