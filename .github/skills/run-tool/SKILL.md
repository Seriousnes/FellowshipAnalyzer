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
| `fetch-abilities.cs` | Fetches all abilities from the FellowshipLogs API and writes `abilities.json` at the repo root | `dotnet run src/FellowshipAnalyzer.Tools/fetch-abilities.cs` |
| `update-spells.cs` | Reads ability data from a JSON file and updates a hero spell registry `.cs` file. JSON is authoritative for name and icon. | `dotnet run src/FellowshipAnalyzer.Tools/update-spells.cs <events-json> <target-cs>` |

## Procedure

### update-spells

1. Identify the **events JSON** file — typically `src/FellowshipAnalyzer.FellowshipLogs/events-with-ability-details.json` or any JSON containing objects with `guid`, `name`, and `abilityIcon` properties.
2. Identify the **target spell registry** — e.g. `src/FellowshipAnalyzer.Core/Common/Spells/Rime.cs`. All spell files follow the `new(id, "Name", "Icon")` pattern.
3. Run from the repo root:
   ```
   dotnet run src/FellowshipAnalyzer.Tools/update-spells.cs <events-json> <target-cs>
   ```
4. Review the output:
   - **Updated** lines show what changed (name/icon diffs).
   - **Not found** lines list abilities in the JSON that have no matching ID in the target file — these may need new entries added manually.

### fetch-abilities

1. Ensure FellowshipLogs credentials are configured in user secrets (`FellowshipLogs:ClientId` and `FellowshipLogs:ClientSecret`).
2. Run from the repo root:
   ```
   dotnet run src/FellowshipAnalyzer.Tools/fetch-abilities.cs
   ```
3. Output is written to `abilities.json` at the repo root.

## Notes

- All tools are file-based apps (no `.csproj`). Dependencies are declared via `#:package` directives at the top of each `.cs` file.
- Run from the repository root (`G:\source\FellowshipAnalyzer`) so relative paths resolve correctly.
- The `update-spells` tool matches spells by numeric ID (`guid` in JSON → first constructor arg in C#). It does not add new entries — only updates existing ones.
