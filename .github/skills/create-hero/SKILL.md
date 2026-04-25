---
name: create-hero
description: "Scaffold a complete new hero analysis module for FellowshipAnalyzer. Use when: adding support for a new hero/class, creating a new hero project from scratch. Creates the project, parser, modules folder, guide shell, and DI wiring."
---

# Create Hero

Scaffold a complete hero analysis project using the current source-generated parser model. Use `src/FellowshipAnalyzer.Heroes.Rime/` as the reference implementation.

## Procedure

### 1. Create The Project

Create a Razor Class Library at `src/FellowshipAnalyzer.Heroes.{Hero}/`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="AspNetCore.SassCompiler" Version="1.77.8" />
    <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="10.0.7" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.7" />
    <SupportedPlatform Include="browser" />
    <ProjectReference Include="..\FellowshipAnalyzer.Core\FellowshipAnalyzer.Core.csproj" />
    <ProjectReference Include="..\FellowshipAnalyzer.Components\FellowshipAnalyzer.Components.csproj" />
    <ProjectReference Include="..\FellowshipAnalyzer.Generators\FellowshipAnalyzer.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
```

Add the project to `FellowshipAnalyzer.slnx` and reference it from the Client project.

### 2. Create Folder Structure

```text
src/FellowshipAnalyzer.Heroes.{Hero}/
  {Hero}CombatLogParser.cs
  {Hero}Guide.razor
  _Imports.razor
  sasscompiler.json
  Modules/
    Abilities.cs
  Guides/
  Statistics/
  Normalizers/
```

Use `Modules/` for analyzers, resource trackers, auras, and the hero `Abilities` module.

### 3. Define Spell Identity Data

Shared spell identity data lives in Core. Add a hero spell registry under `src/FellowshipAnalyzer.Core/Common/Spells/{Hero}/Spells.cs`:

```csharp
namespace FellowshipAnalyzer.Core.Common.Spells.{Hero};

public class Spells : ISpellRegistry
{
    public static Spell BasicAttack { get; } = new(1001, "Basic Attack", "basic.jpg");
    public static Spell SpecialAbility { get; } = new(1002, "Special Ability", "special.jpg");
}
```

Use the `run-tool` skill for `update-spells` when refreshing names/icons from JSON. Gameplay metadata such as cooldowns, GCD, categories, and costs belongs in the hero `Abilities` module or spell init properties, not in analyzer logic.

### 4. Create The CombatLogParser

`{Hero}CombatLogParser.cs`:

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.{Hero}.Guides;
using FellowshipAnalyzer.Heroes.{Hero}.Modules;

namespace FellowshipAnalyzer.Heroes.{Hero}.Analysis;

[HeroAnalyzer("{hero-id}")]
[AddModule<Modules.Abilities>]
public sealed partial class {Hero}CombatLogParser : CombatLogParser
{
    public override string HeroId => "{hero-id}";
    public override Type? GuideComponent => typeof({Hero}Guide);
}
```

Do not write a constructor or override `Analyze`; the source generator and base `CombatLogParser` provide the current pipeline.

### 5. Create The Abilities Module

`Modules/Abilities.cs`:

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.{Hero};

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.{Hero}.Modules;

public sealed class Abilities : CoreAbilities
{
    public override IEnumerable<SpellbookAbility> Spellbook() =>
    [
        new()
        {
            PrimarySpell = Spells.BasicAttack,
            Category = SpellCategory.Rotational,
            Gcd = StandardGcd,
        },
        new()
        {
            PrimarySpell = Spells.SpecialAbility,
            Category = SpellCategory.Cooldowns,
            Cooldown = 60,
            Gcd = StandardGcd,
        },
    ];
}
```

The source generator registers this as a scoped service and aliases it to the core `Abilities` type so core normalizers can inject it.

### 6. Create Imports

`_Imports.razor`:

```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
@using FellowshipAnalyzer.Components
@using FellowshipAnalyzer.Core.Analysis
@using FellowshipAnalyzer.Heroes.{Hero}.Analysis
@using FellowshipAnalyzer.Heroes.{Hero}.Guides
@using FellowshipAnalyzer.Heroes.{Hero}.Modules
@using FellowshipAnalyzer.Heroes.{Hero}.Statistics
```

### 7. Create The Root Guide

`{Hero}Guide.razor`:

```razor
@namespace FellowshipAnalyzer.Heroes.{Hero}.Guides
@using FellowshipAnalyzer.Heroes.{Hero}.Analysis
@inject {Hero}CombatLogParser Parser

@if (Parser.SomeAnalyzer is not null)
{
    <SomeGuide />
}
```

The root guide starts empty until feature guide components exist. Add feature guides with the `create-guide` skill.

### 8. Configure SCSS

Copy the `sasscompiler.json` shape from Rime and use `.razor.scss` files for any component styles. Load the `style-guide` skill before creating or changing styles.

### 9. Register DI

In the Client startup, register core analysis services before hero-specific analysis:

```csharp
builder.Services.AddCoreAnalysisServices();
builder.Services.AddCoreAnalysis();
builder.Services.Add{Hero}Analysis();
```

`Add{Hero}Analysis()` is generated from `[HeroAnalyzer]`, `[AddModule]`, and `[AddNormalizer]` attributes.

## Adding Hero Features

Once the scaffold is in place, use the individual skills:

- `create-analyzer` for event-driven modules.
- `create-resource-tracker` for resource tracking.
- `create-guide` for guide sections.
- `create-statistics` for statistics cards.
- `create-normalizer` for event preprocessing.
- `style-guide` for SCSS.

## Checklist

- [ ] Project references Core, Components, and Generators.
- [ ] Project is added to the solution and Client project references.
- [ ] Folder structure uses `Modules/`, `Guides/`, `Statistics/`, and `Normalizers/`.
- [ ] Spell identity data is defined under `Core/Common/Spells/{Hero}/`.
- [ ] `{Hero}CombatLogParser` is `partial`, has `[HeroAnalyzer]`, and overrides `HeroId` and `GuideComponent`.
- [ ] `Modules/Abilities.cs` exists and is registered with `[AddModule<Modules.Abilities>]`.
- [ ] `_Imports.razor` includes hero Analysis, Guides, Modules, and Statistics namespaces.
- [ ] `{Hero}Guide.razor` exists.
- [ ] `Add{Hero}Analysis()` is called in Client startup.