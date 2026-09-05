using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

/// <summary>Exercises Resonant Fate over one pull, one close signal at a time.</summary>
public sealed class ResonantFateAnalyzerTests
{
    private const int PlayerId = 7;
    private const int TankId = 9;
    private const int OtherAllyId = 11;
    private const int EnemyId = 100;
    private const int PullEnd = 60_000;

    private static readonly List<ReportActor> Party =
    [
        new(PlayerId, "Aeona", "Player", "Aeona", null, null),
        new(TankId, "Xavian", "Player", "Xavian", null, null),
        new(OtherAllyId, "Rime", "Player", "Rime", null, null),
    ];

    [Fact]
    public async Task WithoutTheTalent_TheAnalyzerDoesNotRun()
    {
        var parser = await AnalyzeParser([Combatant(), CounterStacked(1_000, ResonantFateAnalyzer.MaximumStacks)]);

        parser.ResonantFateAnalyzers.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheCounterBelowItsMaximum_OpensNoHold()
    {
        var analyzer = await Analyze(CounterStacked(1_000, ResonantFateAnalyzer.MaximumStacks - 1));

        analyzer.Holds.ShouldBeEmpty();
        analyzer.HeldAtMaximumMs.ShouldBe(0);
    }

    [Fact]
    public async Task TheCounterReachingItsMaximum_HoldsUntilTheDamageReductionIsApplied()
    {
        var analyzer = await Analyze(
            CounterStacked(1_000, ResonantFateAnalyzer.MaximumStacks),
            DamageReductionApplied(TankId, 5_000),
            DamageReductionRemoved(TankId, 20_000));

        var hold = analyzer.Holds.ShouldHaveSingleItem();
        hold.ReachedAt.ShouldBe(1_000);
        hold.SpentAt.ShouldBe(5_000);
        hold.HeldAtMaximumMs.ShouldBe(4_000);
        hold.GrantedTo.ShouldBe(TankId);
        hold.GrantedToTank.ShouldBeTrue();
        hold.DamageReductionActiveMs.ShouldBe(15_000);
        analyzer.HoldsGranted.ShouldBe(1);
    }

    [Fact]
    public async Task AHoldSpentOnAnAllyWhoIsNotTheTank_IsRecordedAsSuch()
    {
        var analyzer = await Analyze(
            CounterStacked(1_000, ResonantFateAnalyzer.MaximumStacks),
            DamageReductionApplied(OtherAllyId, 5_000),
            DamageReductionRemoved(OtherAllyId, 20_000));

        var hold = analyzer.Holds.ShouldHaveSingleItem();
        hold.GrantedTo.ShouldBe(OtherAllyId);
        hold.GrantedToTank.ShouldBeFalse();
        analyzer.DamageReductionActiveMs.ShouldBe(0);
    }

    [Fact]
    public async Task ResonantFateExhausted_ClosesAHoldWithNoGrant()
    {
        var analyzer = await Analyze(
            CounterStacked(1_000, ResonantFateAnalyzer.MaximumStacks),
            Exhausted(6_000));

        var hold = analyzer.Holds.ShouldHaveSingleItem();
        hold.SpentAt.ShouldBe(6_000);
        hold.GrantedTo.ShouldBeNull();
        hold.GrantedToTank.ShouldBeFalse();
        hold.DamageReductionActiveMs.ShouldBe(0);
        analyzer.HoldsGranted.ShouldBe(0);
    }

    [Fact]
    public async Task TheCounterFallingBackBelowItsMaximum_ClosesTheHold()
    {
        var analyzer = await Analyze(
            CounterStacked(1_000, ResonantFateAnalyzer.MaximumStacks),
            CounterStackRemoved(4_000, ResonantFateAnalyzer.MaximumStacks - 1));

        analyzer.Holds.ShouldHaveSingleItem().HeldAtMaximumMs.ShouldBe(3_000);
    }

    [Fact]
    public async Task TheCounterBeingRemoved_ClosesTheHold()
    {
        var analyzer = await Analyze(
            CounterStacked(1_000, ResonantFateAnalyzer.MaximumStacks),
            CounterRemoved(4_000));

        analyzer.Holds.ShouldHaveSingleItem().SpentAt.ShouldBe(4_000);
    }

    [Fact]
    public async Task AHoldThePullEndsOn_RunsToThePullEnd()
    {
        var analyzer = await Analyze(CounterStacked(1_000, ResonantFateAnalyzer.MaximumStacks));

        var hold = analyzer.Holds.ShouldHaveSingleItem();
        hold.SpentAt.ShouldBe(PullEnd);
        hold.HeldAtMaximumMs.ShouldBe(PullEnd - 1_000);
    }

    [Fact]
    public async Task AFurtherStackAtTheMaximum_DoesNotOpenASecondHold()
    {
        var analyzer = await Analyze(
            CounterStacked(1_000, ResonantFateAnalyzer.MaximumStacks),
            CounterStacked(2_000, ResonantFateAnalyzer.MaximumStacks),
            Exhausted(5_000));

        analyzer.Holds.ShouldHaveSingleItem().ReachedAt.ShouldBe(1_000);
    }

    [Fact]
    public async Task TwoHolds_AreRecordedSeparately()
    {
        var analyzer = await Analyze(
            CounterStacked(1_000, ResonantFateAnalyzer.MaximumStacks),
            Exhausted(4_000),
            CounterStacked(30_000, ResonantFateAnalyzer.MaximumStacks),
            Exhausted(36_000));

        analyzer.Holds.Count.ShouldBe(2);
        analyzer.HeldAtMaximumMs.ShouldBe(3_000 + 6_000);
    }

    [Fact]
    public async Task TheDamageReductionUptimeOnTheTank_IsMeasuredAgainstThePull()
    {
        var analyzer = await Analyze(
            CounterStacked(1_000, ResonantFateAnalyzer.MaximumStacks),
            DamageReductionApplied(TankId, 5_000),
            DamageReductionRemoved(TankId, 20_000));

        analyzer.DamageReductionActiveMs.ShouldBe(15_000);
        analyzer.DamageReductionUptime.ShouldBe(15_000 / (double)PullEnd, 0.0001);
    }

    [Fact]
    public async Task ADamageReductionStillActiveAtThePullEnd_ClosesThere()
    {
        var analyzer = await Analyze(
            CounterStacked(1_000, ResonantFateAnalyzer.MaximumStacks),
            DamageReductionApplied(TankId, 50_000));

        analyzer.DamageReductionActiveMs.ShouldBe(PullEnd - 50_000);
    }

    [Fact]
    public async Task TheStackCapMatchesTheExport() =>
        ResonantFateAnalyzer.MaximumStacks.ShouldBe(100);

    [Fact]
    public async Task NoEvents_EverythingIsZero()
    {
        var analyzer = await Analyze();

        analyzer.Holds.ShouldBeEmpty();
        analyzer.HeldAtMaximumMs.ShouldBe(0);
        analyzer.HoldsGranted.ShouldBe(0);
        analyzer.DamageReductionActiveMs.ShouldBe(0);
        analyzer.DamageReductionUptime.ShouldBe(0d, 0.0001);
    }

    private static CombatantInfoEvent Combatant(params int[] talents) => new()
    {
        SourceId = PlayerId,
        Talents = [.. talents.Select(id => new TalentInfo { Id = id })],
    };

    private static ApplyBuffStackEvent CounterStacked(int timestamp, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = new Ability { Id = Spells.ResonantFate.FSLID },
    };

    private static RemoveBuffStackEvent CounterStackRemoved(int timestamp, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = new Ability { Id = Spells.ResonantFate.FSLID },
    };

    private static RemoveBuffEvent CounterRemoved(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.ResonantFate.FSLID },
    };

