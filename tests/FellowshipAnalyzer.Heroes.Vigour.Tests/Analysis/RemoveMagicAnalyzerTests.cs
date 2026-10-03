using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Vigour.Spells;

using static FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis.VigourAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis;

public sealed class RemoveMagicAnalyzerTests
{
    [Fact]
    public async Task CastsAreSplitByWhetherTheyDispelled()
    {
        var parser = await Analyze(
            Cast(PullStart, Spells.RemoveMagic, AllyId),
            Cast(PullStart + 7_000, Spells.RemoveMagic, AllyId),
            Dispel(PullStart + 7_000, Spells.RemoveMagic, AllyId),
            Cast(PullStart + 8_000, Spells.Dawnflare));

        var analyzer = parser.RemoveMagic.ShouldNotBeNull();

        analyzer.Casts.ShouldBe(2);
        analyzer.CastsWithDispel.ShouldBe(1);
        analyzer.CastsWithoutDispel.ShouldBe(1);
        analyzer.Dispels.ShouldBe(1);
    }
}
