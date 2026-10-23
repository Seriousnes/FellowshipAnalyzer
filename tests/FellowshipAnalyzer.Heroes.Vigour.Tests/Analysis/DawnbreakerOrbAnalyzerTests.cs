using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Vigour.Spells;

using static FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis.VigourAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis;

public sealed class DawnbreakerOrbAnalyzerTests
{
    [Fact]
    public async Task EachOrbCountsDistinctAlliesAndEnemiesItReached()
    {
        var parser = await Analyze(
            Cast(PullStart, Spells.DawnbreakerOrb),
            Heal(PullStart + 500, Spells.DawnbreakerOrb, TankId),
            Heal(PullStart + 600, Spells.DawnbreakerOrb, AllyId),
            Damage(PullStart + 700, Spells.DawnbreakerOrb, EnemyId),
            Heal(PullStart + 3_000, Spells.DawnbreakerOrb, TankId),
            Damage(PullStart + 3_100, Spells.DawnbreakerOrb, SecondEnemyId));

        var cast = parser.DawnbreakerOrbAnalyzers.ShouldHaveSingleItem().Analyzer.Casts.ShouldHaveSingleItem();

        cast.AlliesHit.ShouldBe(2);
        cast.EnemiesHit.ShouldBe(2);
        cast.TargetsHit.ShouldBe(4);
    }

    [Fact]
    public async Task AHitAfterTheFlightWindowIsNotCreditedToTheOrb()
    {
        var parser = await Analyze(
            Cast(PullStart, Spells.DawnbreakerOrb),
            Heal(PullStart + 500, Spells.DawnbreakerOrb, TankId),
            Damage(PullStart + 9_000, Spells.DawnbreakerOrb, EnemyId));

        var analyzer = parser.DawnbreakerOrbAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.CastCount.ShouldBe(1);
        analyzer.AverageTargetsHit.ShouldBe(1d);
    }

    [Fact]
    public void DawnbreakerOrbCostsTwoRadiantRunes() =>
        Spells.DawnbreakerOrb.Cost(FellowshipAnalyzer.Core.Game.ResourceTypes.Primary).ShouldBe(2);
}