    private static ApplyBuffEvent Exhausted(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.ResonantFateExhausted.FSLID },
    };

    private static ApplyBuffEvent DamageReductionApplied(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { Id = Spells.ResonantFateDamageReduction.FSLID },
    };

    private static RemoveBuffEvent DamageReductionRemoved(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { Id = Spells.ResonantFateDamageReduction.FSLID },
    };

    private static DamageEvent TankStaggered(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = EnemyId,
        TargetId = TankId,
        Amount = 500,
        Ability = new Ability { Id = 1 },
        TargetResources = new ActorResources
        {
            HitPoints = 20_000,
            MaxHitPoints = 40_000,
            Resources = [new ClassResource { Type = ResourceTypes.Stagger, Amount = 5_000, Max = -100 }],
        },
    };

    private static async Task<ResonantFateAnalyzer> Analyze(params Event[] events)
    {
        var parser = await AnalyzeParser([Combatant(AeonaTalents.ResonantFate), TankStaggered(500), .. events]);

        return parser.ResonantFateAnalyzers
            .ShouldHaveSingleItem()
            .Analyzer
            .ShouldBeOfType<ResonantFateAnalyzer>();
    }

    private static async Task<AeonaCombatLogParser> AnalyzeParser(Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        parser.Actors = Party;
        await parser.Analyze([.. events], PlayerId, new ReportDungeon(0, "Boss", 1, true, 0, PullEnd, null, null, null));
        return parser;
    }
}
