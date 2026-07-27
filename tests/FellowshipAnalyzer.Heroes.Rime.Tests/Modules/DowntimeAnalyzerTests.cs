using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Rime.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Rime.Spells;

namespace FellowshipAnalyzer.Heroes.Rime.Tests.Modules;

public sealed class DowntimeAnalyzerTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 100;

    /// <summary>
    /// The global cooldown the core module fabricates for a hand-built log. Nothing here carries
    /// combatant info, so Haste is zero and every on-GCD ability lands on the unhasted baseline.
    /// Every expectation below is derived from this one value.
    /// </summary>
    private const int Gcd = 1_500;

    private const int FightEnd = 60_000;

    [Fact]
    public async Task FabricatedCastsLandOnTheUnhastedGlobalCooldown()
    {
        var events = new Event[] { Cast(1_000, Spells.ColdSnap) };
        var parser = await Analyze(Fight(FightEnd), events);

        var cast = events.OfType<CastEvent>().Single();
        cast.GlobalCooldown.ShouldNotBeNull();
        cast.GlobalCooldown!.Duration.ShouldBe(Gcd);
        parser.DowntimeAnalyzers.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task BackToBackCasts_LeaveNoGapAndAFullyActivePull()
    {
        var analyzer = await AnalyzeAsync(
            fightEndTime: 1_000 + (Gcd * 4),
            Cast(1_000, Spells.ColdSnap),
            Cast(1_000 + Gcd, Spells.IceComet),
            Cast(1_000 + (Gcd * 2), Spells.IceComet),
            Cast(1_000 + (Gcd * 3), Spells.IceComet));

        analyzer.MeasuredSpanMs.ShouldBe(Gcd * 4);
        analyzer.ActiveMs.ShouldBe(Gcd * 4);
        analyzer.DowntimeMs.ShouldBe(0);
        analyzer.ActiveRatio.ShouldBe(1);
        analyzer.GapCount.ShouldBe(0);
        analyzer.LongestGapMs.ShouldBe(0);
        analyzer.Gaps.ShouldBeEmpty();
    }

    [Fact]
    public async Task ASpacedPair_ProducesOneGapMeasuredFromTheGlobalCooldownExpiring()
    {
        var analyzer = await AnalyzeAsync(
            fightEndTime: 10_000 + Gcd,
            Cast(1_000, Spells.ColdSnap),
            Cast(10_000, Spells.IceComet));

        var gap = analyzer.Gaps.ShouldHaveSingleItem();
        gap.StartTimestamp.ShouldBe(1_000 + Gcd);
        gap.DurationMs.ShouldBe(10_000 - (1_000 + Gcd));

        analyzer.GapCount.ShouldBe(1);
        analyzer.LongestGapMs.ShouldBe(gap.DurationMs);
        analyzer.MeasuredSpanMs.ShouldBe(10_000 + Gcd - 1_000);
        analyzer.ActiveMs.ShouldBe(Gcd * 2);
        analyzer.DowntimeMs.ShouldBe(gap.DurationMs);
    }

    [Fact]
    public async Task AnIdleStretchBelowTheNoiseThreshold_IsAbsorbedAsActiveTime()
    {
        const int idleMs = DowntimeAnalyzer.MinimumGapMs - 1;

        var analyzer = await AnalyzeAsync(
            fightEndTime: 1_000 + Gcd + idleMs + Gcd,
            Cast(1_000, Spells.ColdSnap),
            Cast(1_000 + Gcd + idleMs, Spells.IceComet));

        analyzer.GapCount.ShouldBe(0);
        analyzer.DowntimeMs.ShouldBe(0);
        analyzer.ActiveMs.ShouldBe((Gcd * 2) + idleMs);
        analyzer.ActiveRatio.ShouldBe(1);
    }

    [Fact]
    public async Task AnIdleStretchAtTheNoiseThreshold_IsRecordedAsAGap()
    {
        const int idleMs = DowntimeAnalyzer.MinimumGapMs;

        var analyzer = await AnalyzeAsync(
            fightEndTime: 1_000 + Gcd + idleMs + Gcd,
            Cast(1_000, Spells.ColdSnap),
            Cast(1_000 + Gcd + idleMs, Spells.IceComet));

        analyzer.Gaps.ShouldHaveSingleItem().DurationMs.ShouldBe(idleMs);
        analyzer.DowntimeMs.ShouldBe(idleMs);
    }

    [Fact]
    public async Task AHardCast_IsActiveFromItsBeginCastRatherThanFromItsCompletion()
    {
        const int castStart = 2_000;
        const int castTime = 2_500;

        var analyzer = await AnalyzeAsync(
            fightEndTime: castStart + castTime + Gcd,
            Cast(1_000, Spells.ColdSnap),
            BeginCast(castStart, Spells.FrostBolt),
            Cast(castStart + castTime, Spells.FrostBolt));

        analyzer.GapCount.ShouldBe(0);
        analyzer.ActiveMs.ShouldBe(castStart + castTime + Gcd - 1_000);
        analyzer.ActiveRatio.ShouldBe(1);
    }

    [Fact]
    public async Task AChannelOutlastingItsGlobalCooldown_AbsorbsTheGapUpToTheChannelEnd()
    {
        const int channelStart = 1_000;
        const int channelEnd = 4_000;

        var analyzer = await AnalyzeAsync(
            fightEndTime: channelEnd + Gcd,
            Cast(channelStart, Spells.FreezingTorrent),
            BeginChannel(channelStart, Spells.FreezingTorrent),
            EndChannel(channelEnd, Spells.FreezingTorrent),
            Cast(channelEnd, Spells.IceComet));

        analyzer.GapCount.ShouldBe(0);
        analyzer.DowntimeMs.ShouldBe(0);
        analyzer.MeasuredSpanMs.ShouldBe(channelEnd + Gcd - channelStart);
        analyzer.ActiveMs.ShouldBe(analyzer.MeasuredSpanMs);
    }

    [Fact]
    public async Task AChannelWithNoLoggedEnd_FallsBackToItsGlobalCooldown()
    {
        const int channelStart = 1_000;

        var analyzer = await AnalyzeAsync(
            fightEndTime: 4_000 + Gcd,
            Cast(channelStart, Spells.FreezingTorrent),
            BeginChannel(channelStart, Spells.FreezingTorrent),
            Cast(4_000, Spells.IceComet));

        analyzer.Gaps.ShouldHaveSingleItem().DurationMs.ShouldBe(4_000 - (channelStart + Gcd));
    }

    [Fact]
    public async Task OffGlobalCooldownCasts_NeitherOpenWindowsNorSplitAGap()
    {
        var analyzer = await AnalyzeAsync(
            fightEndTime: 20_000 + Gcd,
            Cast(1_000, Spells.ColdSnap),
            Cast(6_000, Spells.IceDash),
            Cast(11_000, Spells.FrostWard),
            Cast(16_000, Spells.BrainFreeze),
            Cast(20_000, Spells.IceComet));

        var gap = analyzer.Gaps.ShouldHaveSingleItem();
        gap.StartTimestamp.ShouldBe(1_000 + Gcd);
        gap.DurationMs.ShouldBe(20_000 - (1_000 + Gcd));
        analyzer.GapCount.ShouldBe(1);
    }

    [Fact]
    public async Task ACastWithNoGlobalCooldownAtAll_LeavesNothingToMeasure()
    {
        var analyzer = await AnalyzeAsync(
            fightEndTime: FightEnd,
            Cast(1_000, Spells.IceDash),
            Cast(20_000, Spells.FrostWard));

        analyzer.MeasuredSpanMs.ShouldBe(0);
        analyzer.ActiveMs.ShouldBe(0);
        analyzer.DowntimeMs.ShouldBe(0);
        analyzer.ActiveRatio.ShouldBe(1);
        analyzer.GapCount.ShouldBe(0);
        analyzer.Gaps.ShouldBeEmpty();
    }

    [Fact]
    public async Task TrailingIdleAfterTheLastCast_CountsAsAGap()
    {
        var analyzer = await AnalyzeAsync(
            fightEndTime: FightEnd,
            Cast(1_000, Spells.ColdSnap));

        var gap = analyzer.Gaps.ShouldHaveSingleItem();
        gap.StartTimestamp.ShouldBe(1_000 + Gcd);
        gap.DurationMs.ShouldBe(FightEnd - (1_000 + Gcd));
        analyzer.MeasuredSpanMs.ShouldBe(FightEnd - 1_000);
        analyzer.ActiveMs.ShouldBe(Gcd);
        analyzer.DowntimeMs.ShouldBe(gap.DurationMs);
    }

    [Fact]
    public async Task ATrailingIdleBelowTheNoiseThreshold_IsAbsorbedRatherThanRecorded()
    {
        const int tailMs = DowntimeAnalyzer.MinimumGapMs - 1;
        const int fightEnd = 1_000 + Gcd + tailMs;

        var analyzer = await AnalyzeAsync(fightEnd, Cast(1_000, Spells.ColdSnap));

        analyzer.GapCount.ShouldBe(0);
        analyzer.DowntimeMs.ShouldBe(0);
        analyzer.ActiveMs.ShouldBe(analyzer.MeasuredSpanMs);
    }

    [Fact]
    public async Task GapsAreOrderedByDurationDescendingAndCappedWhileGapCountCoversThemAll()
    {
        var events = new List<Event>();
        var timestamp = 1_000;

        for (var index = 1; index <= DowntimeAnalyzer.TopGapLimit + 4; index++)
        {
            events.Add(Cast(timestamp, Spells.IceComet));
            timestamp += Gcd + (index * 1_000);
        }

        events.Add(Cast(timestamp, Spells.IceComet));

        var analyzer = await AnalyzeAsync(timestamp + Gcd, [.. events]);

        analyzer.GapCount.ShouldBe(DowntimeAnalyzer.TopGapLimit + 4);
        analyzer.Gaps.Count.ShouldBe(DowntimeAnalyzer.TopGapLimit);
        analyzer.Gaps.Select(gap => gap.DurationMs).ShouldBeInOrder(SortDirection.Descending);
        analyzer.Gaps[0].DurationMs.ShouldBe((DowntimeAnalyzer.TopGapLimit + 4) * 1_000);
        analyzer.LongestGapMs.ShouldBe(analyzer.Gaps[0].DurationMs);
        analyzer.DowntimeMs.ShouldBeGreaterThan(analyzer.Gaps.Sum(gap => gap.DurationMs));
    }

    [Fact]
    public async Task AnActivityWindowRunningPastThePullEnd_IsClampedToTheBoundary()
    {
        const int fightEnd = 2_000;

        var analyzer = await AnalyzeAsync(fightEnd, Cast(1_500, Spells.ColdSnap));

        analyzer.MeasuredSpanMs.ShouldBe(500);
        analyzer.ActiveMs.ShouldBe(500);
        analyzer.DowntimeMs.ShouldBe(0);
        analyzer.ActiveRatio.ShouldBe(1);
    }

    [Fact]
    public async Task PerPullReadPathsResolveToTheSameAnalyzerInstance()
    {
        var parser = await Analyze(Fight(FightEnd), [Cast(1_000, Spells.ColdSnap)]);

        var entry = parser.DowntimeAnalyzers.ShouldHaveSingleItem();
        entry.Pull.DowntimeAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(entry.Pull).DowntimeAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static CastEvent Cast(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
    };

    private static BeginCastEvent BeginCast(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
    };

    private static BeginChannelEvent BeginChannel(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
    };

    private static EndChannelEvent EndChannel(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
    };

    private static ReportFight Fight(int endTime) => new(
        Id: 0, Name: "Boss", EncounterId: 1, Kill: true,
        StartTime: 0, EndTime: endTime, Difficulty: null,
        FriendlyPlayers: null, FightPercentage: null);

    private static async Task<DowntimeAnalyzer> AnalyzeAsync(int fightEndTime, params Event[] events)
    {
        var parser = await Analyze(Fight(fightEndTime), events);
        return parser.DowntimeAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<RimeCombatLogParser> Analyze(ReportFight fight, params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddRimeAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<RimeCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, fight);
        return parser;
    }
}
