using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Helena.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Helena.Spells;

using static FellowshipAnalyzer.Heroes.Helena.Tests.Analysis.HelenaAnalysisFixture;
using FellowshipAnalyzer.Core.Common;

namespace FellowshipAnalyzer.Heroes.Helena.Tests.Analysis;

public sealed class ToughnessTrackerTests
{
    [Fact]
    public async Task AGeneratorCastAtAFullBar_CountsAsOvercap()
    {
        var tracker = await Analyze(
            Cast(PullStart + 1_000, Spells.ShieldSlam, toughness: 1_000));

        tracker.OvercappedCastsFor(Spells.ShieldSlam.FSLID).ShouldBe(1);
        tracker.GeneratorCastsFor(Spells.ShieldSlam.FSLID).ShouldBe(1);
        tracker.Overcaps.ShouldHaveSingleItem().SpellId.ShouldBe(Spells.ShieldSlam.FSLID.Value);
    }

    [Fact]
    public async Task AGeneratorCastBelowTheCap_IsCountedButNotOvercapped()
    {
        var tracker = await Analyze(
            Cast(PullStart + 1_000, Spells.ShieldSlam, toughness: 999));

        tracker.OvercappedCastsFor(Spells.ShieldSlam.FSLID).ShouldBe(0);
        tracker.GeneratorCastsFor(Spells.ShieldSlam.FSLID).ShouldBe(1);
        tracker.Overcaps.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheCapIsResolvedAgainstEachCastsOwnMaximum()
    {
        var tracker = await Analyze(
            Cast(PullStart + 1_000, Spells.Shockwave, toughness: 1_000, toughnessMax: 1_000),
            Cast(PullStart + 2_000, Spells.Shockwave, toughness: 1_000, toughnessMax: 4_000));

        tracker.GeneratorCastsFor(Spells.Shockwave.FSLID).ShouldBe(2);
        tracker.OvercappedCastsFor(Spells.Shockwave.FSLID).ShouldBe(1);
    }

    [Fact]
    public async Task EveryGeneratorIsAttributedSeparately()
    {
        var tracker = await Analyze(
            Cast(PullStart + 1_000, Spells.ShieldsUp, toughness: 1_000),
            Cast(PullStart + 2_000, Spells.Shockwave, toughness: 1_000),
            Cast(PullStart + 3_000, Spells.ShieldSlam, toughness: 1_000));

        tracker.OvercappedCastsFor(Spells.ShieldsUp.FSLID).ShouldBe(1);
        tracker.OvercappedCastsFor(Spells.Shockwave.FSLID).ShouldBe(1);
        tracker.OvercappedCastsFor(Spells.ShieldSlam.FSLID).ShouldBe(1);
        tracker.OvercappedCasts.ShouldBe(3);
        tracker.OvercappedCastShare.ShouldBe(1d);
    }

    [Fact]
    public async Task ANonGeneratorCastAtAFullBar_IsIgnored()
    {
        var tracker = await Analyze(
            Cast(PullStart + 1_000, Spells.MeasuredStrike, toughness: 1_000),
            Cast(PullStart + 2_000, Spells.HoldTheLine, toughness: 1_000));

        tracker.OvercappedCasts.ShouldBe(0);
        tracker.GeneratorCasts.ShouldBe(0);
        tracker.Overcaps.ShouldBeEmpty();
    }

    [Fact]
    public async Task ACastWithNoToughnessSnapshot_CountsButNeverOvercaps()
    {
        var tracker = await Analyze(Cast(PullStart + 1_000, Spells.ShieldSlam));

        tracker.GeneratorCastsFor(Spells.ShieldSlam.FSLID).ShouldBe(1);
        tracker.OvercappedCastsFor(Spells.ShieldSlam.FSLID).ShouldBe(0);
    }

    [Fact]
    public async Task ABlockTakenAtFullToughness_CountsAsOvercap()
    {
        var tracker = await Analyze(
            DamageTaken(PullStart + 1_000, 0, 10_000, 9_000, blocked: 1_000, hitType: HitType.Block, toughness: 1_000),
            DamageTaken(PullStart + 2_000, 0, 10_000, 9_000, blocked: 1_000, hitType: HitType.Block, toughness: 500));

        tracker.ObservedBlocks.ShouldBe(2);
        tracker.OvercappedBlocks.ShouldBe(1);
        tracker.OvercappedBlockShare.ShouldBe(0.5);
    }

    [Fact]
    public async Task ANonBlockHitAtAFullBar_IsNotABlockOvercap()
    {
        var tracker = await Analyze(
            DamageTaken(PullStart + 1_000, 500, 10_000, 9_500, toughness: 1_000));

        tracker.ObservedBlocks.ShouldBe(0);
        tracker.OvercappedBlocks.ShouldBe(0);
    }

    [Fact]
    public async Task NominalGenerationShares_ComeFromTheSeasonThreeStrengthScalers()
    {
        var byId = ToughnessTracker.Generators.ToDictionary(generator => generator.SpellId);

        byId[Spells.ShieldsUp.FSLID].NominalShare.ShouldBe(0.30, 0.001);
        byId[Spells.Shockwave.FSLID].NominalShare.ShouldBe(0.25, 0.001);
        byId[Spells.ShieldSlam.FSLID].NominalShare.ShouldBe(0.15, 0.001);
        ToughnessTracker.BlockGeneration.ShouldBe(0.24 / 7.7, 0.001);
    }

    private static async Task<ToughnessTracker> Analyze(params Event[] events)
    {
        var parser = await HelenaAnalysisFixture.Analyze(events);
        return parser.ToughnessTracker.ShouldNotBeNull();
    }
}
