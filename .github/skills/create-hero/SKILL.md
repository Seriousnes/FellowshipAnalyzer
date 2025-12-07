---
name: create-hero
description: "Scaffold a complete new hero analysis module for FellowshipAnalyzer. Use when: adding support for a new hero/class, creating a new hero project from scratch. Creates the project, parser, definition, folder structure, and DI wiring."
---

# Create Hero

Scaffold a complete new hero analysis module. This creates the Razor Class Library project, CombatLogParser, analysis definition, folder structure, spells, and DI registration.

## Procedure

### 1. Create the project

Create a new Razor Class Library at `src/FellowshipAnalyzer.Heroes.{Hero}/`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.Web" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\FellowshipAnalyzer.Core\FellowshipAnalyzer.Core.csproj" />
    <ProjectReference Include="..\FellowshipAnalyzer.Components\FellowshipAnalyzer.Components.csproj" />
    <ProjectReference Include="..\FellowshipAnalyzer.Generators\FellowshipAnalyzer.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  </ItemGroup>

</Project>
```

Add the project reference to the solution (`FellowshipAnalyzer.slnx`) and to the Client project.

### 2. Create folder structure

```
src/FellowshipAnalyzer.Heroes.{Hero}/
├── Analysis/
│   ├── {Hero}CombatLogParser.cs
│   └── {Hero}AnalysisDefinition.cs
├── Analyzers/
│   └── Abilities.cs
├── Combat/
│   └── {Hero}Spells.cs
├── Guides/
│   └── {Hero}Guide.razor
├── Statistics/
│   └── (empty initially)
└── Normalizers/
    └── (empty initially)
```

### 3. Define spells

`Combat/{Hero}Spells.cs`:

```csharp
using FellowshipAnalyzer.Core.Models;

namespace FellowshipAnalyzer.Heroes.{Hero}.Combat;

public static class {Hero}Spells
{
    public static Spell BasicAttack { get; } = new(1001, "Basic Attack");
    public static Spell SpecialAbility { get; } = new(1002, "Special Ability");
    // Add spells as needed from combat log data
}
```

### 4. Create analysis definition

`Analysis/{Hero}AnalysisDefinition.cs`:

```csharp
using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.{Hero}.Analysis;

public static class {Hero}AnalysisDefinition
{
    public static HeroAnalysisDefinition Instance { get; } = new(
        HeroId: "{hero-id}",
        Abilities: new Dictionary<int, AbilityDefinition>
        {
            [{Hero}Spells.BasicAttack.Id] = new(/* ... */),
            [{Hero}Spells.SpecialAbility.Id] = new(/* ... */),
        });
}
```

### 5. Create the CombatLogParser

`Analysis/{Hero}CombatLogParser.cs`:

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.{Hero}.Analyzers;
using FellowshipAnalyzer.Heroes.{Hero}.Guides;

using Microsoft.Extensions.DependencyInjection;

namespace FellowshipAnalyzer.Heroes.{Hero}.Analysis;

[AddModule<Abilities>]
public sealed partial class {Hero}CombatLogParser : CombatLogParser, IHeroAnalyzer
{
    public string HeroId => "{hero-id}";
    public new HeroAnalysisDefinition Definition => {Hero}AnalysisDefinition.Instance;
    public override Type GuideComponentType => typeof({Hero}Guide);

    public HeroAnalysisResult Analyze(IReadOnlyList<Event> events, int playerId)
    {
        Events = events;
        PlayerId = playerId;
        base.Definition = {Hero}AnalysisDefinition.Instance;

        Module[] modules = [.. Modules.Select(t => (Module)provider.GetRequiredService(t))];
        Run(modules);

        return new HeroAnalysisResult
        {
            ScoreCards = [],
            Modules = modules,
        };
    }
}
```

### 6. Create the Abilities module

`Analyzers/Abilities.cs`:

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.{Hero}.Combat;

namespace FellowshipAnalyzer.Heroes.{Hero}.Analyzers;

public sealed class Abilities : Core.Analysis.Abilities
{
    protected override Dictionary<int, AbilityDefinition> Spellbook() =>
        {Hero}AnalysisDefinition.Instance.Abilities.ToDictionary();
}
```

### 7. Create the main Guide page

`Guides/{Hero}Guide.razor`:

```razor
@using FellowshipAnalyzer.Heroes.{Hero}.Analysis
@inject {Hero}CombatLogParser Parser

<h2>{Hero} Guide</h2>

<!-- Add guide sections here as analyzers are created -->
```

### 8. Register DI

The source generator creates `Add{Hero}Analysis()` from the `[AddModule]` and `[AddNormalizer]` attributes. In the Client `Program.cs`:

```csharp
builder.Services.Add{Hero}Analysis();
```

## Adding Analyzers to the Hero

Once the scaffold is in place, use the individual skills:
- **create-analyzer** — Add a new event-driven analyzer
- **create-resource-tracker** — Add resource tracking
- **create-guide** — Add a guide section for an analyzer
- **create-statistics** — Add a statistics card for an analyzer
- **create-normalizer** — Add event pre-processing

## Checklist

- [ ] Project `.csproj` references Core, Components, and Generators
- [ ] Added to solution file and Client project references
- [ ] Folder structure: `Analysis/`, `Analyzers/`, `Combat/`, `Guides/`, `Statistics/`, `Normalizers/`
- [ ] Spells defined in `Combat/{Hero}Spells.cs`
- [ ] `{Hero}AnalysisDefinition` with `HeroAnalysisDefinition Instance`
- [ ] `{Hero}CombatLogParser` is `partial`, implements `IHeroAnalyzer`
- [ ] `Abilities` module created and registered with `[AddModule]`
- [ ] `{Hero}Guide.razor` exists
- [ ] `Add{Hero}Analysis()` called in Client `Program.cs`
