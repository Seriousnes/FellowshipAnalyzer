using FellowshipAnalyzer.Heroes.Xavian.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Xavian.Spells;

using static FellowshipAnalyzer.Heroes.Xavian.Tests.Analysis.XavianAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Xavian.Tests.Analysis;

public sealed class ShiningHaloAnalyzerTests
{
    private static async Task<ShiningHaloAnalyzer> Analyzer(params Core.Events.Event[] events) =>
        (await Analyze(events)).ShiningHaloAnalyzers.ShouldHaveSingleItem().Analyzer;

    [Fact]
    public async Task Casts_CountEveryShiningHalo()
    {
        var analyzer = await Analyzer(
            Cast(PullStart, Spells.ShiningHalo),
            Cast(PullStart + 30_000, Spells.ShiningHalo));

        analyzer.Casts.ShouldBe(2);
    }

    [Fact]
    public async Task BrilliantFlash_IsCountedInTheHaloOnlyWhileStandingInIt()
    {
        var analyzer = await Analyzer(
            ApplyBuff(PullStart, Spells.ShiningHaloSelfBuff),
            Cast(PullStart + 2_000, Spells.BrilliantFlash),
            RemoveBuff(PullStart + 5_000, Spells.ShiningHaloSelfBuff),
            Cast(PullStart + 7_000, Spells.BrilliantFlash));

        analyzer.BrilliantFlashCasts.ShouldBe(2);
        analyzer.BrilliantFlashInHalo.ShouldBe(1);
    }
}
