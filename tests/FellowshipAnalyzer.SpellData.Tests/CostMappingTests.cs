using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Model;
using FellowshipAnalyzer.SpellData.Sources;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class CostMappingTests
{
    private static ExportAbility Ability(double? cost, string? resource, bool fraction = false) =>
        new(1, "Test", null, null, cost, resource, fraction, null, null, null, null, null, [], []);

    [Fact]
    public void WinterOrbCost_MapsToTertiary() =>
        Costs.Map(Ability(2, "Winter Orbs"), [], "rime", "GlacialBlast")[ResourceTypes.Tertiary].ShouldBe(2);

    [Fact]
    public void SpiritCost_MapsToSpirit() =>
        Costs.Map(Ability(100, "Spirit"), [], "elarion", "EventHorizon")[ResourceTypes.Spirit].ShouldBe(100);

    [Fact]
    public void LowercaseResourceName_StillResolves() =>
        Costs.Map(Ability(30, "mana"), [], "xavian", "SunStrike")[ResourceTypes.Mana].ShouldBe(30);

    [Fact]
    public void SingularRadiantRune_ResolvesToPrimary() =>
        Costs.Map(Ability(1, "Radiant Rune"), [], "vigour", "Soulbrand")[ResourceTypes.Primary].ShouldBe(1);

    [Fact]
    public void CostWithoutAResource_MapsToNothing() =>
        Costs.Map(Ability(30, null), [], "meiko", "EarthFist").ShouldBeEmpty();

    [Fact]
    public void FractionalCost_MapsToNothing() =>
        Costs.Map(Ability(0.25, "Fury", fraction: true), [], "tariq", "SkullCrusher").ShouldBeEmpty();

    [Fact]
    public void UnresolvableResourceName_RaisesAGap()
    {
        var gaps = new List<Gap>();
        Costs.Map(Ability(1, "Burning Ember"), gaps, "ardeos", "Detonate").ShouldBeEmpty();
        gaps.ShouldContain(g => g.Scope == "ardeos" && g.Member == "Detonate" && g.Kind == GapKind.UnknownResource);
    }
}
