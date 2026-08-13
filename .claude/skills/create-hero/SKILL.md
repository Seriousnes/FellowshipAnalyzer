---
name: create-hero
description: "Scaffold a complete new hero analysis module for FellowshipAnalyzer. Use when: adding support for a new hero/class, creating a new hero project from scratch. Creates the project, parser, modules folder, guide shell, and DI wiring."
---

# Create Hero

Scaffold a complete hero analysis project using the current source-generated parser model. Use `src/Heroes/FellowshipAnalyzer.Heroes.Rime/` for the cleanest minimal shape and `src/Heroes/FellowshipAnalyzer.Heroes.Gunde/` for the most recently scaffolded hero (it shows the current file set, including `GundeCombatLogParser.Config.cs` and `Changelog.razor`). `src/Heroes/FellowshipAnalyzer.Heroes.Ardeos/` is the most built-out hero.

## Procedure

### 1. Create The Project

Create a Razor Class Library at `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="AspNetCore.SassCompiler" />
    <PackageReference Include="Microsoft.AspNetCore.Components.Web" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <SupportedPlatform Include="browser" />
    <ProjectReference Include="..\..\FellowshipAnalyzer.Core\FellowshipAnalyzer.Core.csproj" />
    <ProjectReference Include="..\..\FellowshipAnalyzer.Generators\FellowshipAnalyzer.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
```

Do not add a `Version` attribute to any `PackageReference`; central package management resolves versions from `src/Heroes/Directory.Packages.props`. Hero projects sit two levels below `src/`, so project references use the `..\..\` prefix.

Add the project to `FellowshipAnalyzer.slnx` and as a `ProjectReference` in `src/FellowshipAnalyzer/FellowshipAnalyzer/FellowshipAnalyzer.csproj`.

### 2. Create Folder Structure

```text
src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/
  {Hero}CombatLogParser.cs
  {Hero}CombatLogParser.Config.cs
  {Hero}Guide.razor
  Changelog.razor
  _Imports.razor
  sasscompiler.json
  Modules/
    Abilities.cs
  Guides/
  Statistics/
  Normalizers/
```

Use `Modules/` for analyzers, resource trackers, auras, and the hero `Abilities` module.

### 3. Spell Identity Data

Hero spell registries are generated. Add the hero's scope to `data/spelldb.json` by running `dotnet run --no-cache src/FellowshipAnalyzer.Tools/rebuild-spelldb.cs`, which reconciles the source data through the `FellowshipAnalyzer.SpellData` merge engine; hand corrections go in `data/overrides.json`, never in a generated `Spells.cs`. `ConsolidatedSpellRegistryGenerator` then emits `FellowshipAnalyzer.Core.Common.Spells.{Hero}.Spells : ISpellRegistry` with one `public static Spell` member per entry.

If the generated partial needs a hand-written companion, add an empty `public partial class Spells : ISpellRegistry { }` stub at `src/FellowshipAnalyzer.Core/Common/Spells/{Hero}/Spells.cs`, as Gunde does.

Talents stay hand-written at `src/FellowshipAnalyzer.Core/Common/Spells/{Hero}/Talents.cs`; `TalentIdConstantsGenerator` emits the `{Hero}Talents` constants from them. Costs are `ResourceTypes`-keyed on `Spell.Costs` and read with `Cost(ResourceTypes)`.

### 4. Register The Hero In Core

In `src/FellowshipAnalyzer.Core/Analysis/Heroes.cs`:

1. Add `{Hero}` to the `HeroName` enum (alphabetical).
2. Add a `Hero.{Hero}` static field with its `HeroRole` and include it in `Hero.All`.
3. Add the `HeroNameExtensions.ToHeroId` arm, the `Hero.IconUrl` arm (a fellows.gg portrait URL), and the `Hero.Color` arm returning `FaVar.Hero{Name}`.

The colour token: add `--fa-hero-{name}` to `FaPalette`, give it a value in all three `FaTheme` themes, add the `FaVar.Hero{Name}` member, then regenerate the stylesheet from `src/FellowshipAnalyzer.Tools`:

```powershell
dotnet run --no-cache emit-palette.cs "../FellowshipAnalyzer.Core/Styles/_palette.scss"
```

`PaletteScssDriftTests.Committed_Palette_Matches_The_Theme` fails the build if the committed `_palette.scss` does not match the C# theme.

### 5. Create The CombatLogParser

`{Hero}CombatLogParser.cs`:

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.{Hero}.Modules;

namespace FellowshipAnalyzer.Heroes.{Hero}.Analysis;

[HeroAnalyzer(HeroName.{Hero})]
[AddModule<Modules.Abilities>]
public sealed partial class {Hero}CombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof({Hero}Guide);
}
```

Do not write a constructor or override `Analyze`; the source generator and base `CombatLogParser` provide the pipeline.

`{Hero}CombatLogParser.Config.cs` holds the `HeroConfig` (`Support`, `Maintainers`, `SeasonLabel`, `Changelog`, `ExampleReport`); copy the shape from Gunde. Add a `Changelog.razor` beside it, and a matching `tests/FellowshipAnalyzer.Heroes.{Hero}.Tests` project.

