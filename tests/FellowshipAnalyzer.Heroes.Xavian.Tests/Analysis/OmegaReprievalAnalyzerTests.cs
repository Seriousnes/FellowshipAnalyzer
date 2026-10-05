using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Xavian.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Xavian.Spells;

using static FellowshipAnalyzer.Heroes.Xavian.Tests.Analysis.XavianAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Xavian.Tests.Analysis;

public sealed class OmegaReprievalAnalyzerTests
{
    private static async Task<OmegaReprievalAnalyzer> Analyzer(params Core.Events.Event[] events) =>
        (await Analyze(events)).OmegaReprievalAnalyzers.ShouldHaveSingleItem().Analyzer;

    [Fact]
    public async Task StacksConsumedByBrilliantFlash_AreSpent()
    {
        var analyzer = await Analyzer(
            Cast(PullStart, Spells.OmegaReprieval),
            ApplyBuff(PullStart, Spells.OmegaReprievalBuff),
            ApplyBuffStack(PullStart, Spells.OmegaReprievalBuff, 2),
            Cast(PullStart + 1_000, Spells.BrilliantFlash),
            RemoveBuffStack(PullStart + 1_000, Spells.OmegaReprievalBuff, 1),
            Cast(PullStart + 2_500, Spells.BrilliantFlash),
            RemoveBuff(PullStart + 2_500, Spells.OmegaReprievalBuff));

        analyzer.Casts.ShouldBe(1);
        analyzer.StacksGained.ShouldBe(2);
        analyzer.StacksSpent.ShouldBe(2);
        analyzer.StacksExpired.ShouldBe(0);
    }

    [Fact]
    public async Task StacksRemovedWithNoBrilliantFlash_Expired()
    {
        var analyzer = await Analyzer(
            Cast(PullStart, Spells.OmegaReprieval),
            ApplyBuff(PullStart, Spells.OmegaReprievalBuff),
            ApplyBuffStack(PullStart, Spells.OmegaReprievalBuff, 2),
            Cast(PullStart + 1_000, Spells.BrilliantFlash),
            RemoveBuffStack(PullStart + 1_000, Spells.OmegaReprievalBuff, 1),
            RemoveBuff(PullStart + 12_000, Spells.OmegaReprievalBuff));

        analyzer.StacksSpent.ShouldBe(1);
        analyzer.StacksExpired.ShouldBe(1);
    }

    [Fact]
    public async Task GoldenHour_IsConsumedByTheNextOmnistrike()
    {
        var analyzer = await Analyzer(
            ApplyBuff(PullStart + 1_000, Spells.GoldenHour),
            Cast(PullStart + 2_000, Spells.Omnistrike),
            RemoveBuff(PullStart + 2_000, Spells.GoldenHour),
            ApplyBuff(PullStart + 10_000, Spells.GoldenHour),
            RemoveBuff(PullStart + 22_000, Spells.GoldenHour),
            Cast(PullStart + 30_000, Spells.Omnistrike));

        analyzer.GoldenHourProcs.ShouldBe(2);
        analyzer.GoldenHourConsumed.ShouldBe(1);
    }

    [Fact]
    public async Task APullWithNoStacks_ReadsZero()
    {
        var analyzer = await Analyzer(Cast(PullStart + 1_000, Spells.BrilliantFlash));

        analyzer.StacksGained.ShouldBe(0);
        analyzer.StacksSpent.ShouldBe(0);
        analyzer.StacksExpired.ShouldBe(0);
    }

    [Fact]
    public async Task AGoldenHourRefreshWhileUp_CountsAsAnotherProc()
    {
        var analyzer = await Analyzer(
            ApplyBuff(PullStart + 1_000, Spells.GoldenHour),
            RefreshBuff(PullStart + 3_000, Spells.GoldenHour),
            Cast(PullStart + 4_000, Spells.Omnistrike),
            RemoveBuff(PullStart + 4_000, Spells.GoldenHour));

        analyzer.GoldenHourProcs.ShouldBe(2);
        analyzer.GoldenHourConsumed.ShouldBe(1);
    }

