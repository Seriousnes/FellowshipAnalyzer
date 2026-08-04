using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Elarion.Analysis;
using FellowshipAnalyzer.Heroes.Elarion.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using SpellAbility = FellowshipAnalyzer.Core.Events.Ability;

namespace FellowshipAnalyzer.Heroes.Elarion.Tests.Modules;

public sealed class ImpendingHeartseekerAnalyzerTests
{
    private const int PlayerId = 1;
    private const int EnemyId = 20;
    private const int BarrageCooldownMs = 20_000;

    private static readonly ReportFight Fight = SpanningFight(60_000);

    private static readonly ReportFight ShortFight = SpanningFight(8_000);

    [Fact]
    public async Task WithoutTheTalent_TheModuleIsInactive()
    {
        var parser = await AnalyzeAsync(
            ShortFight,
            Barrage(1_000),
            ApplyBuff(6_000));

        parser.ImpendingHeartseeker.ShouldBeNull();
        parser.GetModule<ImpendingHeartseekerAnalyzer>().ShouldBeNull();
    }

    [Fact]
    public async Task WithoutTheTalent_BarrageKeepsItsFullCooldown()
    {
        var parser = await AnalyzeAsync(
            ShortFight,
            Barrage(1_000),
            ApplyBuff(6_000));

        parser.SpellUsable.ShouldNotBeNull()
            .CooldownRemaining(Spells.HeartseekerBarrage.Id, 6_000)
            .ShouldBe(BarrageCooldownMs - 5_000);
    }

    [Fact]
    public async Task TheBuffResetsBarrage_AndRecoversTheRemainingRecharge()
    {
        var (parser, tracker) = await TrackAsync(
            ShortFight,
            Barrage(1_000),
            ApplyBuff(6_000));

        tracker.Resets.ShouldBe(1);
        tracker.LandedResets.ShouldBe(1);
        tracker.WastedResets.ShouldBe(0);
        tracker.RecoveredMs.ShouldBe(BarrageCooldownMs - 5_000);
        tracker.AverageRecoveredMs.ShouldBe(BarrageCooldownMs - 5_000);

        parser.SpellUsable.ShouldNotBeNull()
            .CooldownRemaining(Spells.HeartseekerBarrage.Id, 6_000)
            .ShouldBe(0);
    }

    [Fact]
    public async Task TheResetLetsBarrageBeRecastInsideItsCooldown()
    {
        var (parser, tracker) = await TrackAsync(
            ShortFight,
            Barrage(1_000),
            ApplyBuff(6_000),
            Barrage(7_000));

        tracker.Resets.ShouldBe(1);

        parser.SpellUsable.ShouldNotBeNull()
            .CooldownRemaining(Spells.HeartseekerBarrage.Id, 7_000)
            .ShouldBe(BarrageCooldownMs);
    }

    [Fact]
    public async Task ABuffWithBarrageAlreadyAvailable_RecoversNothing()
    {
        var (_, tracker) = await TrackAsync(ApplyBuff(6_000));

        tracker.Resets.ShouldBe(1);
        tracker.WastedResets.ShouldBe(1);
        tracker.LandedResets.ShouldBe(0);
        tracker.RecoveredMs.ShouldBe(0);
        tracker.AverageRecoveredMs.ShouldBe(0);
        tracker.WastedShare.ShouldBe(1d);
    }

    [Fact]
    public async Task EveryGrantOfTheBuff_CountsAsAReset()
    {
        var (_, tracker) = await TrackAsync(
            Barrage(1_000),
            ApplyBuff(6_000),
            Barrage(7_000),
            ApplyBuffStack(9_000, stack: 2),
            Barrage(10_000),
            RefreshBuff(12_000));

        tracker.Resets.ShouldBe(3);
        tracker.WastedResets.ShouldBe(0);
        tracker.RecoveredMs.ShouldBe(15_000 + 18_000 + 18_000);
    }

    [Fact]
    public async Task WithNoBuffAtAll_TheStatisticsCardIsWithheld()
    {
        var (_, tracker) = await TrackAsync(Barrage(1_000));

        tracker.Resets.ShouldBe(0);
        tracker.Statistic.ShouldBeNull();
    }

    [Fact]
    public async Task AResetMakesTheStatisticsCardAvailable()
    {
        var (_, tracker) = await TrackAsync(
            Barrage(1_000),
            ApplyBuff(6_000));

        tracker.Statistic.ShouldNotBeNull();
    }

    private static CombatantInfoEvent Talented() => new()
    {
        Timestamp = 0,
        SourceId = PlayerId,
        Talents = [new TalentInfo { Id = Talents.ImpendingHeartseeker.FSLID }],
    };

    private static CastEvent Barrage(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Activation = true,
        Ability = new SpellAbility
        {
            FSLID = Spells.HeartseekerBarrage.FSLID,
            Name = Spells.HeartseekerBarrage.Name,
        },
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

    private static RefreshBuffEvent RefreshBuff(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = BuffAbility(),
    };

    private static SpellAbility BuffAbility() => new()
    {
        FSLID = Spells.ImpendingHeartseeker.FSLID,
        Name = Spells.ImpendingHeartseeker.Name,
    };

    private static ReportFight SpanningFight(int endTime) =>
        new(Id: 0, Name: "Boss", EncounterId: 31, Kill: true,
            StartTime: 0, EndTime: endTime, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    private static Task<(ElarionCombatLogParser Parser, ImpendingHeartseekerAnalyzer Tracker)> TrackAsync(
        params Event[] events) => TrackAsync(Fight, events);

    private static async Task<(ElarionCombatLogParser Parser, ImpendingHeartseekerAnalyzer Tracker)> TrackAsync(
        ReportFight fight,
        params Event[] events)
    {
        var parser = await AnalyzeAsync(fight, [Talented(), .. events]);
        return (parser, parser.ImpendingHeartseeker.ShouldNotBeNull());
    }

    private static async Task<ElarionCombatLogParser> AnalyzeAsync(ReportFight fight, params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddElarionAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ElarionCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, fight);
        return parser;
    }
}
