using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Tariq.Analysis;
using FellowshipAnalyzer.Heroes.Tariq.Modules;

using Shouldly;

using Xunit;

using TariqSpells = FellowshipAnalyzer.Core.Common.Spells.Tariq.Spells;

namespace FellowshipAnalyzer.Heroes.Tariq.Tests.Analysis;

public sealed class FocusedWrathAnalyzerTests
{
    private static readonly int SelfBuff = TariqSpells.FocusedWrathSelfBuff.FSLID;

    [Fact]
    public async Task Analyze_FocusedWrath_RecordsTheSpenderThatConsumedTheBuff()
    {
        var (parser, _) = await ThunderCallWindowAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, TariqSpells.FocusedWrath.FSLID),
            Apply(1_100, SelfBuff),
            Cast(1_300, TariqSpells.HammerStorm.FSLID),
            Remove(1_300, SelfBuff),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;
        var window = analyzer.Windows.ShouldHaveSingleItem();

        window.OpenedAt.ShouldBe(1_100);
        window.ClosedAt.ShouldBe(1_300);
        window.Consumed.ShouldBeTrue();
        window.ConsumedBy.ShouldBe(TariqSpells.HammerStorm.FSLID);
        window.FromCast.ShouldBeTrue();
        window.ClippedByPullEnd.ShouldBeFalse();

        analyzer.WindowCount.ShouldBe(1);
        analyzer.ConsumedWindows.ShouldBe(1);
        analyzer.ExpiredWindows.ShouldBe(0);
        analyzer.HammerStormConsumptions.ShouldBe(1);
        analyzer.CastCount.ShouldBe(1);
        analyzer.CastsWithoutSpender.ShouldBe(0);
    }

    /// <summary>
    /// Leap Smash procs the same buff, so a window is only attributable to a press when a Focused
    /// Wrath cast sits within <see cref="FocusedWrathAnalyzer.CastAttributionMs"/> of the application.
    /// </summary>
    [Fact]
    public async Task Analyze_FocusedWrath_SeparatesAProcWindowFromACastWindow()
    {
        var (parser, _) = await ThunderCallWindowAnalyzerTests.AnalyzeAsync(
        [
            Apply(1_000, SelfBuff),
            Cast(1_200, TariqSpells.SkullCrusher.FSLID),
            Remove(1_200, SelfBuff),
            Cast(50_000, TariqSpells.FocusedWrath.FSLID),
            Apply(50_100, SelfBuff),
            Cast(50_400, TariqSpells.HammerStorm.FSLID),
            Remove(50_400, SelfBuff),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.WindowCount.ShouldBe(2);
        analyzer.WindowsFromProc.ShouldBe(1);
        analyzer.WindowsFromCast.ShouldBe(1);
        analyzer.Windows[0].FromCast.ShouldBeFalse();
        analyzer.Windows[1].FromCast.ShouldBeTrue();
        analyzer.SkullCrusherConsumptions.ShouldBe(1);
        analyzer.HammerStormConsumptions.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_FocusedWrath_CountsAWindowThatFellOffWithNoSpenderAsExpired()
    {
        var (parser, _) = await ThunderCallWindowAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, TariqSpells.FocusedWrath.FSLID),
            Apply(1_100, SelfBuff),
            Remove(1_100 + FocusedWrathAnalyzer.BuffDurationMs, SelfBuff),
            Cast(60_000, TariqSpells.SkullCrusher.FSLID),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;
        var window = analyzer.Windows.ShouldHaveSingleItem();

        window.Consumed.ShouldBeFalse();
        window.ConsumedBy.ShouldBeNull();

        analyzer.ExpiredWindows.ShouldBe(1);
        analyzer.CastsWithoutSpender.ShouldBe(1);
    }

    /// <summary>
    /// The buff removal and the cast that consumed it share a timestamp in the log, so a spender at
    /// the very end of the window still counts.
    /// </summary>
    [Fact]
    public async Task Analyze_FocusedWrath_CountsASpenderLoggedAtTheRemoval()
    {
        var (parser, _) = await ThunderCallWindowAnalyzerTests.AnalyzeAsync(
        [
            Apply(1_000, SelfBuff),
            Remove(5_000, SelfBuff),
            Cast(5_000, TariqSpells.CullingStrike.FSLID),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.Windows.ShouldHaveSingleItem().ConsumedBy.ShouldBe(TariqSpells.CullingStrike.FSLID);
        analyzer.CullingStrikeConsumptions.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_FocusedWrath_TracksStacksAgainstTheGameCap()
    {
        var (parser, _) = await ThunderCallWindowAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, TariqSpells.FocusedWrath.FSLID),
            Apply(1_100, SelfBuff),
            Stack(1_100, SelfBuff, stacks: 2),
            Stack(1_200, SelfBuff, stacks: 3),
            Cast(1_400, TariqSpells.HammerStorm.FSLID),
            Remove(1_400, SelfBuff),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.Windows.ShouldHaveSingleItem().MaxStacks.ShouldBe(3);
        analyzer.MaxStacksReached.ShouldBe(FocusedWrathAnalyzer.MaxStackLimit);
    }

    [Fact]
    public async Task Analyze_FocusedWrath_ClosesAWindowStillUpWhenThePullEnds()
    {
        var (parser, _) = await ThunderCallWindowAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, TariqSpells.FocusedWrath.FSLID),
            Apply(1_100, SelfBuff),
        ]);

        var analyzer = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem().Analyzer;
        var window = analyzer.Windows.ShouldHaveSingleItem();

        window.ClippedByPullEnd.ShouldBeTrue();
        analyzer.ExpiredWindows.ShouldBe(0);
        analyzer.CastsWithoutSpender.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_FocusedWrath_ExposesPerPullReadPaths()
    {
        var (parser, _) = await ThunderCallWindowAnalyzerTests.AnalyzeAsync(
        [
            Cast(1_000, TariqSpells.FocusedWrath.FSLID),
            Apply(1_100, SelfBuff),
            Cast(1_500, TariqSpells.SkullCrusher.FSLID),
            Remove(1_500, SelfBuff),
        ]);

        var entry = parser.FocusedWrathAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);

        pull.FocusedWrathAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).FocusedWrathAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static CastEvent Cast(int timestamp, int abilityId) =>
        FuryEconomyAnalyzerTests.CastWithoutResources(timestamp, abilityId);

    private static ApplyBuffEvent Apply(int timestamp, int abilityId) =>
        ThunderCallWindowAnalyzerTests.Apply(timestamp, abilityId);

    private static RemoveBuffEvent Remove(int timestamp, int abilityId) =>
        ThunderCallWindowAnalyzerTests.Remove(timestamp, abilityId);

    private static ApplyBuffStackEvent Stack(int timestamp, int abilityId, int stacks)
    {
        var stackEvent = FuryEconomyAnalyzerTests.Buff<ApplyBuffStackEvent>(timestamp, abilityId);
        stackEvent.Stack = stacks;
        return stackEvent;
    }
}
