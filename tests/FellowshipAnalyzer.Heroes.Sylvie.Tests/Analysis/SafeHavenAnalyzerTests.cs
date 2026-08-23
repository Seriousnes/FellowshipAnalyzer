using FellowshipAnalyzer.Heroes.Sylvie.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Sylvie.Spells;

using static FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis.SylvieAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

public sealed class SafeHavenAnalyzerTests
{
    [Fact]
    public async Task APlacementHoldingEveryAllyForItsFullDurationReadsAsWhollyHeld()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.SafeHaven),
            ApplyBuff(PullStart + 1_000, Spells.SafeHavenIncreasedHealingTakenBuff, TankId),
            ApplyBuff(PullStart + 1_000, Spells.SafeHavenIncreasedHealingTakenBuff, AllyId),
            RemoveBuff(PullStart + 16_000, Spells.SafeHavenIncreasedHealingTakenBuff, TankId),
            RemoveBuff(PullStart + 16_000, Spells.SafeHavenIncreasedHealingTakenBuff, AllyId));

        var analyzer = parser.SafeHavenAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.Casts.ShouldBe(1);
        analyzer.AlliesAffected.ShouldBe(2);
        analyzer.AllyMs.ShouldBe(2 * SylvieKit.SafeHavenDurationMs);
        analyzer.HeldShare.ShouldBe(1d, 0.001);
        analyzer.CastsWithoutAnAlly.ShouldBe(0);
    }

    [Fact]
    public async Task AnAllyLeavingEarlyLowersTheHeldShareAgainstTheFullDuration()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.SafeHaven),
            ApplyBuff(PullStart + 1_000, Spells.SafeHavenIncreasedHealingTakenBuff, TankId),
            RemoveBuff(PullStart + 6_000, Spells.SafeHavenIncreasedHealingTakenBuff, TankId));

        var analyzer = parser.SafeHavenAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.AlliesAffected.ShouldBe(1);
        analyzer.AllyMs.ShouldBe(5_000);
        analyzer.HeldShare.ShouldBe(5_000 / (double)SylvieKit.SafeHavenDurationMs, 0.001);
    }

    [Fact]
    public async Task APlacementNoAllyEverEnteredIsCounted()
    {
        var parser = await Analyze(Cast(PullStart + 1_000, Spells.SafeHaven));

        var analyzer = parser.SafeHavenAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.Casts.ShouldBe(1);
        analyzer.CastsWithoutAnAlly.ShouldBe(1);
        analyzer.AlliesAffected.ShouldBe(0);
        analyzer.Placements.ShouldHaveSingleItem().HeldShare.ShouldBe(0d);
    }

    [Fact]
    public async Task APlacementLateInThePullIsMeasuredOnlyAgainstTheTimeItHad()
    {
        var parser = await Analyze(
            Cast(PullEnd - 5_000, Spells.SafeHaven),
            ApplyBuff(PullEnd - 5_000, Spells.SafeHavenIncreasedHealingTakenBuff, TankId));

        var analyzer = parser.SafeHavenAnalyzers.ShouldHaveSingleItem().Analyzer;

        var placement = analyzer.Placements.ShouldHaveSingleItem();
        placement.Truncated.ShouldBeTrue();
        placement.AvailableMs.ShouldBe(5_000);
        placement.HeldShare.ShouldBe(1d, 0.001);
    }

    [Fact]
    public async Task CoverageWithNoPlacementInThePullIsReportedApart()
    {
        var parser = await Analyze(
            ApplyBuff(PullEnd - 5_000, Spells.SafeHavenIncreasedHealingTakenBuff, TankId));

        var analyzer = parser.SafeHavenAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.Casts.ShouldBe(0);
        analyzer.Placements.ShouldBeEmpty();
        analyzer.PrepullAllies.ShouldBe(1);
        analyzer.PrepullAllyMs.ShouldBe(5_000);
    }
}
