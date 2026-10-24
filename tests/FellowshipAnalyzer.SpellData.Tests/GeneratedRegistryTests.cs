using FellowshipAnalyzer.Core.Common.Spells;

using GundeSpells = FellowshipAnalyzer.Core.Common.Spells.Gunde.Spells;
using GundeLegendaries = FellowshipAnalyzer.Core.Common.Spells.Gunde.Legendaries;
using RimeSpells = FellowshipAnalyzer.Core.Common.Spells.Rime.Spells;

using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class GeneratedRegistryTests
{
    [Fact]
    public void GundeRegistry_ExposesEveryAreaAbilityRadiusInGameUnits()
    {
        GundeSpells.Slaughter.Radius.ShouldBe(700);
        GundeSpells.GrimCarve.Radius.ShouldBe(850);
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
        GundeSpells.GrimCarve.Radius!.Value.ShouldBe(850);
        GundeSpells.GrimCarve.Range!.Value.ShouldBe(3_000);
    }

    [Fact]
    public void GundeRegistry_MeasuresWarboundNeitherWay()
    {
        GundeSpells.Warbound.Range.ShouldBeNull();
        GundeSpells.Warbound.Radius.ShouldBeNull();
    }

    [Fact]
    public void GundeRegistry_GivesHeartSplitterTheChargesBleedingHeartsGrants() =>
        GundeSpells.HeartSplitter.ChargeGrant.ShouldBe(new ChargeGrant
        {
            Source = GrantSource.LegendaryItem,
            Id = GundeLegendaries.BleedingHearts.Id,
            Charges = 2,
            CooldownHaste = CooldownHasteScaling.TimeRate,
        });

    [Fact]
    public void RimeRegistry_ScalesFrostBoltsCastTimeWithHaste() =>
        RimeSpells.FrostBolt.CastTimeScalesWithHaste.ShouldBe(true);
}