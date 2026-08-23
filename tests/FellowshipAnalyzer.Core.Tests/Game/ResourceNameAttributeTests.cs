using System.Reflection;
using FellowshipAnalyzer.Core.Game;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Game;

public class ResourceNameAttributeTests
{
    private static string[] AliasesOf(ResourceTypes value)
    {
        var field = typeof(ResourceTypes).GetField(value.ToString())!;
        return field.GetCustomAttribute<ResourceNameAttribute>()?.Names ?? [];
    }

    [Fact]
    public void Primary_HasEveryPrimaryFlavorName()
    {
        var names = AliasesOf(ResourceTypes.Primary);
        names.ShouldContain("Anima");
        names.ShouldContain("Energy");
        names.ShouldContain("Fury");
        names.ShouldContain("Chrona");
        names.ShouldContain("Cinders");
        names.ShouldContain("Focus");
        names.ShouldContain("Radiant Runes");
    }

    [Fact]
    public void Tertiary_HasWinterOrbsAndBloodFeathers()
    {
        var names = AliasesOf(ResourceTypes.Tertiary);
        names.ShouldContain("Winter Orbs");
        names.ShouldContain("Blood Feathers");
    }

    [Fact]
    public void Secondary_HasComboPointsAndToughness()
    {
        var names = AliasesOf(ResourceTypes.Secondary);
        names.ShouldContain("Combo Points");
        names.ShouldContain("Toughness");
    }
}
