using FellowshipAnalyzer.Heroes.Meiko.Analysis;
using FellowshipAnalyzer.Heroes.Meiko.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Meiko.Spells;

using static FellowshipAnalyzer.Heroes.Meiko.Tests.Analysis.MeikoAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Meiko.Tests.Analysis;

public sealed class StoneShieldAnalyzerTests
{
    [Fact]
    public async Task Uptime_CountsAnyStoneCount_AndSplitsTimeByStones()
    {
        var parser = await Analyze(
            Cast(PullStart, Spells.StoneShieldAlt),
            ApplyBuff(PullStart, Spells.StoneShieldBuff),
            ApplyBuffStack(PullStart, Spells.StoneShieldBuff, 2),
            ApplyBuffStack(PullStart, Spells.StoneShieldBuff, 3),
            RemoveBuffStack(PullStart + 20_000, Spells.StoneShieldBuff, 2),
            RemoveBuffStack(PullStart + 30_000, Spells.StoneShieldBuff, 1),
            RemoveBuff(PullStart + 40_000, Spells.StoneShieldBuff));

        var analyzer = Single(parser);

        analyzer.Casts.Count.ShouldBe(1);
        analyzer.ActiveMs.ShouldBe(40_000);
        analyzer.MsAtStacks[3].ShouldBe(20_000);
        analyzer.MsAtStacks[2].ShouldBe(10_000);
        analyzer.MsAtStacks[1].ShouldBe(10_000);
        analyzer.InactiveMs.ShouldBe(20_000);
        analyzer.Drops.ShouldBe(1);
    }

    [Fact]
    public async Task ARecastBeforeTheLastStoneBreaks_IsNotADrop()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart, Spells.StoneShieldBuff),
            Cast(PullStart + 10_000, Spells.StoneShieldAlt),
            RemoveBuff(PullStart + 10_000, Spells.StoneShieldBuff),
            ApplyBuff(PullStart + 10_000, Spells.StoneShieldBuff),
            ApplyBuffStack(PullStart + 10_000, Spells.StoneShieldBuff, 3),
            RemoveBuffStack(PullStart + 50_000, Spells.StoneShieldBuff, 2));

        var analyzer = Single(parser);

        analyzer.Uptime.ShouldBe(1, 0.001);
        analyzer.Drops.ShouldBe(0);
    }

    [Fact]
    public async Task ARecastAfterTheLastStoneBroke_IsADrop()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart, Spells.StoneShieldBuff),
            RemoveBuff(PullStart + 10_000, Spells.StoneShieldBuff),
            Cast(PullStart + 12_000, Spells.StoneShieldAlt),
            ApplyBuff(PullStart + 12_000, Spells.StoneShieldBuff));

        var analyzer = Single(parser);

        analyzer.Drops.ShouldBe(1);
        analyzer.InactiveMs.ShouldBe(2_000);
    }

    private static StoneShieldAnalyzer Single(MeikoCombatLogParser parser) =>
        (StoneShieldAnalyzer)parser.StoneShieldAnalyzers.ShouldHaveSingleItem().Analyzer;
}
