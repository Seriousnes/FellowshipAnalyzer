using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Xavian.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Xavian.Spells;
using Talents = FellowshipAnalyzer.Core.Common.Spells.Xavian.Talents;

using static FellowshipAnalyzer.Heroes.Xavian.Tests.Analysis.XavianAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Xavian.Tests.Analysis;

public sealed class SolarShieldAnalyzerTests
{
    private static async Task<SolarShieldAnalyzer> Analyzer(params Core.Events.Event[] events) =>
        (await Analyze(events)).SolarShieldAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<SolarShieldAnalyzer>();

    [Fact]
    public async Task ARefreshExtendsTheWindowRatherThanSplittingIt()
    {
        var analyzer = await Analyzer(
            Cast(PullStart, Spells.SolarShield, PlayerId),
            ApplyBuff(PullStart, Spells.SolarShieldAbsorb),
            Cast(PullStart + 5_000, Spells.SolarShield, PlayerId),
            RefreshBuff(PullStart + 5_000, Spells.SolarShieldAbsorb),
            RemoveBuff(PullStart + 12_000, Spells.SolarShieldAbsorb));

        analyzer.WindowCount.ShouldBe(1);
        analyzer.ActiveMs.ShouldBe(12_000);
        analyzer.Uptime.ShouldBe(0.2, 0.001);
    }

    [Fact]
    public async Task CastsOnAnAlly_CountAsCastsButNotAsUptime()
    {
        var analyzer = await Analyzer(
            Cast(PullStart, Spells.SolarShield, PlayerId),
            Cast(PullStart + 6_000, Spells.SolarShield, AllyId),
            ApplyBuff(PullStart + 6_000, Spells.SolarShieldAbsorb, AllyId),
            RemoveBuff(PullStart + 14_000, Spells.SolarShieldAbsorb, AllyId));

        analyzer.Casts.ShouldBe(2);
        analyzer.CastsOnSelf.ShouldBe(1);
        analyzer.ActiveMs.ShouldBe(0);
        analyzer.CastsPerMinute.ShouldBe(2, 0.001);
    }

    [Fact]
    public async Task HitsInsideTheWindow_AreAttributedToIt()
    {
        var analyzer = await Analyzer(
            ApplyBuff(PullStart, Spells.SolarShieldAbsorb),
            DamageTaken(PullStart + 1_000, 400, absorbed: 600),
            RemoveBuff(PullStart + 2_000, Spells.SolarShieldAbsorb),
            DamageTaken(PullStart + 3_000, 1_000));

        analyzer.HitsInWindows.ShouldBe(1);
        analyzer.AbsorbedInWindow.ShouldBe(600);
        analyzer.DamageTakenInWindow.ShouldBe(400);
    }

    [Fact]
    public async Task MagicWardUptime_IsReadOnlyWhenTheTalentIsTaken()
    {
        var events = new Core.Events.Event[]
        {
            ApplyBuff(PullStart, Spells.MagicWard),
            RemoveBuff(PullStart + 4_000, Spells.MagicWard),
            ApplyBuff(PullStart + 10_000, Spells.MagicWard),
            RemoveBuff(PullStart + 14_000, Spells.MagicWard),
        };

        var talented = (await AnalyzeWithTalents([Talents.MagicWard], events))
            .SolarShieldAnalyzers.ShouldHaveSingleItem().Analyzer.ShouldBeOfType<SolarShieldAnalyzer>();
        var untalented = await Analyzer(events);

        talented.MagicWardTaken.ShouldBeTrue();
        talented.MagicWardActiveMs.ShouldBe(8_000);
        untalented.MagicWardTaken.ShouldBeFalse();
    }

    [Fact]
    public async Task AShieldCarriedIntoAPull_OpensAWindowAtThePullStart()
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
            ApplyBuff(18_000, Spells.SolarShieldAbsorb),
            DamageTaken(22_000, 100, absorbed: 300),
            RemoveBuff(25_000, Spells.SolarShieldAbsorb));

        var second = parser.SolarShieldAnalyzers[1].Analyzer.ShouldBeOfType<SolarShieldAnalyzer>();
        second.ActiveMs.ShouldBe(4_000);
        second.HitsInWindows.ShouldBe(1);
    }
}
