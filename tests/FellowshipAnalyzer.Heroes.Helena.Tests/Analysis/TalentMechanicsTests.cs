using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Helena.Analysis;
using FellowshipAnalyzer.Heroes.Helena.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Helena.Spells;
using Talent = FellowshipAnalyzer.Core.Common.Spells.Talent;
using Talents = FellowshipAnalyzer.Core.Common.Spells.Helena.Talents;

using static FellowshipAnalyzer.Heroes.Helena.Tests.Analysis.HelenaAnalysisFixture;
using FellowshipAnalyzer.Core.Common;

namespace FellowshipAnalyzer.Heroes.Helena.Tests.Analysis;

/// <summary>
/// The Season 3 talents that change what the rest of Helena's analysis measures. Each is gated on the
/// talent being selected, so every case here runs the parser for a build that took it and asserts that
/// a build that did not is left alone.
/// </summary>
public sealed class TalentMechanicsTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void FrontLineDefender_AddsFivePointsToEveryBandButTheDepletedOne()
    {
        ToughnessBands.DamageReduction(ToughnessBand.Level1, true).ShouldBe(0.10, Tolerance);
        ToughnessBands.DamageReduction(ToughnessBand.Level2, true).ShouldBe(0.20, Tolerance);
        ToughnessBands.DamageReduction(ToughnessBand.Level3, true).ShouldBe(0.30, Tolerance);
        ToughnessBands.DamageReduction(ToughnessBand.Level4, true).ShouldBe(0.40, Tolerance);
        ToughnessBands.DamageReduction(ToughnessBand.Depleted, true).ShouldBe(0);
        ToughnessBands.Ceiling(true).ShouldBe(0.40, Tolerance);
        ToughnessBands.Ceiling(false).ShouldBe(0.35, Tolerance);
    }

    [Fact]
    public async Task FrontLineDefender_RaisesTheReductionTheBandAnalyzerReports()
    {
        var withTalent = await BandAnalyzer(Talents.FrontLineDefender);
        var without = await BandAnalyzer();

        withTalent.HasFrontLineDefender.ShouldBeTrue();
        withTalent.AverageDamageReduction.ShouldBe(0.40, 0.001);
        withTalent.DamageReductionCeiling.ShouldBe(0.40, Tolerance);

        without.HasFrontLineDefender.ShouldBeFalse();
        without.AverageDamageReduction.ShouldBe(0.35, 0.001);
        without.DamageReductionCeiling.ShouldBe(0.35, Tolerance);
    }

    [Fact]
    public async Task GreaterShockwave_SizesAmplificationFromToughnessAtCast()
    {
        var parser = await AnalyzeWithTalents(
            [Talents.GreaterShockwave],
            Cast(PullStart + 1_000, Spells.Shockwave, toughness: 1_000),
            Cast(PullStart + 40_000, Spells.Shockwave, toughness: 500));

        var analyzer = parser.GetModule<GreaterShockwaveAnalyzer>().ShouldNotBeNull();

        analyzer.MeasuredCasts.ShouldBe(2);
        analyzer.AverageAmplification.ShouldBe(0.225, 0.001);
        analyzer.AmplificationShare.ShouldBe(0.75, 0.001);
    }

    [Fact]
    public async Task GreaterShockwave_CountsTheReturnedToughnessOvercapped()
    {
        var parser = await AnalyzeWithTalents(
            [Talents.GreaterShockwave],
            Cast(PullStart + 1_000, Spells.Shockwave, toughness: 1_000),
            Cast(PullStart + 40_000, Spells.Shockwave, toughness: 950),
            Cast(PullStart + 80_000, Spells.Shockwave, toughness: 500));

        var analyzer = parser.GetModule<GreaterShockwaveAnalyzer>().ShouldNotBeNull();

        analyzer.ToughnessWastedShare.ShouldBe((1 + 0.5 + 0) / 3, 0.001);
    }

    [Fact]
    public async Task GreaterShockwave_IsAbsentForABuildThatDidNotTakeIt()
    {
        var parser = await Analyze(Cast(PullStart + 1_000, Spells.Shockwave, toughness: 1_000));

        parser.GetModule<GreaterShockwaveAnalyzer>().ShouldBeNull();
    }

    [Fact]
    public async Task GreaterShockwave_ContributesItsCardOnlyOnceItHasACastToReport()
    {
        var withCasts = await AnalyzeWithTalents(
            [Talents.GreaterShockwave],
            Cast(PullStart + 1_000, Spells.Shockwave, toughness: 1_000));
        var withoutCasts = await AnalyzeWithTalents([Talents.GreaterShockwave]);

        withCasts.GetModule<GreaterShockwaveAnalyzer>()
            .ShouldNotBeNull().Statistic.ShouldNotBeNull();
        withoutCasts.GetModule<GreaterShockwaveAnalyzer>()
            .ShouldNotBeNull().Statistic.ShouldBeNull();
    }

    [Fact]
    public async Task SwordAndBoard_ContributesItsCardOnlyOnceItHasAProcToReport()
    {
        var withProc = await AnalyzeWithTalents(
            [Talents.SwordAndBoard],
            ApplyBuff(PullStart + 1_000, Spells.SwordAndBoardBuff));
        var withoutProc = await AnalyzeWithTalents([Talents.SwordAndBoard]);

        withProc.GetModule<SwordAndBoardAnalyzer>()
            .ShouldNotBeNull().Statistic.ShouldNotBeNull();
        withoutProc.GetModule<SwordAndBoardAnalyzer>()
            .ShouldNotBeNull().Statistic.ShouldBeNull();
    }

    [Fact]
    public async Task GreaterShockwave_AddsItsReturnedToughnessToShockwaveOvercapAccounting()
    {
        var withTalent = await AnalyzeWithTalents(
            [Talents.GreaterShockwave],
            Cast(PullStart + 1_000, Spells.Shockwave, toughness: 1_000));
        var without = await Analyze(Cast(PullStart + 1_000, Spells.Shockwave, toughness: 1_000));

        var shockwave = Spells.Shockwave.FSLID;

        withTalent.ToughnessTracker!.NominalGenerationFor(shockwave)
            .ShouldBe(without.ToughnessTracker!.NominalGenerationFor(shockwave)
                + GreaterShockwaveAnalyzer.AdditionalToughnessShare, 0.001);
    }

    [Fact]
    public async Task PunishingStrikes_DoublesTheReductionUnderIt()
    {
        var parser = await AnalyzeWithTalents(
            [Talents.PunishingStrikes],
            ApplyBuff(PullStart + 1_000, Spells.PunishingStrikesBuff),
            Cast(PullStart + 2_000, Spells.MeasuredStrike),
            RemoveBuff(PullStart + 3_000, Spells.PunishingStrikesBuff),
            Cast(PullStart + 4_000, Spells.MeasuredStrike));

        var analyzer = parser.VeteranOfWarAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.PunishingStrikesCasts.ShouldBe(1);
        analyzer.CooldownReduction.Total.ShouldBe((4_000 * 2) + 4_000);
    }

    [Fact]
    public async Task PunishingStrikes_MultipliesWithTheSpiritAbilityRatherThanReplacingIt()
    {
        var parser = await AnalyzeWithTalents(
            [Talents.PunishingStrikes],
            ApplyBuff(PullStart + 1_000, Spells.SiegebreakerBuff),
            ApplyBuff(PullStart + 1_100, Spells.PunishingStrikesBuff),
            Cast(PullStart + 2_000, Spells.MeasuredStrike));

        parser.VeteranOfWarAnalyzers.ShouldHaveSingleItem().Analyzer.CooldownReduction.Total.ShouldBe(4_000 * 4);
    }

    [Fact]
    public async Task PunishingStrikes_StopsDoublingWhenTheLogSpendsItsLastStack()
    {
        var parser = await AnalyzeWithTalents(
            [Talents.PunishingStrikes],
            ApplyBuff(PullStart + 1_000, Spells.PunishingStrikesBuff),
            Cast(PullStart + 2_000, Spells.MeasuredStrike),
            RemoveBuffStack(PullStart + 2_100, Spells.PunishingStrikesBuff, 1),
            Cast(PullStart + 3_000, Spells.MeasuredStrike),
            RemoveBuffStack(PullStart + 3_100, Spells.PunishingStrikesBuff, 0),
            Cast(PullStart + 4_000, Spells.MeasuredStrike));

        var analyzer = parser.VeteranOfWarAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.PunishingStrikesCasts.ShouldBe(2);
        analyzer.CooldownReduction.Total.ShouldBe((4_000 * 2) + (4_000 * 2) + 4_000);
    }

    [Fact]
    public async Task ShieldMastery_SizesItsReductionFromToughnessAtTheMomentOfTheHit()
    {
        var parser = await AnalyzeWithTalents(
            [Talents.ShieldMastery],
            Cast(PullStart + 1_000, Spells.ShieldThrow),
            Cast(PullStart + 1_100, Spells.ShieldsUp),
            DamageTaken(PullStart + 2_000, 100, 100, 0, toughness: 1_000));

        var analyzer = parser.GetModule<ShieldMasteryAnalyzer>().ShouldNotBeNull();

        analyzer.HitsTaken.ShouldBe(1);
        analyzer.CooldownReduction.Total.ShouldBe((int)((21_000 * 0.1) + (30_000 * 0.1)));
        analyzer.CooldownReduction.Effective.ShouldBe(analyzer.CooldownReduction.Total);
    }

    [Fact]
    public async Task ShieldMastery_GeneratesNothingFromAHitTakenAtDepletedToughness()
    {
        var parser = await AnalyzeWithTalents(
            [Talents.ShieldMastery],
            Cast(PullStart + 1_000, Spells.ShieldThrow),
            DamageTaken(PullStart + 2_000, 100, 100, 0, toughness: 0));

        var analyzer = parser.GetModule<ShieldMasteryAnalyzer>().ShouldNotBeNull();

        analyzer.HitsTaken.ShouldBe(1);
        analyzer.CooldownReduction.Total.ShouldBe(0);
    }

    [Fact]
    public async Task ShieldMastery_GeneratesNothingFromAHitWithNoToughness()
    {
        var parser = await AnalyzeWithTalents(
            [Talents.ShieldMastery],
            Cast(PullStart + 1_000, Spells.ShieldThrow),
            DamageTaken(PullStart + 2_000, 100, 100, 0));

        var analyzer = parser.GetModule<ShieldMasteryAnalyzer>().ShouldNotBeNull();

        analyzer.HitsTaken.ShouldBe(0);
        analyzer.HitsWithoutToughness.ShouldBe(1);
        analyzer.CooldownReduction.Total.ShouldBe(0);
    }

    [Fact]
    public async Task SwordAndBoard_RefundsTheChargeTheFreeCastSpent()
    {
        var parser = await AnalyzeWithTalents(
            [Talents.SwordAndBoard],
            Cast(PullStart + 1_000, Spells.ShieldSlam),
            ApplyBuff(PullStart + 1_500, Spells.SwordAndBoardBuff),
            Cast(PullStart + 2_000, Spells.ShieldSlam),
            RemoveBuff(PullStart + 2_100, Spells.SwordAndBoardBuff));

        var analyzer = parser.GetModule<SwordAndBoardAnalyzer>().ShouldNotBeNull();

        analyzer.Procs.ShouldBe(1);
        analyzer.FreeCasts.ShouldBe(1);
        analyzer.ProcsExpired.ShouldBe(0);

        var refund = ShieldSlamUpdates(parser).Last(update => update.Timestamp == PullStart + 2_000);

        refund.UpdateType.ShouldBe(UpdateSpellUsableType.RestoreCharge);
        refund.ChargesAvailable.ShouldBe(1);
    }

    [Fact]
    public async Task SwordAndBoard_LeavesTheRunningRechargeWhereItWas()
    {
        var parser = await AnalyzeWithTalents(
            [Talents.SwordAndBoard],
            Cast(PullStart + 1_000, Spells.ShieldSlam),
            ApplyBuff(PullStart + 2_000, Spells.SwordAndBoardBuff),
            Cast(PullStart + 2_500, Spells.ShieldSlam));

        var updates = ShieldSlamUpdates(parser);
        var started = updates.First(update => update.UpdateType == UpdateSpellUsableType.BeginCooldown);
        var refund = updates.Last(update => update.Timestamp == PullStart + 2_500);

        refund.UpdateType.ShouldBe(UpdateSpellUsableType.RestoreCharge);
        refund.ChargesAvailable.ShouldBe(1);
        refund.ExpectedRechargeTimestamp.ShouldBe(started.ExpectedRechargeTimestamp);
        refund.ExpectedRechargeTimestamp.ShouldBeLessThan(PullStart + 2_500 + refund.ExpectedRechargeDuration);
    }

    [Fact]
    public async Task SwordAndBoard_CountsAProcThatExpiredUnused()
    {
        var parser = await AnalyzeWithTalents(
            [Talents.SwordAndBoard],
            ApplyBuff(PullStart + 1_000, Spells.SwordAndBoardBuff),
            RemoveBuff(PullStart + 5_000, Spells.SwordAndBoardBuff));

        var analyzer = parser.GetModule<SwordAndBoardAnalyzer>().ShouldNotBeNull();

        analyzer.Procs.ShouldBe(1);
        analyzer.FreeCasts.ShouldBe(0);
        analyzer.ProcsExpired.ShouldBe(1);
        analyzer.Efficiency.ShouldBe(0);
    }

    [Fact]
    public async Task SwordAndBoard_AttributesTheFreeCastsDamageIncludingCleave()
    {
        var parser = await AnalyzeWithTalents(
            [Talents.SwordAndBoard],
            Cast(PullStart + 1_000, Spells.ShieldSlam),
            DamageDealt(PullStart + 1_050, Spells.ShieldSlamDamage, 500),
            ApplyBuff(PullStart + 1_500, Spells.SwordAndBoardBuff),
            Cast(PullStart + 2_000, Spells.ShieldSlam),
            DamageDealt(PullStart + 2_050, Spells.ShieldSlamDamage, 900),
            DamageDealt(PullStart + 2_060, Spells.ShieldSlamCleaveDamage, 300),
            Cast(PullStart + 12_000, Spells.ShieldSlam),
            DamageDealt(PullStart + 12_050, Spells.ShieldSlamDamage, 700));

        parser.GetModule<SwordAndBoardAnalyzer>().ShouldNotBeNull().ExtraDamage.ShouldBe(1_200);
    }

    [Fact]
    public async Task ACastOutsideEveryPull_ReachesModulesButNotPullAnalyzers()
    {
        var parser = await AnalyzeWithTalents(
            [Talents.ShieldMastery],
            Cast(PullStart - 500, Spells.MeasuredStrike),
            Cast(PullStart - 400, Spells.ShieldThrow),
            DamageTaken(PullStart - 300, 100, 100, 0, toughness: 1_000));

        parser.GetModule<ShieldMasteryAnalyzer>().ShouldNotBeNull().HitsTaken.ShouldBe(1);
        parser.VeteranOfWarAnalyzers.ShouldHaveSingleItem().Analyzer.CooldownReduction.Total.ShouldBe(0);
    }

    private static List<UpdateSpellUsableEvent> ShieldSlamUpdates(HelenaCombatLogParser parser) =>
        [.. parser.Events
            .OfType<UpdateSpellUsableEvent>()
            .Where(update => update.Ability.Id == Spells.ShieldSlam.FSLID)];

    private static async Task<ToughnessAnalyzer> BandAnalyzer(params Talent[] talents)
    {
        var parser = await AnalyzeWithTalents(
            talents,
            ToughnessSample(PullStart + 1_000, 1_000),
            ToughnessSample(PullEnd - 1_000, 1_000));

        return parser.ToughnessAnalyzers.ShouldHaveSingleItem().Analyzer;
    }
}
