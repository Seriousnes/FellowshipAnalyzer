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
    public async Task AnOrbNeverCastIsReadyForTheWholePull()
    {
        var parser = await Analyze(Cast(PullStart + 1_000, Spells.Dawnflare));

        var analyzer = parser.DawnbreakerOrbAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.CastCount.ShouldBe(0);
        analyzer.ReadyShare.ShouldBe(1d, 0.001);
    }

    [Fact]
    public async Task CastingOnCooldownLeavesLittleReadyTime()
    {
        var events = Enumerable.Range(0, 8)
            .Select(i => Cast(PullStart + 2_000 + i * 7_000, Spells.DawnbreakerOrb))
            .ToArray();

        var parser = await Analyze(events);

        var analyzer = parser.DawnbreakerOrbAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.CastCount.ShouldBe(8);
        analyzer.ReadyMs.ShouldBeLessThan(2_000 + 8 * 1_000);
    }
}
