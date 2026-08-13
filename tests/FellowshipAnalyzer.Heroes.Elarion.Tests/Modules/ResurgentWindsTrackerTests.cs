using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Elarion.Analysis;
using FellowshipAnalyzer.Heroes.Elarion.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using SpellAbility = FellowshipAnalyzer.Core.Events.Ability;

namespace FellowshipAnalyzer.Heroes.Elarion.Tests.Modules;

public sealed class ResurgentWindsTrackerTests
{
    private const int PlayerId = 1;
    private const int EnemyId = 20;

    private static readonly ReportDungeon Dungeon =
        new(Id: 0, Name: "Boss", EncounterId: 31, Kill: true,
            StartTime: 0, EndTime: 30_000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    [Fact]
    public async Task WithoutTheTalent_TheModuleIsInactive()
    {
        var parser = await AnalyzeAsync(
            ApplyBuff(1_000),
            InstantHighwind(2_000),
            RemoveBuff(2_001));

        parser.ResurgentWindsTracker.ShouldBeNull();
        parser.GetModule<ResurgentWindsTracker>().ShouldBeNull();
    }

    [Fact]
    public async Task WithTheTalent_TheModuleRuns()
    {
        var tracker = await Track(ApplyBuff(1_000));

        tracker.ProcsGained.ShouldBe(1);
    }

    [Fact]
    public async Task ProcRemoved_JustAfterAnInstantHighwind_IsConsumed()
    {
        var tracker = await Track(
            ApplyBuff(1_000),
            InstantHighwind(2_000),
            RemoveBuff(2_001));

        tracker.ProcsGained.ShouldBe(1);
        tracker.ProcsConsumed.ShouldBe(1);
        tracker.ProcsExpired.ShouldBe(0);
        tracker.InstantHighwindCasts.ShouldBe(1);
        tracker.ConsumedShare.ShouldBe(1d);
        tracker.ExpiredShare.ShouldBe(0d);
    }

    [Fact]
    public async Task ProcRemoved_SecondsFromAnyHighwind_IsExpired()
    {
        var tracker = await Track(
            ApplyBuff(1_000),
            InstantHighwind(2_000),
            RemoveBuff(17_000));

        tracker.ProcsGained.ShouldBe(1);
        tracker.ProcsConsumed.ShouldBe(0);
        tracker.ProcsExpired.ShouldBe(1);
    }

    [Fact]
    public async Task HardcastHighwind_DoesNotConsumeTheProcAndIsNotCountedAsInstant()
    {
        var tracker = await Track(
            ApplyBuff(1_000),
            HighwindActivation(2_000),
            HighwindBeginCast(2_000),
            RemoveBuff(2_001),
            HardcastCompletion(3_700));

        tracker.InstantHighwindCasts.ShouldBe(0);
        tracker.ProcsConsumed.ShouldBe(0);
        tracker.ProcsExpired.ShouldBe(1);
    }

    [Fact]
    public async Task HardcastCompletion_OnItsOwn_DoesNotConsumeTheProc()
    {
        var tracker = await Track(
            ApplyBuff(1_000),
            HardcastCompletion(2_000),
            RemoveBuff(2_001));

        tracker.InstantHighwindCasts.ShouldBe(0);
        tracker.ProcsConsumed.ShouldBe(0);
        tracker.ProcsExpired.ShouldBe(1);
    }

    [Fact]
    public async Task BeginCastArrivingAFewMillisecondsLate_StillMarksTheCastAsAHardcast()
    {
        var tracker = await Track(
            ApplyBuff(1_000),
            HighwindActivation(2_000),
            HighwindBeginCast(2_004),
            RemoveBuff(2_005));

        tracker.InstantHighwindCasts.ShouldBe(0);
        tracker.ProcsConsumed.ShouldBe(0);
        tracker.ProcsExpired.ShouldBe(1);
    }

    [Fact]
    public async Task OneInstantHighwind_CannotClaimTwoRemovals()
    {
        var tracker = await Track(
            ApplyBuff(1_000),
            ApplyBuffStack(1_500, stack: 2),
            InstantHighwind(2_000),
            RemoveBuffStack(2_020, stack: 1),
            RemoveBuff(2_060));

        tracker.ProcsGained.ShouldBe(2);
        tracker.ProcsConsumed.ShouldBe(1);
        tracker.ProcsExpired.ShouldBe(1);
    }

    [Fact]
    public async Task Procs_MakeTheStatisticsCardAvailable()
    {
        var tracker = await Track(
            ApplyBuff(1_000),
            InstantHighwind(2_000),
            RemoveBuff(2_001));

        tracker.Statistic.ShouldNotBeNull();
        tracker.StatisticCategory.ShouldBe(StatisticCategory.Talents);
    }

    [Fact]
    public async Task WithoutAProc_TheStatisticsCardIsWithheld()
    {
        var tracker = await Track(InstantHighwind(2_000));

        tracker.ProcsGained.ShouldBe(0);
        tracker.InstantHighwindCasts.ShouldBe(1);
        tracker.Statistic.ShouldBeNull();
    }

    private static CombatantInfoEvent Talented() => new()
    {
        Timestamp = 0,
        SourceId = PlayerId,
        Talents = [new TalentInfo { Id = Talents.ResurgentWinds.FSLID }],
    };

    private static CastEvent InstantHighwind(int timestamp) => HighwindActivation(timestamp);

    private static CastEvent HighwindActivation(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Activation = true,
        Ability = HighwindAbility(),
    };

    private static CastEvent HardcastCompletion(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Activation = false,
        Ability = HighwindAbility(),
    };

    private static BeginCastEvent HighwindBeginCast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = HighwindAbility(),
    };

    private static SpellAbility HighwindAbility() => new()
    {
        FSLID = Spells.HighwindArrow.FSLID,
        Name = Spells.HighwindArrow.Name,
    };

    private static ApplyBuffEvent ApplyBuff(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = BuffAbility(),
    };

    private static ApplyBuffStackEvent ApplyBuffStack(int timestamp, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = BuffAbility(),
    };

    private static RemoveBuffEvent RemoveBuff(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = BuffAbility(),
    };

    private static RemoveBuffStackEvent RemoveBuffStack(int timestamp, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = BuffAbility(),
    };

    private static SpellAbility BuffAbility() => new()
    {
        FSLID = Spells.ResurgentWindsBuff.FSLID,
        Name = Spells.ResurgentWindsBuff.Name,
    };

    private static async Task<ResurgentWindsTracker> Track(params Event[] events)
    {
        var parser = await AnalyzeAsync([Talented(), .. events]);
        return parser.ResurgentWindsTracker.ShouldNotBeNull();
    }

    private static async Task<ElarionCombatLogParser> AnalyzeAsync(params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddElarionAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ElarionCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, Dungeon);
        return parser;
    }
}
