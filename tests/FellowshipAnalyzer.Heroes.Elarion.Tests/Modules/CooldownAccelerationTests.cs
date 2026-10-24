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

public sealed class CooldownAccelerationTests
{
    private const int PlayerId = 1;
    private const int EnemyId = 20;
    private const int GraceCooldownMs = 120_000;
    private const int VolleyCooldownMs = 40_000;
    private const double SpiritOfHeroismHaste = 0.30;

    private const int GraceDungeonEnd = 41_000;
    private const int VolleyDungeonEnd = 22_000;

    [Fact]
    public async Task WithoutSkylitGrace_TheModuleIsInactive_AndGraceKeepsItsFullCooldown()
    {
        var parser = await AnalyzeAsync(GraceDungeonEnd, GraceCast(1_000), VolleyCast(11_000));

        parser.GetModule<SkylitGraceAnalyzer>().ShouldBeNull();
        ShouldBeNear(GraceRemaining(parser, 20_000), GraceCooldownMs - 19_000);
    }

    [Fact]
    public async Task SkylitGrace_AcceleratesGraceForTheVolleysEightSeconds()
    {
        var parser = await AnalyzeAsync(GraceDungeonEnd, Talented(), GraceCast(1_000), VolleyCast(11_000));

        var gained = (int)(SkylitGraceAnalyzer.VolleyDurationMs * SkylitGraceAnalyzer.Acceleration);
        ShouldBeNear(GraceRemaining(parser, 20_000), GraceCooldownMs - 19_000 - gained);
        parser.GetModule<SkylitGraceAnalyzer>().ShouldNotBeNull().ActiveMs.ShouldBe(SkylitGraceAnalyzer.VolleyDurationMs);
    }

    [Fact]
    public async Task SkylitGrace_LeavesStarfallVolleysOwnCooldownAlone()
    {
        var parser = await AnalyzeAsync(GraceDungeonEnd, Talented(), GraceCast(1_000), VolleyCast(11_000));

        ShouldBeNear(VolleyRemaining(parser, 21_000), VolleyCooldownMs - 10_000);
    }

    [Fact]
    public async Task SkylitGraceBuffRemoval_ClosesTheWindowEarly()
    {
        var parser = await AnalyzeAsync(
            GraceDungeonEnd,
            Talented(),
            GraceCast(1_000),
            VolleyCast(11_000),
            ApplyBuff(11_000, SkylitGraceBuff()),
            RemoveBuff(15_000, SkylitGraceBuff()));

        var gained = (int)(4_000 * SkylitGraceAnalyzer.Acceleration);
        ShouldBeNear(GraceRemaining(parser, 20_000), GraceCooldownMs - 19_000 - gained);
        parser.GetModule<SkylitGraceAnalyzer>().ShouldNotBeNull().ActiveMs.ShouldBe(4_000);
    }

    [Fact]
    public async Task EventHorizon_AddsTheHasteItWasAppliedAtToEveryCooldown()
    {
        var parser = await AnalyzeAsync(
            VolleyDungeonEnd,
            VolleyCast(1_000),
            ApplyBuff(5_000, SpiritOfHeroism()),
            ApplyBuff(5_000, EventHorizonBuff()),
            RemoveBuff(15_000, EventHorizonBuff()));

        var gained = (int)(10_000 * SpiritOfHeroismHaste);
        ShouldBeNear(VolleyRemaining(parser, 20_000), VolleyCooldownMs - 19_000 - gained);
    }

    [Fact]
    public async Task EventHorizon_FollowsAHasteChangeInsideTheBuff()
    {
        var parser = await AnalyzeAsync(
            VolleyDungeonEnd,
            VolleyCast(1_000),
            ApplyBuff(5_000, EventHorizonBuff()),
            ApplyBuff(9_000, SpiritOfHeroism()),
            RemoveBuff(15_000, EventHorizonBuff()));

        var gained = (int)(6_000 * SpiritOfHeroismHaste);
        ShouldBeNear(VolleyRemaining(parser, 20_000), VolleyCooldownMs - 19_000 - gained);
    }

