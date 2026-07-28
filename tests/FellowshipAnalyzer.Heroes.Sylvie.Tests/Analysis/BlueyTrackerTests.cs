using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Sylvie.Spells;

using static FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis.SylvieAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

public sealed class BlueyTrackerTests
{
    [Fact]
    public async Task SendingBlueyToAnAllyOpensAPostingOnThatAlly()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.FluttercallProtect, targetId: TankId),
            ApplyBuff(PullStart + 1_000, Spells.FluttercallProtectBuff, TankId));

        var posting = parser.BlueyTracker.ShouldNotBeNull().Postings.ShouldHaveSingleItem();

        posting.TargetId.ShouldBe(TankId);
        posting.OnSylvie.ShouldBeFalse();
        posting.Start.ShouldBe(PullStart + 1_000);
    }

    [Fact]
    public async Task RecallingBlueyClosesTheAllyPostingAndOpensASelfPosting()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.FluttercallProtectBuff, TankId),
            ApplyBuff(PullStart + 11_000, Spells.FluttercallEmbraceBuff, PlayerId),
            RemoveBuff(PullStart + 11_000, Spells.FluttercallProtectBuff, TankId));

        var tracker = parser.BlueyTracker.ShouldNotBeNull();

        tracker.Postings.Count.ShouldBe(2);
        tracker.Postings[0].TargetId.ShouldBe(TankId);
        tracker.Postings[0].End.ShouldBe(PullStart + 11_000);
        tracker.Postings[1].OnSylvie.ShouldBeTrue();
        tracker.AllyMsBetween(PullStart, PullEnd).ShouldBe(10_000);
        tracker.SelfMsBetween(PullStart, PullEnd).ShouldBe(PullEnd - (PullStart + 11_000));
    }

    [Fact]
    public async Task ARemovalWithNoApplicationBackdatesThePostingToTheFightStart()
    {
        var parser = await Analyze(
            RemoveBuff(PullStart + 4_000, Spells.FluttercallEmbraceBuff, PlayerId));

        var posting = parser.BlueyTracker.ShouldNotBeNull().Postings.ShouldHaveSingleItem();

        posting.OnSylvie.ShouldBeTrue();
        posting.Start.ShouldBe(0);
        posting.End.ShouldBe(PullStart + 4_000);
    }

    [Fact]
    public async Task ThePullAnalyzerSplitsTimeBetweenTheAllyAndSylvie()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart - 500, Spells.FluttercallProtectBuff, TankId),
            RemoveBuff(PullStart + 21_000, Spells.FluttercallProtectBuff, TankId),
            ApplyBuff(PullStart + 21_000, Spells.FluttercallEmbraceBuff, PlayerId));

        var analyzer = parser.BlueyAssignmentAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.OnAllyMs.ShouldBe(21_000);
        analyzer.OnSylvieMs.ShouldBe(PullEnd - PullStart - 21_000);
        analyzer.UnplacedMs.ShouldBe(0);
        analyzer.Holders.ShouldBe(2);
        analyzer.OnAllyShare.ShouldBe(21_000 / 60_000d, 0.001);
    }

    [Fact]
    public async Task CastsThatMoveBlueyAreCounted()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.FluttercallProtect, targetId: TankId),
            Cast(PullStart + 20_000, Spells.FluttercallEmbrace, targetId: PlayerId));

        parser.BlueyTracker.ShouldNotBeNull().Reassignments.ShouldBe(2);
    }
}