### 6. Create The Abilities Module

`Modules/Abilities.cs` follows the current spellbook shape; copy a live entry from `src/Heroes/FellowshipAnalyzer.Heroes.Ardeos/Modules/Abilities.cs` or Rime's. Scalars such as cooldown and charges flow from the generated registry `Spell` via `PrimarySpell`; the spellbook adds analysis-facing settings (`SpellCategory`, `AbilityCategory`, GCD, `CooldownReducedByHaste`).

The source generator emits the factory that constructs this module per analysis and resolves it polymorphically for the core `Abilities` type, so core normalizers can take it. Modules are never registered in the DI container.

### 7. Create Imports

`_Imports.razor`, matching Rime's live set:

```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
@using FellowshipAnalyzer.Core.Analysis
@using FellowshipAnalyzer.Core.Contracts.Design
@using FellowshipAnalyzer.Core.Game
@using FellowshipAnalyzer.Core.UI
@using FellowshipAnalyzer.Core.UI.Components
@using FellowshipAnalyzer.Core.UI.Diagnostics
@using FellowshipAnalyzer.Core.UI.Guides
@using FellowshipAnalyzer.Core.UI.Timeline
@using FellowshipAnalyzer.Heroes.{Hero}.Analysis
@using FellowshipAnalyzer.Heroes.{Hero}.Guides
@using FellowshipAnalyzer.Heroes.{Hero}.Modules
@using FellowshipAnalyzer.Heroes.{Hero}.Statistics
@using Spells = FellowshipAnalyzer.Core.Common.Spells.{Hero}.Spells
```

### 8. Create The Root Guide

`{Hero}Guide.razor` at the project root, with no `@namespace` directive (live root guides keep the project root namespace):

```razor
@inherits ReportComponent<{Hero}CombatLogParser>

@if (Parser.{Name}Analyzers.Count > 0)
{
    <{Name}Guide />
}
@if (Parser.{Name}Tracker is not null)
{
    <{Tracker}Guide />
}
```

Gate pull analyzers on a non-empty surface stream and dungeon-lifetime modules on a null check, exactly as `RimeGuide.razor` does for both. The root guide renders only analysis-driven guide components; no static rotation, overview, or how-to-play prose belongs in it. Add feature guides with the `create-guide` skill.

### 9. Configure SCSS

`sasscompiler.json`:

```json
{
  "SourceFolder": ".",
  "TargetFolder": ".",
  "GenerateScopedCss": true,
  "ScopedCssFolders": [ "." ],
  "IncludePaths": [ ".", "../../FellowshipAnalyzer.Core/Styles" ]
}
```

The `../../FellowshipAnalyzer.Core/Styles` include path is what lets a hero `.razor.scss` write `@use 'tokens' as t;` and `@use 'mixins' as mx;` with no path prefix. Load the `style-guide` skill before creating or changing styles.

### 10. Wiring

Adding the hero project as a `ProjectReference` in `src/FellowshipAnalyzer/FellowshipAnalyzer/FellowshipAnalyzer.csproj` is the whole wiring step. `Program.cs` already calls `AddCoreAnalysisServices()`, `AddCoreAnalysis()` and `AddFellowshipHeroAnalysis()`; the last is generated by `HeroManifestGenerator` from the `[GenerateHeroManifest]` marker and picks up the new hero's `[HeroAnalyzer]` parser automatically. Do not edit `Program.cs`.

## Adding Hero Features

Once the scaffold is in place, use the individual skills:

- `create-analyzer` for event-driven modules.
- `create-resource-tracker` for resource tracking.
- `create-guide` for guide sections.
- `create-statistics` for statistics cards.
- `create-normalizer` for event preprocessing.
- `style-guide` for SCSS.

## Checklist

- [ ] Project references Core and Generators (versionless PackageReferences, `..\..\` paths).
- [ ] Hero project is referenced from `FellowshipAnalyzer.csproj` and added to `FellowshipAnalyzer.slnx`.
- [ ] Folder structure uses `Modules/`, `Guides/`, `Statistics/`, and `Normalizers/`.
- [ ] Hero scope exists in `data/spelldb.json` (via rebuild-spelldb + overrides.json); `Talents.cs` is hand-written.
- [ ] `Heroes.cs` has the `HeroName` member, `Hero.{Hero}` field in `Hero.All`, and `ToHeroId`/`IconUrl`/`Color` arms.
- [ ] `--fa-hero-{name}` exists in `FaPalette`, every `FaTheme` theme, and `FaVar`; `_palette.scss` regenerated.
- [ ] `{Hero}CombatLogParser` is `partial`, has `[HeroAnalyzer(HeroName.{Hero})]`, and overrides `GuideComponent`.
- [ ] `{Hero}CombatLogParser.Config.cs` and `Changelog.razor` exist.
- [ ] `Modules/Abilities.cs` exists and is registered with `[AddModule<Modules.Abilities>]`.
- [ ] `_Imports.razor` matches the live set including the `Spells` alias.
- [ ] `{Hero}Guide.razor` exists at the project root with analysis-gated children only.
- [ ] `tests/FellowshipAnalyzer.Heroes.{Hero}.Tests` exists.