    [Fact]
    public async Task EventHorizon_DropsItsTermOnRemoval()
    {
        var parser = await AnalyzeAsync(
            VolleyDungeonEnd,
            ApplyBuff(1_000, SpiritOfHeroism()),
            ApplyBuff(1_000, EventHorizonBuff()),
            RemoveBuff(2_000, EventHorizonBuff()),
            VolleyCast(3_000));

        parser.GetModule<EventHorizonAccelerationAnalyzer>().ShouldNotBeNull().CurrentAcceleration.ShouldBe(0.0);
        ShouldBeNear(VolleyRemaining(parser, 13_000), VolleyCooldownMs - 10_000);
    }

    [Fact]
    public async Task EventHorizonAndSkylitGrace_SumOnGrace()
    {
        var parser = await AnalyzeAsync(
            GraceDungeonEnd,
            Talented(),
            GraceCast(1_000),
            ApplyBuff(11_000, SpiritOfHeroism()),
            ApplyBuff(11_000, EventHorizonBuff()),
            VolleyCast(11_000),
            RemoveBuff(31_000, EventHorizonBuff()));

        var gained = (int)(SkylitGraceAnalyzer.VolleyDurationMs * SkylitGraceAnalyzer.Acceleration)
            + (int)(20_000 * SpiritOfHeroismHaste);
        ShouldBeNear(GraceRemaining(parser, 40_000), GraceCooldownMs - 39_000 - gained);
    }

    private static void ShouldBeNear(int actual, int expected) =>
        Math.Abs(actual - expected).ShouldBeLessThanOrEqualTo(2, $"{actual} vs {expected}");

    private static int GraceRemaining(ElarionCombatLogParser parser, int timestamp) =>
        parser.SpellUsable.ShouldNotBeNull().CooldownRemaining(Spells.SkystridersGrace.Id, timestamp);

    private static int VolleyRemaining(ElarionCombatLogParser parser, int timestamp) =>
        parser.SpellUsable.ShouldNotBeNull().CooldownRemaining(Spells.StarfallVolley.Id, timestamp);

    private static CombatantInfoEvent Talented() => new()
    {
        Timestamp = 0,
        SourceId = PlayerId,
        Talents = [new TalentInfo { Id = Talents.SkylitGrace.FSLID }],
    };

    private static CastEvent GraceCast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = -1,
        Activation = true,
        Ability = new SpellAbility { FSLID = Spells.SkystridersGrace.FSLID, Name = Spells.SkystridersGrace.Name },
    };

    private static CastEvent VolleyCast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Activation = true,
        Ability = new SpellAbility { FSLID = Spells.StarfallVolley.FSLID, Name = Spells.StarfallVolley.Name },
    };

    private static SpellAbility SkylitGraceBuff() => new()
    {
        FSLID = Spells.StarfallVolleyTalentBuffWhileActive.FSLID,
        Name = Spells.StarfallVolleyTalentBuffWhileActive.Name,
    };

    private static SpellAbility EventHorizonBuff() => new()
    {
        FSLID = Spells.EventHorizonBuff.FSLID,
        Name = Spells.EventHorizonBuff.Name,
    };

    private static SpellAbility SpiritOfHeroism() => new()
    {
        FSLID = Spells.SpiritOfHeroism.FSLID,
        Name = Spells.SpiritOfHeroism.Name,
    };

    private static ApplyBuffEvent ApplyBuff(int timestamp, SpellAbility ability) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = ability,
    };

    private static RemoveBuffEvent RemoveBuff(int timestamp, SpellAbility ability) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = ability,
    };

    private static async Task<ElarionCombatLogParser> AnalyzeAsync(int endTime, params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddElarionAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ElarionCombatLogParser>();
        var dungeon = new ReportDungeon(Id: 0, Name: "Boss", EncounterId: 31, Kill: true,
            StartTime: 0, EndTime: endTime, Difficulty: null, FriendlyPlayers: null, CompletionPercentage: null);
        await parser.Analyze([.. events], PlayerId, dungeon);
        return parser;
    }
}
