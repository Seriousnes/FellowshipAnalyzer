using Xunit;

using RimeFacts = FellowshipAnalyzer.Core.Common.Spells.Rime.AbilityFacts;
using ElarionFacts = FellowshipAnalyzer.Core.Common.Spells.Elarion.AbilityFacts;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Locks in the values the AbilityFactsGenerator extracts from s3/hero_data.json,
/// guarding against normalization regressions (DevName-merge, MaxRange/100,
/// RechargeTime/NumCharges fallbacks, cast/channel/tick) and documenting the
/// data-absent and data-vs-hand cases discovered during wiring.
/// </summary>
public sealed class AbilityFactsTests
{
    [Fact]
    public void Rime_SimpleCooldownAndRange_AreExtracted()
    {
        Assert.Equal(20, RimeFacts.BrainFreeze.Cooldown);
        Assert.Equal(30, RimeFacts.BrainFreeze.Range);
        Assert.Equal(60, RimeFacts.FlightOfTheNavir.Cooldown);
    }

    [Fact]
    public void Rime_ColdSnap_MergesCooldownAndChargesFromDevNameSibling()
    {
        // Cold Snap's live entry has no cooldown; the value lives in the
        // InstantSingleDamage entry sharing DevName GA_Rime_InstantSingleDamage.
        Assert.Equal(12, RimeFacts.ColdSnap.Cooldown);
        Assert.Equal(2, RimeFacts.ColdSnap.Charges);
        Assert.Equal(30, RimeFacts.ColdSnap.Range);
    }

    [Fact]
    public void Rime_IceDash_UsesRechargeTimeAndNumChargesFallbacks()
    {
        Assert.Equal(25, RimeFacts.IceDash.Cooldown);
        Assert.Equal(2, RimeFacts.IceDash.Charges);
    }

    [Fact]
    public void Rime_FreezingTorrent_ExtractsChannelTimings()
    {
        Assert.Equal(15, RimeFacts.FreezingTorrent.Cooldown);
        Assert.Equal(30, RimeFacts.FreezingTorrent.Range);
        Assert.Equal(2.0, RimeFacts.FreezingTorrent.ChannelDuration);
        Assert.Equal(0.4, RimeFacts.FreezingTorrent.ChannelTickInterval);
    }

    [Fact]
    public void Rime_CastDuration_IsExtractedForCastedAbilities()
    {
        Assert.Equal(1.5, RimeFacts.FrostBolt.CastDuration);
        Assert.Equal(2.0, RimeFacts.GlacialBlast.CastDuration);
        Assert.Null(RimeFacts.FrostBolt.Cooldown);
    }

    [Fact]
    public void Elarion_CooldownAndCharges_AreExtracted()
    {
        Assert.Equal(15, ElarionFacts.HighwindArrow.Cooldown);
        Assert.Equal(3, ElarionFacts.HighwindArrow.Charges);
        Assert.Equal(20, ElarionFacts.HeartseekerBarrage.Cooldown);
        Assert.Equal(0.7, ElarionFacts.EventHorizon.CastDuration);
    }

    [Fact]
    public void Elarion_RangeIsAbsentWhereDataLacksMaxRange()
    {
        // Most Elarion Constants entries carry no MaxRange, so range stays
        // hand-authored in the spellbook. LunarlightMark is the exception:
        // its range comes from the InstantMarkTarget DevName sibling.
        Assert.Null(ElarionFacts.HighwindArrow.Range);
        Assert.Null(ElarionFacts.Multishot.Range);
        Assert.Equal(30, ElarionFacts.LunarlightMark.Range);
    }

    [Fact]
    public void Elarion_DataAuthoritativeCooldowns_DifferFromOldHandValues()
    {
        // The S3 export reports 40s where the spellbook previously hand-coded 30s.
        Assert.Equal(40, ElarionFacts.StarfallVolley.Cooldown);
        Assert.Equal(40, ElarionFacts.LunarlightMark.Cooldown);
    }

    [Fact]
    public void Elarion_Roll_HasNoCooldownInData()
    {
        Assert.Null(ElarionFacts.Roll.Cooldown);
        Assert.Equal(3, ElarionFacts.Roll.Charges);
    }
}
