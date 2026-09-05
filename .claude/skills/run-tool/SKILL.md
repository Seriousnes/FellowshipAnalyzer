---
name: run-tool
description: "Run a .NET file-based tool from src/FellowshipAnalyzer.Tools/. Use when: rebuilding spelldb.json, regenerating the palette, fetching reports or abilities from the FellowshipLogs API, or executing any file-based dotnet tool script."
argument-hint: "Name of the tool to run (e.g. rebuild-spelldb, fetch-report)"
---

# Run Tool

Execute a .NET 10 file-based app from `src/FellowshipAnalyzer.Tools/`.

## Available Tools

| Script | Purpose | Usage |
|--------|---------|-------|
| `rebuild-spelldb.cs` | Regenerates `data/spelldb.json` from the SpellData merge engine; the source of every generated per-hero `Spells` registry. Hand corrections go in `data/overrides.json`, never in the output. | `dotnet run --no-cache src/FellowshipAnalyzer.Tools/rebuild-spelldb.cs` |
| `emit-palette.cs` | Renders `src/FellowshipAnalyzer.Core/Styles/_palette.scss` from the C# design tokens. `PaletteScssDriftTests.Committed_Palette_Matches_The_Theme` fails the build if the committed file is stale. | from `src/FellowshipAnalyzer.Tools`: `dotnet run --no-cache emit-palette.cs "../FellowshipAnalyzer.Core/Styles/_palette.scss"` |
| `fetch-report.cs` | Fetches one player's event stream for a dungeon and writes it to the gitignored `raw-reports/{code}-f{dungeon}-s{source}.json`. Requires credentials. | `dotnet run src/FellowshipAnalyzer.Tools/fetch-report.cs <code> <dungeonId> <sourceId> [outputPath]` |
| `refresh-schema.cs` | Fetches a fresh GraphQL introspection result from the FellowshipLogs API and writes SDL to `src/FellowshipAnalyzer/FellowshipAnalyzer.Api.GraphQL/schema.graphql`. Takes no arguments and always hits the network, so it requires credentials. | `dotnet run src/FellowshipAnalyzer.Tools/refresh-schema.cs` |
| `probe-deaths.cs` | Probes the Deaths GraphQL query semantics (defaults to the RaMDvgzWXBCnF4QT/16/25 example report; has a `--scan` mode). Requires credentials. | `dotnet run src/FellowshipAnalyzer.Tools/probe-deaths.cs` |
| `event-schema.cs` | Scans a log JSON events array and prints every unique event type with all properties each type can have, their frequency, and JSON value kinds. Use for C# model comparison and deserialization audits. | `dotnet run src/FellowshipAnalyzer.Tools/event-schema.cs <log-json>` |
| `resource-analysis.cs` | Analyzes `sourceResources.resources` across a log JSON and prints a Markdown summary of unique resource types, change patterns, and common event/ability pairings. | `dotnet run src/FellowshipAnalyzer.Tools/resource-analysis.cs <log-json>` |
| `measure-haste-stacking.cs` | Measures, from real logs, whether a flat percentage haste buff adds to rating-derived haste or multiplies it, by comparing periodic tick intervals inside and outside Spirit of Heroism windows. Reads every report in a directory. | `dotnet run src/FellowshipAnalyzer.Tools/measure-haste-stacking.cs raw-reports` |
| `update-spells.cs` | Rewrites name/icon literals in a hand-written `Spell` declaration file. Per-hero `Spells` registries are generated from `data/spelldb.json`, so this tool no longer applies to them; use `rebuild-spelldb.cs` and `data/overrides.json` instead. Reach for it only when a genuinely hand-written declaration file needs refreshing, such as `src/FellowshipAnalyzer.Core/Common/Spells/{Hero}/Talents.cs`. | `dotnet run src/FellowshipAnalyzer.Tools/update-spells.cs <events-json> <target-cs>` |

## Procedure

### Credentials (fetch-report, refresh-schema, probe-deaths)

The API tools read the user-secrets store id from the `.env` file at the repo root. That file is git-tracked and already holds the non-secret `USER_SECRET_ID=fellowshipanalyzer-devapi`; no copying is needed. Populate the user-secrets store it names with the credentials:

