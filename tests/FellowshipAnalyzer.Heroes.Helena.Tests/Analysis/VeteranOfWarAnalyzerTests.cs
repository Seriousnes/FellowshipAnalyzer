using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Helena.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Helena.Spells;

using static FellowshipAnalyzer.Heroes.Helena.Tests.Analysis.HelenaAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Helena.Tests.Analysis;

public sealed class VeteranOfWarAnalyzerTests
{
    [Fact]
    public async Task ReductionAimedAtAnAbilityAlreadyAvailable_IsAllWaste()
    {
        var analyzer = await Analyze(Cast(PullStart + 1_000, Spells.MeasuredStrike));

        analyzer.GeneratedMs.ShouldBe(4_000);
        analyzer.AppliedMs.ShouldBe(0);
        analyzer.WastedMs.ShouldBe(4_000);
        analyzer.Efficiency.ShouldBe(0);
    }

    [Fact]
    public async Task ReductionAimedAtARunningCooldown_Lands()
    {
        var analyzer = await Analyze(
            Cast(PullStart + 1_000, Spells.Shockwave),
            Cast(PullStart + 2_000, Spells.ShieldSlam));

        var toShockwave = Pairing(analyzer, Spells.ShieldSlam.FSLID, Spells.Shockwave.FSLID);

        toShockwave.GeneratedMs.ShouldBe(3_000);
        toShockwave.AppliedMs.ShouldBe(3_000);
        toShockwave.WastedMs.ShouldBe(0);
    }

    [Fact]
    public async Task ReductionLongerThanTheRemainingCooldown_LandsOnlyWhatWasLeft()
    {
        var analyzer = await Analyze(
            Cast(PullStart + 1_000, Spells.Shockwave),
            Cast(PullStart + 29_000, Spells.HoldTheLine));

        var toShockwave = Pairing(analyzer, Spells.HoldTheLine.FSLID, Spells.Shockwave.FSLID);

        toShockwave.GeneratedMs.ShouldBe(10_000);
        toShockwave.AppliedMs.ShouldBe(2_000);
        toShockwave.WastedMs.ShouldBe(8_000);
    }

    [Fact]
    public async Task HoldTheLine_GeneratesAgainstAllFourOfItsTargets()
    {
        var analyzer = await Analyze(Cast(PullStart + 1_000, Spells.HoldTheLine));

        var targets = analyzer.Contributions
            .Where(contribution => contribution.SourceSpellId == Spells.HoldTheLine.FSLID.Value)
            .Select(contribution => contribution.TargetSpellId)
            .ToList();

        targets.ShouldBe(
            [
                Spells.ShieldSlam.FSLID.Value,
                Spells.ShieldThrow.FSLID.Value,
                Spells.Shockwave.FSLID.Value,
                Spells.ShieldsUp.FSLID.Value,
            ],
            ignoreOrder: true);
        analyzer.GeneratedMs.ShouldBe(40_000);
    }

    [Fact]
    public async Task AnActiveSpiritAbility_DoublesEveryReductionMadeUnderIt()
    {
        var analyzer = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.SiegebreakerBuff),
            Cast(PullStart + 2_000, Spells.MeasuredStrike),
            RemoveBuff(PullStart + 3_000, Spells.SiegebreakerBuff),
            Cast(PullStart + 4_000, Spells.MeasuredStrike));

        analyzer.SawActiveUltimate.ShouldBeTrue();
        analyzer.GeneratedMs.ShouldBe((4_000 * 2) + 4_000);
    }

    [Fact]
    public async Task BySource_CountsCastsRatherThanSourceToTargetPairings()
    {
        var analyzer = await Analyze(
            Cast(PullStart + 1_000, Spells.HoldTheLine),
            Cast(PullStart + 2_000, Spells.HoldTheLine));

        analyzer.BySource
            .Single(contribution => contribution.SourceSpellId == Spells.HoldTheLine.FSLID.Value)
            .Events.ShouldBe(2);
    }

    [Fact]
    public async Task ACastTheModelBelievedImpossible_IsCountedAgainstTheModelNotThePlayer()
    {
        var analyzer = await Analyze(
            Cast(PullStart + 1_000, Spells.Shockwave),
            Cast(PullStart + 2_000, Spells.Shockwave));

        analyzer.ModelDisagreements.ShouldBe(1);
        analyzer.TargetCasts.ShouldBe(2);
        analyzer.ModelAgreement.ShouldBe(0.5);
        analyzer.CastsWhileModelledUnavailable[Spells.Shockwave.FSLID].ShouldBe(1);
    }

    [Fact]
    public async Task CastsTheModelAgreesWith_LeaveTheAgreementIntact()
    {
        var analyzer = await Analyze(
            Cast(PullStart + 1_000, Spells.Shockwave),
            Cast(PullStart + 40_000, Spells.Shockwave));

        analyzer.ModelDisagreements.ShouldBe(0);
        analyzer.ModelAgreement.ShouldBe(1d);
    }

    [Fact]
    public async Task TheComboTable_MatchesTheSeasonThreeReductionValues()
    {
        var combos = VeteranOfWarAnalyzer.Combos.ToLookup(combo => combo.SourceSpellId);

        combos[Spells.MeasuredStrike.FSLID].Select(combo => combo.ReductionMs).ShouldAllBe(ms => ms == 2_000);
        combos[Spells.PowerStrike.FSLID].Select(combo => combo.ReductionMs).ShouldAllBe(ms => ms == 2_000);
        combos[Spells.ShieldSlam.FSLID].ShouldHaveSingleItem().ReductionMs.ShouldBe(3_000);
        combos[Spells.ShieldThrow.FSLID].ShouldHaveSingleItem().ReductionMs.ShouldBe(3_000);
        combos[Spells.Shockwave.FSLID].ShouldHaveSingleItem().ReductionMs.ShouldBe(6_000);
        combos[Spells.HoldTheLine.FSLID].Select(combo => combo.ReductionMs).ShouldAllBe(ms => ms == 10_000);
        VeteranOfWarAnalyzer.ActiveUltimateScaler.ShouldBe(2.0);
    }

    private static CooldownContribution Pairing(VeteranOfWarAnalyzer analyzer, int source, int target) =>
        analyzer.Contributions.Single(contribution =>
            contribution.SourceSpellId == source && contribution.TargetSpellId == target);

    private static async Task<VeteranOfWarAnalyzer> Analyze(params Event[] events)
    {
        var parser = await HelenaAnalysisFixture.Analyze(events);
        return parser.VeteranOfWarAnalyzers.ShouldHaveSingleItem().Analyzer;
    }
}
