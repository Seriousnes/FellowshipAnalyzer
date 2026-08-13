# FellowshipAnalyzer

FellowshipAnalyzer is a combat log analysis tool for the online RPG Fellowship. It reads reports from Fellowship Logs, runs hero-specific analyzers over combat events, and presents guide and statistics views that help players understand their performance.


## Project Shape

The solution is split into a few major areas:

- `src/FellowshipAnalyzer/FellowshipAnalyzer` - the standalone Blazor WebAssembly client that runs the analysis pipeline in the browser.
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Api` - the Azure Functions API for Fellowship Logs.
- `src/FellowshipAnalyzer.Core` - combat events, parser infrastructure, shared analysis modules, normalizers, spell data, source-generated JSON context, shared Razor UI under `UI/`, and SCSS tokens/mixins under `Styles/`.
- `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}` - one Razor class library per hero analyzer.
- `src/FellowshipAnalyzer.Generators` - source generators for analyzer registration, parser constructors, module accessors, event metadata, and spell registry data.
- `tests/` - unit and analyzer tests, including one test project per hero.

Hero projects are intentionally pre-created and separate. That is a contributor-experience choice as much as an architecture choice: contributors should be able to open the project for a hero, add a module, and stay inside that boundary. Cross-hero sharing should go through `FellowshipAnalyzer.Core` (shared UI lives under its `UI/` and `Styles/` folders), not direct references between hero projects.

For a deeper technical overview, see [.claude/instructions/FellowshipAnalyzer-Architecture-Overview.md](.claude/instructions/FellowshipAnalyzer-Architecture-Overview.md).

## Prerequisites

Install these before running the app locally:

- .NET 10 SDK
- Blazor WebAssembly workload, restored with `dotnet workload restore`
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

The app host starts DevHost (which serves the WebAssembly client and proxies `/api`) and DevApi. The app URL is `http://fellowshipanalyzer.dev.localhost:5120`; Aspire assigns the API port at runtime and shows it on the Aspire dashboard.

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
3. Register it on `{Hero}CombatLogParser.cs` with `[AddAnalyzer<T>]` (pull-lifetime gameplay analysis) or `[AddModule<T>]` / `[AddState<T>]` (dungeon-lifetime state).
4. Add a guide component under `Guides/` or a statistics component under `Statistics/` when there is player-facing output.
5. Add focused tests in `tests/FellowshipAnalyzer.Heroes.{Hero}.Tests`.

Modules are constructed per analysis run. Do constructor-time setup, subscribe to events with `[On<TEvent>]` attributes on instance methods, and expose derived values as read-only computed properties that guide and statistics components read directly.

## Source Generation And Registration

Hero analyzers are registered without runtime reflection. The parser source generator reads attributes like `[HeroAnalyzer(HeroName.Rime)]` and `[AddModule<T>]` and emits strongly typed constructors, module accessors, DI registration, and keyed `IHeroAnalyzer` registration.

The WebAssembly client calls a single reflection-free aggregate registration method in `FellowshipAnalyzer`:

```csharp
builder.Services.AddCoreAnalysisServices();
builder.Services.AddCoreAnalysis();
builder.Services.AddFellowshipHeroAnalysis();
```

`AddFellowshipHeroAnalysis()` is itself generated: it scans referenced assemblies at compile time for `[HeroAnalyzer]` parsers and calls each hero's `Add{Hero}Analysis()`, so adding a hero project reference is the whole wiring step.

## Package Versions

Package versions are centrally managed with layered `Directory.Packages.props` files. The root file enables central package management, [src/Directory.Packages.props](src/Directory.Packages.props) owns source package versions, [src/Heroes/Directory.Packages.props](src/Heroes/Directory.Packages.props) is reserved for hero-only browser-side packages, [tests/Directory.Packages.props](tests/Directory.Packages.props) owns test package versions, and [src/FellowshipAnalyzer.Tools/Directory.Packages.props](src/FellowshipAnalyzer.Tools/Directory.Packages.props) owns the file-based tool versions. Project files should use versionless `PackageReference` entries and keep only project-specific metadata such as `PrivateAssets`, `OutputItemType`, or `IncludeAssets`.

## Download Size Discipline

FellowshipAnalyzer is a Blazor WebAssembly app and release builds use AOT. That makes initial download size especially important.

When contributing:

