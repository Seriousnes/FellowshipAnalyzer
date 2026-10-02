using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Vigour.Spells;

using static FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis.VigourAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis;

public sealed class SoulbrandAnalyzerTests
{
    [Fact]
    public async Task CoverageCountsOverlappingEnemiesOnce()
    {
        var parser = await Analyze(
            Cast(PullStart, Spells.Soulbrand),
            ApplyDebuff(PullStart, Spells.SoulbrandDot, EnemyId),
            ApplyDebuff(PullStart + 10_000, Spells.SoulbrandDot, SecondEnemyId),
            RemoveDebuff(PullStart + 20_000, Spells.SoulbrandDot, EnemyId),
            RemoveDebuff(PullStart + 30_000, Spells.SoulbrandDot, SecondEnemyId));

        var analyzer = parser.SoulbrandAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.CastCount.ShouldBe(1);
        analyzer.CoveredMs.ShouldBe(30_000);
        analyzer.Coverage.ShouldBe(0.5, 0.001);
        analyzer.AverageEnemies.ShouldBe(40_000 / (double)PullDuration, 0.001);
    }
}
