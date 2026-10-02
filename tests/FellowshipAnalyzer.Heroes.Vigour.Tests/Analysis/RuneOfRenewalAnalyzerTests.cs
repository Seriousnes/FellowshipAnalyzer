using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Vigour.Spells;

using static FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis.VigourAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis;

public sealed class RuneOfRenewalAnalyzerTests
{
    [Fact]
    public async Task UptimeIsMeasuredPerPartyMemberAndTheTankIsIdentified()
    {
        var parser = await Analyze(
            Cast(PullStart, Spells.RuneOfRenewal, TankId),
            ApplyBuff(PullStart, Spells.RuneOfRenewalBuff, TankId),
            ApplyBuff(PullStart, Spells.RuneOfRenewalBuff, AllyId),
            RemoveBuff(PullStart + 30_000, Spells.RuneOfRenewalBuff, AllyId));

        var analyzer = parser.RuneOfRenewalAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.Casts.ShouldBe(1);
        analyzer.TankUptime.ShouldNotBeNull().ShouldBe(1d, 0.001);
        analyzer.Allies.Single(ally => ally.ActorId == AllyId).Uptime.ShouldBe(0.5, 0.001);
        analyzer.Allies.Single(ally => ally.ActorId == PlayerId).Uptime.ShouldBe(0d);
        analyzer.PartyUptime.ShouldBe(0.5, 0.001);
    }

    [Fact]
    public async Task ARuneAppliedBeforeThePullCountsFromThePullStart()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart - 500, Spells.RuneOfRenewalBuff, TankId),
            RemoveBuff(PullStart + 15_000, Spells.RuneOfRenewalBuff, TankId));

        var analyzer = parser.RuneOfRenewalAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.TankUptime.ShouldNotBeNull().ShouldBe(0.25, 0.001);
    }
}