- Avoid adding package references to the client, Core, or hero projects unless they are clearly needed in the browser.
- Keep server-only dependencies in the API or host projects.
- Prefer existing shared components and core services over new UI or analysis libraries.
- Compress and minimize static assets before adding them.
- Be careful moving code into `FellowshipAnalyzer.Core`; core code is shared by every analyzer.
- Treat the per-hero project boundary as a future-friendly point for lazy loading or packaging improvements.

## Codebase Knowledge Graph

`graphify-out/` holds a [graphify](https://github.com/Graphify-Labs/graphify) knowledge graph of this repository: what the code and docs contain, how the pieces relate, named communities, and a rendered report. It is committed so contributors and coding assistants share one graph instead of each paying to build their own.

Tracked: `graph.json`, `GRAPH_REPORT.md`, `manifest.json`, `.graphify_labels.json`.

Ignored, because they are per-person, per-machine, or pure rebuild churn:

- `graphify-out/cost.json`, which records your own API spend.
- `graphify-out/cache/`, which is large and rebuildable.
- `graphify-out/<YYYY-MM-DD>/`, the snapshot of the previous graph that every rebuild leaves behind. Each one is roughly 9 MB, so tracking them would grow the repo by that much per rebuild.
- `.graphify_python` and `.graphify_root`, which record paths from whichever machine ran the build.

This repo is past graphify's 5000-node limit for the interactive `graph.html` viz, so rebuilds skip it and it is not tracked. Read `GRAPH_REPORT.md`, or raise `GRAPHIFY_VIZ_NODE_LIMIT` locally if you want the HTML.

After cloning, install the CLI and run the hook installer once:

```powershell
uv tool install graphifyy
graphify hook install
```

Git hooks live in `.git/hooks` and are not versioned, so every contributor runs `graphify hook install` in their own clone. It sets up three things:

- A `post-commit` hook that re-extracts the files the commit touched and rebuilds `graph.json` and `GRAPH_REPORT.md`. This is AST parsing only, so it needs no API key and costs nothing. The rebuild runs detached and logs to `~/.cache/graphify-rebuild.log`, so `git commit` returns immediately.
- A `post-checkout` hook that rebuilds after a branch switch.
- A merge driver registered in local git config. [.gitattributes](.gitattributes) carries the matching `graphify-out/graph.json merge=graphify` line, so two branches that both rebuilt the graph union-merge instead of leaving conflict markers in a multi-megabyte JSON file.

Both hooks skip linked worktrees, no-op during rebase, merge, and cherry-pick, and skip commits that only touch `graphify-out/`. Set `GRAPHIFY_SKIP_HOOK=1` to suppress them for a single command.

On Windows, `graphify hook install` registers the merge driver as a backslash path to the Python interpreter. Git runs merge drivers through `sh`, which eats the backslashes, so the driver silently fails and the merge falls back to a conflicted multi-megabyte `graph.json`. Re-register it through the launcher instead:

```powershell
git config merge.graphify.driver "graphify merge-driver %O %A %B"
```

`graphify hook status` only reports that a driver is configured, not that it runs, so it says "registered" either way. Confirm the command itself with `git config --get merge.graphify.driver`.

Doc and image changes are outside the commit hook's scope. Rebuild the whole graph with `graphify extract .`, or `/graphify .` from Claude Code. That path does run an LLM backend and does cost tokens.

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
- [.claude/instructions/FellowshipAnalyzer-Architecture-Overview.md](.claude/instructions/FellowshipAnalyzer-Architecture-Overview.md) describes the analysis pipeline in more depth.
- [CombatMechanics.md](CombatMechanics.md) documents Fellowship combat mechanics relevant to analyzer work.
- [NOTICE.md](NOTICE.md) credits the upstream [WoWAnalyzer](https://github.com/WoWAnalyzer/WoWAnalyzer) project that FellowshipAnalyzer is based on.

## Acknowledgements

FellowshipAnalyzer would not exist without [WoWAnalyzer](https://github.com/WoWAnalyzer/WoWAnalyzer). It is a C# and Blazor WebAssembly port of WoWAnalyzer for Fellowship: many of the analysis patterns, module structures, guide and statistics concepts, and the overall event-driven design used here are adapted from that project. FellowshipAnalyzer is licensed under the [GNU Affero General Public License v3.0](LICENSE), the same license as WoWAnalyzer. See [NOTICE.md](NOTICE.md) for full credits.
