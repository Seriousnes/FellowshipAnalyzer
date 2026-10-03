using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Xavian.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Xavian.Spells;

using static FellowshipAnalyzer.Heroes.Xavian.Tests.Analysis.XavianAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Xavian.Tests.Analysis;

public sealed class SwiftReprievalAnalyzerTests
{
    private static async Task<SwiftReprievalAnalyzer> Analyzer(params Core.Events.Event[] events) =>
        (await Analyze(events)).SwiftReprievalAnalyzers.ShouldHaveSingleItem().Analyzer;

    [Fact]
    public async Task SolarBladesAtThreeStacks_IsCountedAsLostStack()
    {
        var analyzer = await Analyzer(
            ApplyBuff(PullStart + 1_000, Spells.SwiftReprieval),
            ApplyBuffStack(PullStart + 2_000, Spells.SwiftReprieval, 2),
            ApplyBuffStack(PullStart + 3_000, Spells.SwiftReprieval, 3),
            Cast(PullStart + 4_000, Spells.SolarBlades));

        analyzer.SolarBladesCasts.ShouldBe(1);
        analyzer.SolarBladesAtCap.ShouldBe(1);
        analyzer.SolarBladesAtCapTimestamps.ShouldBe([PullStart + 4_000]);
    }

    [Fact]
    public async Task SolarBladesBelowThreeStacks_IsNotCounted()
    {
        var analyzer = await Analyzer(
            ApplyBuff(PullStart + 1_000, Spells.SwiftReprieval),
            ApplyBuffStack(PullStart + 2_000, Spells.SwiftReprieval, 2),
            Cast(PullStart + 3_000, Spells.SolarBlades),
            ApplyBuffStack(PullStart + 3_000, Spells.SwiftReprieval, 3),
            Cast(PullStart + 4_000, Spells.BrilliantFlash),
            RemoveBuffStack(PullStart + 4_000, Spells.SwiftReprieval, 2),
            Cast(PullStart + 5_000, Spells.SolarBlades));

        analyzer.SolarBladesCasts.ShouldBe(2);
        analyzer.SolarBladesAtCap.ShouldBe(0);
        analyzer.BrilliantFlashCasts.ShouldBe(1);
    }

    [Fact]
    public async Task StacksCarriedIntoTheNextPull_StillFlagSolarBladesAtCap()
    {
        var dungeon = BossDungeon with
        {
            DungeonPulls =
            [
                new DungeonPull(1, 0, false, 1_000, 20_000, "Trash", null),
                new DungeonPull(2, 0, false, 30_000, 50_000, "Trash", null),
            ],
        };

        var parser = await AnalyzeIn(
            dungeon,
            ApplyBuff(2_000, Spells.SwiftReprieval),
            ApplyBuffStack(3_000, Spells.SwiftReprieval, 2),
            ApplyBuffStack(4_000, Spells.SwiftReprieval, 3),
            Cast(31_000, Spells.SolarBlades));

        parser.SwiftReprievalAnalyzers.Count.ShouldBe(2);
        parser.SwiftReprievalAnalyzers[1].Analyzer.SolarBladesAtCap.ShouldBe(1);
    }

    [Fact]
    public async Task APullWithNoSolarBlades_ReadsZero()
    {
        var analyzer = await Analyzer(Cast(PullStart + 1_000, Spells.BrilliantFlash));

        analyzer.SolarBladesCasts.ShouldBe(0);
        analyzer.SolarBladesAtCap.ShouldBe(0);
    }
}
