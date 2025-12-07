# FellowshipAnalyzer

- This is a tool for parsing and analyzing data related to the online RPG game "Fellowship". It provides detailed analysis on ability usage, combat statistics, and player performance, and suggests areas for improvement.
- The game creates log files, which are uploaded to fellowshiplogs.com, this application calls the Fellowship Logs API GraphQL API to retrieve log data for analysis.
- The application is built using C#14 and NET10. It uses Interactive-Auto for SSR using Blazor Server initially, then transitions to Blazor WebAssembly for client-side interactivity on subsequent interactions.
- Local development is orchestrated using Aspire
- See [./instructions/FellowshipAnalyzer-Architecture-Overview.md](./instructions/FellowshipAnalyzer-Architecture-Overview.md) for architectural details.
- See [../CombatMechanics.md](../CombatMechanics.md) for details on the combat mechanics of the game, which are relevant to the analysis performed by this tool.


## Skills

When creating or modifying analysis modules, use the appropriate skill:

- **create-analyzer** — Adding a new event-driven analyzer (talent, ability, feature)
- **create-guide** — Adding a guide Razor component to the Guide tab
- **create-statistics** — Adding an auto-collected statistics component
- **create-resource-tracker** — Adding resource generation/spending tracking
- **create-normalizer** — Adding event pre-processing (reordering, linking, fabrication)
- **create-hero** — Scaffolding an entire new hero from scratch
- **run-tool** — Running file-based dotnet tools (update-spells, fetch-abilities)

When adding CSS/SCSS to any component, creating a new Razor component with styles, or reviewing existing component styles for consistency, use:

- **style-guide** — SCSS setup, design tokens, class naming, scoped vs global styling, component patterns

## Reference / Inspiration
- The project is loosely based on [WoWAnalyzer](https://github.com/WoWAnalyzer/WoWAnalyzer). Some principles and patterns from that project are followed, but the architecture is designed take advantage of modern C# features where WoWAnalyzer is a long-running project written in TypeScript and React. Always consider using the latest C# features when adapting patterns from WoWAnalyzer, and feel free to deviate from their architecture when it makes sense to do so in the context of C# and Blazor.