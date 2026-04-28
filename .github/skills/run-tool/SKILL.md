---
name: run-tool
description: "Run a .NET file-based tool from src/FellowshipAnalyzer.Tools/. Use when: updating spell lists from JSON, fetching abilities from the FellowshipLogs API, or executing any file-based dotnet tool script."
argument-hint: "Name of the tool to run (e.g. update-spells, fetch-abilities)"
---

# Run Tool

Execute a .NET 10 file-based app from `src/FellowshipAnalyzer.Tools/`.

## Available Tools

| Script | Purpose | Usage |
|--------|---------|-------|
| `fetch-abilities.cs` | Fetches all abilities from the FellowshipLogs API and writes `abilities.json` at the repo root. Uses the cached file if it exists; pass `--refresh` to re-fetch. | `dotnet run src/FellowshipAnalyzer.Tools/fetch-abilities.cs [--refresh]` |
| `event-schema.cs` | Scans a log JSON events array and prints every unique event type with all properties each type can have, their frequency, and JSON value kinds. Use for C# model comparison and deserialization audits. | `dotnet run src/FellowshipAnalyzer.Tools/event-schema.cs <log-json>` |
| `resource-analysis.cs` | Analyzes `sourceResources.resources` across a log JSON and prints a Markdown summary of unique resource types, change patterns, and common event/ability pairings | `dotnet run src/FellowshipAnalyzer.Tools/resource-analysis.cs <log-json>` |
| `update-spells.cs` | Reads ability data from a JSON file and updates a hero spell registry `.cs` file. JSON is authoritative for name and icon. Supports both `abilities.json` (API format) and combat-log export format. | `dotnet run src/FellowshipAnalyzer.Tools/update-spells.cs <abilities-json> <target-cs>` |

## Procedure

### update-spells

The standard input is `abilities.json` at the repo root — a cached copy of all game abilities from the FellowshipLogs API. All entries have fully populated `Id`, `Name`, and `Icon` fields.

1. Run from the repo root:
   ```
   dotnet run src/FellowshipAnalyzer.Tools/update-spells.cs abilities.json src/Heroes/<Hero>/Spells.cs
   ```
2. Review the output:
   - **Updated** lines show what changed (name/icon diffs).
   - **Not found** lines list spell/effect IDs in the `.cs` file that have no match in the JSON — verify the ID is correct.
   - Unmatched abilities (in JSON but not in `.cs`) are also listed — these may need new entries added manually.

The tool also accepts combat-log export JSON (objects with `guid`, `name`, `abilityIcon` properties) as an alternative input.

**Spell vs Effect matching**: The tool detects `Effect` vs `Spell` from the C# declaration on each line. For `abilities.json`, the same ability entry is checked against both lookups; the C# file determines which applies. For combat-log format, `guid >= 1_000_000` identifies effects (stored by `guid - 1_000_000` to match the base ID in the constructor).

### fetch-abilities

Requires `FellowshipLogs:ClientId` and `FellowshipLogs:ClientSecret` in user secrets.

1. Run from the repo root:
   ```
   dotnet run src/FellowshipAnalyzer.Tools/fetch-abilities.cs
   ```
2. If `abilities.json` already exists, the tool exits immediately with a message — no network call is made.
3. To force a re-fetch (e.g. after a game patch adds new abilities):
   ```
   dotnet run src/FellowshipAnalyzer.Tools/fetch-abilities.cs --refresh
   ```
4. Output is written to `abilities.json` at the repo root.

### event-schema

1. Identify the log JSON file to analyze. Works with `raw-report.json` or any JSON containing an events array.
2. Run from the repo root:
   ```
   dotnet run src/FellowshipAnalyzer.Tools/event-schema.cs <log-json>
   ```
3. Review the Markdown output:
   - The summary table lists every unique event type with count and property count.
   - Each type section lists all observed properties with frequency, JSON type(s), and child property names for nested objects.
4. To compare the output against C# event classes and find deserialization mismatches, load the **analyze-event-schema** skill.

### resource-analysis

1. Identify the log JSON file to analyze. This works with `raw-report.json`, a top-level event array, or similar JSON that contains event objects with `sourceResources.resources`.
2. Run from the repo root:
   ```
   dotnet run src/FellowshipAnalyzer.Tools/resource-analysis.cs <log-json>
   ```
3. Review the Markdown output:
   - The top table summarizes each unique resource type.
   - Each type section shows amount/max ranges, event and ability pairings, and change patterns.
   - If no matching resource objects are found, the tool reports that explicitly.

## Notes

- All tools are file-based apps (no `.csproj`). Dependencies are declared via `#:package` directives at the top of each `.cs` file.
- Run from the repository root (`G:\source\FellowshipAnalyzer`) so relative paths resolve correctly.
- The `update-spells` tool matches spells by numeric ID. It does not add new entries — only updates existing ones.
- `abilities.json` data is always fully populated (`Id`, `Name`, `Icon` are never null or missing).
