---
name: analyze-log-resources
description: "Analyze sourceResources.resources in a Fellowship log JSON. Use when: inspecting raw-report.json files, identifying unique resource types, understanding how each resource changes, and summarizing which event types and ability names each resource typically pairs with."
argument-hint: "Path to the log JSON file to analyze"
---

# Analyze Log Resources

Run the reusable resource analysis tool in `src/FellowshipAnalyzer.Tools/` and return the Markdown report it prints.

## Use When

- A user wants the unique `sourceResources.resources.type` values in a log.
- A user wants to know how each resource changes over time.
- A user wants the most common `event.type` and `ability.name` pairings for each resource type.
- A user is working from `raw-report.json` or any JSON file that contains event objects with `sourceResources.resources`.

## Command

Run from the repository root:

```powershell
dotnet run src/FellowshipAnalyzer.Tools/resource-analysis.cs <log-json>
```

Example:

```powershell
dotnet run src/FellowshipAnalyzer.Tools/resource-analysis.cs raw-reports/RaMDvgzWXBCnF4QT-f16-s25.json
```

Report JSON lives in the gitignored `raw-reports/` folder at the repo root, named `{code}-f{dungeonId}-s{sourceId}.json`; fetch a new one with `fetch-report.cs` (run-tool skill).

## Input Shapes Supported

- A raw report JSON with events at `data.reportData.report.events.data`
- A top-level array of event objects
- A nested JSON document where one array contains event objects and at least some include `sourceResources.resources`

## Expected Output

The tool prints Markdown with:

1. A summary table of unique resource types.
2. For each type, a breakdown of:
   - occurrence count
   - amount and max ranges
   - common event types
   - common abilities
   - common `event.type + ability.name` pairs
   - increase, decrease, and unchanged counts
   - common amount values and deltas
   - top increase and decrease drivers
   - common non-zero transitions

## Response Guidance

- Prefer returning the tool's Markdown directly when the user asked for analysis.
- If the output is long, keep the top table and the most relevant type sections, then summarize the rest.
- Map an observed numeric `type` against `ResourceTypes` in `src/FellowshipAnalyzer.Core/Game/ResourceTypes.cs` and its `[ResourceName]` attributes before inferring meaning; that is the source of truth for which slot a hero's resource occupies.
- The tool reports raw JSON values. `ResourceNormalizer` divides amount, max and cost by 100 before dispatch, so a raw `500` is `5` to an analyzer, and a raw `max: -100` is the no-maximum sentinel that becomes -1.
- If the user asks what a type likely means, make it clear when the conclusion is inferred from patterns rather than confirmed by source code.
- If the tool reports no `sourceResources.resources`, tell the user the file does not include that structure instead of guessing.