```
dotnet user-secrets set "FellowshipLogs:ClientId" "..."     --id fellowshipanalyzer-devapi
dotnet user-secrets set "FellowshipLogs:ClientSecret" "..." --id fellowshipanalyzer-devapi
```

### rebuild-spelldb

1. Make curation edits in `data/overrides.json` (sparse `Spell` overlays by scope and member). Never hand-edit `data/spelldb.json`.
2. Run from the repo root with `--no-cache` (see Notes):
   ```
   dotnet run --no-cache src/FellowshipAnalyzer.Tools/rebuild-spelldb.cs
   ```
3. Verify with the SpellData reproducibility tests: `dotnet test tests/FellowshipAnalyzer.SpellData.Tests/... `. The merge reads whichever `data/v*` export folder carries the highest build number, so adding a newer export changes what those tests assert against.

### emit-palette

Run after any change to `FaPalette`/`FaTheme`/`FaTypography`/`FaMetrics`/`FaElevation` in `FellowshipAnalyzer.Core.Contracts/Design`:

```
cd src/FellowshipAnalyzer.Tools
dotnet run --no-cache emit-palette.cs "../FellowshipAnalyzer.Core/Styles/_palette.scss"
```

The drift test catches a forgotten run, so a stale committed `_palette.scss` fails the build rather than shipping.

### fetch-report

1. Run from the repo root with the report code (anonymous reports keep their `a:` prefix), dungeon id, and source (player) id:
   ```
   dotnet run src/FellowshipAnalyzer.Tools/fetch-report.cs 6fgrXtW1b2aTZcD3 347 4
   ```
2. Output lands in the gitignored `raw-reports/` folder as `{code}-f{dungeon}-s{source}.json`. Analyze it with the `analyze-event-schema` or `analyze-log-resources` skill.

### refresh-schema

Updates `src/FellowshipAnalyzer/FellowshipAnalyzer.Api.GraphQL/schema.graphql` from the live FellowshipLogs API introspection. Run whenever the GraphQL API schema changes, before regenerating StrawberryShake client code.

1. ```
   dotnet run src/FellowshipAnalyzer.Tools/refresh-schema.cs
   ```
2. Rebuild the solution to trigger StrawberryShake's source generator:
   ```
   dotnet build FellowshipAnalyzer.slnx
   ```
3. If the schema adds new fields used in queries, update the relevant `.graphql` files in `src/FellowshipAnalyzer/FellowshipAnalyzer.Api.GraphQL/GraphQL/` and add corresponding mapper logic in `FellowshipAnalyzer.Api.Core/GraphQLMapper.cs`.

### event-schema

1. Identify the log JSON to analyze; real logs live in the gitignored `raw-reports/` folder.
2. Run from the repo root:
   ```
   dotnet run src/FellowshipAnalyzer.Tools/event-schema.cs raw-reports/RaMDvgzWXBCnF4QT-f16-s25.json
   ```
3. To compare the output against C# event classes and find deserialization mismatches, load the **analyze-event-schema** skill.

### resource-analysis

1. Run from the repo root:
   ```
   dotnet run src/FellowshipAnalyzer.Tools/resource-analysis.cs raw-reports/RaMDvgzWXBCnF4QT-f16-s25.json
   ```
2. Review the Markdown output: the top table summarizes each unique resource type; each type section shows amount/max ranges, event and ability pairings, and change patterns. If no matching resource objects are found, the tool reports that explicitly.

## Notes

- All tools are file-based apps (no `.csproj`). Dependencies and build settings are declared via `#:package`, `#:project` and `#:property` directives at the top of each `.cs` file.
- A tool with a `#:project` reference (`rebuild-spelldb.cs`, `emit-palette.cs`) must be run with `dotnet run --no-cache`, or a stale cached build of the referenced project is used and the output is silently wrong.
- Run from the repository root (`G:\source\FellowshipAnalyzer`) unless the usage above says otherwise, so relative paths resolve correctly.
- The `update-spells` tool matches spells by numeric ID and only updates existing entries; it never adds new ones.
