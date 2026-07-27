using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Rime.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Rime.Spells;

namespace FellowshipAnalyzer.Heroes.Rime.Tests.Modules;

public sealed class MajorCooldownAnalyzerTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 100;
    private const int EnemyInstance = 1;

    private const int DefaultFightEnd = 60_000;
    private const int WindowStart = 1_000;
    private const int WindowEnd = 21_000;

    [Fact]
    public async Task HeldTime_SumsEveryGapBetweenComingOffCooldownAndTheNextCast()
    {
        var analyzer = await AnalyzeAsync(
            fightEndTime: 200_000,
            Cast(0, Spells.IceBlitz),
            Cast(70_000, Spells.IceBlitz),
            Cast(140_000, Spells.IceBlitz));

        analyzer.IceBlitz.Casts.ShouldBe(3);
        analyzer.IceBlitz.HeldMs.ShouldBe(20_000);
    }

    [Fact]
    public async Task HeldTime_ClosesAnIntervalStillOpenAtPullEnd()
    {
        var analyzer = await AnalyzeAsync(
            fightEndTime: 200_000,
            Cast(0, Spells.IceBlitz));

        analyzer.PullDurationMs.ShouldBe(200_000);
        analyzer.IceBlitz.Casts.ShouldBe(1);
        analyzer.IceBlitz.HeldMs.ShouldBe(140_000);
    }

    [Fact]
    public async Task HeldTime_IsNotChargedForTheTimeBeforeTheFirstCast()
    {
        var analyzer = await AnalyzeAsync(
            fightEndTime: 70_000,
            Cast(50_000, Spells.IceBlitz));

        analyzer.IceBlitz.HeldMs.ShouldBe(0);
    }

    [Fact]
    public async Task WrathOfWinter_IsSpiritGatedSoItRecordsNoHeldTime()
    {
        Spells.WrathOfWinter.Cooldown.ShouldBeNull();

        var analyzer = await AnalyzeAsync(
            fightEndTime: 200_000,
            Cast(0, Spells.WrathOfWinter));

        analyzer.UsageFor(RimeMajorCooldown.WrathOfWinter).HeldMs.ShouldBe(0);
    }

    [Fact]
    public async Task BuffWindow_CountsBurstingIceAndSpenderCastsInsideOnly()
    {
        var analyzer = await AnalyzeAsync(
            DefaultFightEnd,
            BuffApplied(WindowStart, Spells.IceBlitzBuff),
            Cast(2_000, Spells.BurstingIce),
            Cast(3_000, Spells.GlacialBlast),
            Cast(4_000, Spells.IceComet),
            Cast(5_000, Spells.FrostBolt),
            Cast(12_000, Spells.BurstingIce),
            BuffRemoved(WindowEnd, Spells.IceBlitzBuff),
            Cast(25_000, Spells.BurstingIce),
            Cast(26_000, Spells.GlacialBlast));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Major.ShouldBe(RimeMajorCooldown.IceBlitz);
        window.WindowStart.ShouldBe(WindowStart);
        window.WindowEnd.ShouldBe(WindowEnd);
        window.DurationMs.ShouldBe(20_000);
        window.BoundaryTruncated.ShouldBeFalse();
        window.BurstingIceCastsInside.ShouldBe(2);
        window.SpenderCastsInside.ShouldBe(2);
        window.FirstSpenderLatencyMs.ShouldBe(2_000);
    }

    [Fact]
    public async Task BuffUptime_IsMeasuredFromApplyToRemove()
    {
        var analyzer = await AnalyzeAsync(
            DefaultFightEnd,
            BuffApplied(WindowStart, Spells.IceBlitzBuff),
            BuffRemoved(WindowEnd, Spells.IceBlitzBuff));

        analyzer.IceBlitz.BuffUptimeMs.ShouldBe(20_000);
    }

    [Fact]
    public async Task AllFourMajors_OpenTheirOwnWindows()
    {
        var analyzer = await AnalyzeAsync(
            DefaultFightEnd,
            BuffApplied(1_000, Spells.IceBlitzBuff),
            BuffRemoved(2_000, Spells.IceBlitzBuff),
            BuffApplied(3_000, Spells.WinterBlessingSelfBuff),
            BuffRemoved(4_000, Spells.WinterBlessingSelfBuff),
            BuffApplied(5_000, Spells.FlightOfTheNavirAoeBuff),
            BuffRemoved(6_000, Spells.FlightOfTheNavirAoeBuff),
            BuffApplied(7_000, Spells.WrathOfWinterBuff),
            BuffRemoved(8_000, Spells.WrathOfWinterBuff));

        analyzer.Windows.Select(window => window.Major).ShouldBe(
        [
            RimeMajorCooldown.IceBlitz,
            RimeMajorCooldown.WintersBlessing,
            RimeMajorCooldown.FlightOfTheNavir,
            RimeMajorCooldown.WrathOfWinter,
        ]);

        analyzer.WindowsFor(RimeMajorCooldown.WintersBlessing).ShouldHaveSingleItem().WindowStart.ShouldBe(3_000);
        analyzer.WindowsFor(RimeMajorCooldown.FlightOfTheNavir).ShouldHaveSingleItem().WindowStart.ShouldBe(5_000);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task WrathOfWinter_OrbsAtActivationComeFromTheOpeningCastSnapshot(int orbs)
    {
        var analyzer = await AnalyzeAsync(
            DefaultFightEnd,
            Cast(WindowStart, Spells.WrathOfWinter, orbs),
            BuffApplied(WindowStart, Spells.WrathOfWinterBuff),
            BuffRemoved(WindowEnd, Spells.WrathOfWinterBuff));

        analyzer.Windows.ShouldHaveSingleItem().OrbsAtActivation.ShouldBe(orbs);
    }

    [Fact]
    public async Task WrathOfWinter_WithoutACastSnapshot_LeavesOrbsAtActivationNull()
    {
        var analyzer = await AnalyzeAsync(
            DefaultFightEnd,
            Cast(WindowStart, Spells.WrathOfWinter),
            BuffApplied(WindowStart, Spells.WrathOfWinterBuff),
            BuffRemoved(WindowEnd, Spells.WrathOfWinterBuff));

        analyzer.Windows.ShouldHaveSingleItem().OrbsAtActivation.ShouldBeNull();
    }

    [Fact]
    public async Task WrathOfWinter_OrbSnapshotIsConsumedByTheWindowItOpens()
    {
        var analyzer = await AnalyzeAsync(
            DefaultFightEnd,
            Cast(WindowStart, Spells.WrathOfWinter, orbs: 4),
            BuffApplied(WindowStart, Spells.WrathOfWinterBuff),
            BuffRemoved(WindowEnd, Spells.WrathOfWinterBuff),
            BuffApplied(30_000, Spells.WrathOfWinterBuff),
            BuffRemoved(50_000, Spells.WrathOfWinterBuff));

        analyzer.Windows.Count.ShouldBe(2);
        analyzer.Windows[0].OrbsAtActivation.ShouldBe(4);
        analyzer.Windows[1].OrbsAtActivation.ShouldBeNull();
    }

    [Fact]
    public async Task FirstSpenderLatency_MeasuresTheFirstSingleTargetSpenderInsideTheWindow()
    {
        var analyzer = await AnalyzeAsync(
            DefaultFightEnd,
            Cast(500, Spells.GlacialBlast),
            BuffApplied(WindowStart, Spells.WrathOfWinterBuff),
            Cast(2_000, Spells.IceComet),
            Cast(4_500, Spells.GlacialBlast),
            Cast(6_000, Spells.GlacialBlast),
            BuffRemoved(WindowEnd, Spells.WrathOfWinterBuff));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.FirstSpenderLatencyMs.ShouldBe(3_500);
        window.SpenderCastsInside.ShouldBe(3);
    }

    [Fact]
    public async Task FirstSpenderLatency_IsNullWhenNoSingleTargetSpenderLandsInside()
    {
        var analyzer = await AnalyzeAsync(
            DefaultFightEnd,
            BuffApplied(WindowStart, Spells.WrathOfWinterBuff),
            Cast(2_000, Spells.IceComet),
            BuffRemoved(WindowEnd, Spells.WrathOfWinterBuff));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.FirstSpenderLatencyMs.ShouldBeNull();
        window.SpenderCastsInside.ShouldBe(1);
    }

    [Fact]
    public async Task OvercapsInside_CountsOnlyGeneratorCastsAtTheOrbCap()
    {
        var analyzer = await AnalyzeAsync(
            DefaultFightEnd,
            BuffApplied(WindowStart, Spells.WrathOfWinterBuff),
            Cast(2_000, Spells.FrostBolt, orbs: MajorCooldownAnalyzer.MaxWinterOrbs),
            Cast(3_000, Spells.ColdSnap, orbs: MajorCooldownAnalyzer.MaxWinterOrbs),
            Cast(4_000, Spells.FreezingTorrent, orbs: 3),
            Cast(5_000, Spells.GlacialBlast, orbs: MajorCooldownAnalyzer.MaxWinterOrbs),
            BuffRemoved(WindowEnd, Spells.WrathOfWinterBuff),
            Cast(25_000, Spells.FrostBolt, orbs: MajorCooldownAnalyzer.MaxWinterOrbs));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.OvercapsInside.ShouldBe(2);
        window.SpenderCastsInside.ShouldBe(1);
    }

    [Fact]
    public async Task OvercapsInside_IsNullWhenNoGeneratorCastInsideCarriesASnapshot()
    {
        var analyzer = await AnalyzeAsync(
            DefaultFightEnd,
            BuffApplied(WindowStart, Spells.WrathOfWinterBuff),
            Cast(2_000, Spells.GlacialBlast, orbs: MajorCooldownAnalyzer.MaxWinterOrbs),
            Cast(3_000, Spells.FrostBolt),
            BuffRemoved(WindowEnd, Spells.WrathOfWinterBuff));

        analyzer.Windows.ShouldHaveSingleItem().OvercapsInside.ShouldBeNull();
    }

    [Fact]
    public async Task WindowStillOpenAtPullEnd_ClosesAtTheBoundaryAndIsMarkedTruncated()
    {
        var analyzer = await AnalyzeAsync(
            fightEndTime: 30_000,
            BuffApplied(20_000, Spells.IceBlitzBuff),
            Cast(21_000, Spells.BurstingIce));

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.WindowEnd.ShouldBe(30_000);
        window.BoundaryTruncated.ShouldBeTrue();
        window.BurstingIceCastsInside.ShouldBe(1);
        analyzer.IceBlitz.BuffUptimeMs.ShouldBe(10_000);
    }

    [Fact]
    public async Task RemoveWithoutALiveWindow_IsIgnored()
    {
        var analyzer = await AnalyzeAsync(
            DefaultFightEnd,
            BuffRemoved(500, Spells.IceBlitzBuff),
            BuffApplied(WindowStart, Spells.IceBlitzBuff),
            BuffRemoved(WindowEnd, Spells.IceBlitzBuff));

        analyzer.Windows.ShouldHaveSingleItem().WindowStart.ShouldBe(WindowStart);
        analyzer.IceBlitz.BuffUptimeMs.ShouldBe(20_000);
    }

    [Fact]
    public async Task ReapplyWhileAWindowIsOpen_ClosesTheOpenWindowAtTheReapply()
    {
        var analyzer = await AnalyzeAsync(
            DefaultFightEnd,
            BuffApplied(WindowStart, Spells.IceBlitzBuff),
            BuffApplied(5_000, Spells.IceBlitzBuff),
            BuffRemoved(WindowEnd, Spells.IceBlitzBuff));

        analyzer.Windows.Count.ShouldBe(2);
        analyzer.Windows[0].WindowEnd.ShouldBe(5_000);
        analyzer.Windows[1].WindowStart.ShouldBe(5_000);
        analyzer.Windows[1].WindowEnd.ShouldBe(WindowEnd);
        analyzer.IceBlitz.BuffUptimeMs.ShouldBe(20_000);
    }

    [Fact]
    public async Task PerPullReadPathsResolveToTheSameAnalyzerInstance()
    {
        var parser = await Analyze(
            Fight(DefaultFightEnd),
            BuffApplied(WindowStart, Spells.IceBlitzBuff),
            BuffRemoved(WindowEnd, Spells.IceBlitzBuff));

        var entry = parser.MajorCooldownAnalyzers.ShouldHaveSingleItem();
        entry.Pull.MajorCooldownAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(entry.Pull).MajorCooldownAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    /// <summary>
    /// A player cast, optionally carrying the Winter Orb snapshot the log would attach. Raw
    /// Fellowship resource values are scaled by 100, so the in-game orb count is stored multiplied.
    /// </summary>
    private static CastEvent Cast(int timestamp, Spell spell, int? orbs = null)
    {
        var cast = new CastEvent
        {
            Timestamp = timestamp,
            SourceId = PlayerId,
            TargetId = EnemyId,
            TargetInstance = EnemyInstance,
            Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        };

        if (orbs is not null)
        {
            cast.SourceResources = new ActorResources
            {
                Resources = [new ClassResource { Type = ResourceTypes.Tertiary, Amount = orbs.Value * 100, Max = 500 }],
            };
        }

        return cast;
    }

    private static ApplyBuffEvent BuffApplied(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
    };

    private static RemoveBuffEvent BuffRemoved(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
    };

    private static ReportFight Fight(int endTime) => new(
        Id: 0, Name: "Boss", EncounterId: 1, Kill: true,
        StartTime: 0, EndTime: endTime, Difficulty: null,
        FriendlyPlayers: null, FightPercentage: null);

    private static async Task<MajorCooldownAnalyzer> AnalyzeAsync(int fightEndTime, params Event[] events)
    {
        var parser = await Analyze(Fight(fightEndTime), events);
        return parser.MajorCooldownAnalyzers.ShouldHaveSingleItem().Analyzer;
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