    [Fact]
    public async Task AGoldenHourStackOnTopOfACast_IsGainedAndSpent()
    {
        var analyzer = await Analyzer(
            Cast(PullStart, Spells.OmegaReprieval),
            ApplyBuff(PullStart, Spells.OmegaReprievalBuff),
            ApplyBuffStack(PullStart, Spells.OmegaReprievalBuff, 2),
            ApplyBuffStack(PullStart + 500, Spells.OmegaReprievalBuff, 3),
            Cast(PullStart + 1_000, Spells.BrilliantFlash),
            RemoveBuffStack(PullStart + 1_000, Spells.OmegaReprievalBuff, 2),
            Cast(PullStart + 2_500, Spells.BrilliantFlash),
            RemoveBuffStack(PullStart + 2_500, Spells.OmegaReprievalBuff, 1),
            Cast(PullStart + 4_000, Spells.BrilliantFlash),
            RemoveBuff(PullStart + 4_000, Spells.OmegaReprievalBuff));

        analyzer.StacksGained.ShouldBe(3);
        analyzer.StacksSpent.ShouldBe(3);
        analyzer.StacksExpired.ShouldBe(0);
    }

    [Fact]
    public async Task StacksCarriedIntoAPull_AreSpentNotGained()
    {
        var dungeon = BossDungeon with
        {
            DungeonPulls =
            [
                new DungeonPull(1, 0, false, 1_000, 20_000, "Trash", null),
                new DungeonPull(2, 0, false, 21_000, 50_000, "Trash", null),
            ],
        };

        var parser = await AnalyzeIn(
            dungeon,
            Cast(19_000, Spells.OmegaReprieval),
            ApplyBuff(19_000, Spells.OmegaReprievalBuff),
            ApplyBuffStack(19_000, Spells.OmegaReprievalBuff, 2),
            Cast(22_000, Spells.BrilliantFlash),
            RemoveBuffStack(22_000, Spells.OmegaReprievalBuff, 1),
            Cast(23_000, Spells.BrilliantFlash),
            RemoveBuff(23_000, Spells.OmegaReprievalBuff));

        var second = parser.OmegaReprievalAnalyzers[1].Analyzer;
        second.StacksGained.ShouldBe(0);
        second.StacksSpent.ShouldBe(2);
        second.StacksExpired.ShouldBe(0);
    }

    [Fact]
    public async Task CarriedStacksThatExpire_CountEveryStackAsExpired()
    {
        var dungeon = BossDungeon with
        {
            DungeonPulls =
            [
                new DungeonPull(1, 0, false, 1_000, 20_000, "Trash", null),
                new DungeonPull(2, 0, false, 21_000, 50_000, "Trash", null),
            ],
        };

        var parser = await AnalyzeIn(
            dungeon,
            Cast(19_000, Spells.OmegaReprieval),
            ApplyBuff(19_000, Spells.OmegaReprievalBuff),
            ApplyBuffStack(19_000, Spells.OmegaReprievalBuff, 2),
            RemoveBuff(31_000, Spells.OmegaReprievalBuff));

        var second = parser.OmegaReprievalAnalyzers[1].Analyzer;
        second.StacksExpired.ShouldBe(2);
        second.StacksSpent.ShouldBe(0);
    }

    [Fact]
    public async Task AGoldenHourCarriedIntoAPull_IsConsumedByOmnistrike()
    {
        var dungeon = BossDungeon with
        {
            DungeonPulls =
            [
                new DungeonPull(1, 0, false, 1_000, 20_000, "Trash", null),
                new DungeonPull(2, 0, false, 21_000, 50_000, "Trash", null),
            ],
        };

        var parser = await AnalyzeIn(
            dungeon,
            ApplyBuff(19_000, Spells.GoldenHour),
            Cast(22_000, Spells.Omnistrike),
            RemoveBuff(22_000, Spells.GoldenHour));

        parser.OmegaReprievalAnalyzers[1].Analyzer.GoldenHourConsumed.ShouldBe(1);
    }
}
