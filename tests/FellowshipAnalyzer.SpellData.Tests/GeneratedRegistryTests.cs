using GundeSpells = FellowshipAnalyzer.Core.Common.Spells.Gunde.Spells;

using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class GeneratedRegistryTests
{
    [Fact]
    public void GundeRegistry_ExposesEveryAreaAbilityRadiusInGameUnits()
    {
        GundeSpells.Slaughter.Radius.ShouldBe(700);
        GundeSpells.GrimCarve.Radius.ShouldBe(700);
        GundeSpells.BloodArc.Radius.ShouldBe(700);
        GundeSpells.ReaverEdge.Radius.ShouldBe(700);
        GundeSpells.BloodboundSpirit.Radius.ShouldBe(1000);
    }

    [Fact]
    public void GundeRegistry_KeepsRangeAndRadiusInGameUnits()
    {
        GundeSpells.Slaughter.Range.ShouldBe(700);
        GundeSpells.Slaughter.Radius.ShouldBe(700);
    }

    [Fact]
    public void GundeRegistry_LeavesRadiusUnsetOnAnAbilityWithNoArea() =>
        GundeSpells.Warbound.Radius.ShouldBeNull();

    [Fact]
    public void GundeRegistry_GivesAnAreaCentredOnTheCasterOneRangeAndRadius()
    {
        GundeSpells.Slaughter.Range.ShouldBe(700);
        GundeSpells.BloodArc.Range.ShouldBe(700);
        GundeSpells.ReaverEdge.Range.ShouldBe(700);
        GundeSpells.BloodboundSpirit.Range.ShouldBe(1000);
    }

    [Fact]
    public void GundeRegistry_MeasuresHeartSplitterByRangeAlone()
    {
        GundeSpells.HeartSplitter.Radius.ShouldBeNull();
        GundeSpells.HeartSplitter.Range!.Value.ShouldBe(500);
    }

    [Fact]
    public void GundeRegistry_GivesGrimCarveARangeWellPastItsRadius()
    {
        GundeSpells.GrimCarve.Radius!.Value.ShouldBe(700);
        GundeSpells.GrimCarve.Range!.Value.ShouldBe(3_000);
    }

    [Fact]
    public void GundeRegistry_MeasuresWarboundNeitherWay()
    {
        GundeSpells.Warbound.Range.ShouldBeNull();
        GundeSpells.Warbound.Radius.ShouldBeNull();
    }
}
