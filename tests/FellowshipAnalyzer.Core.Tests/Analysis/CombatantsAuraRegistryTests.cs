using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for the <see cref="Combatants"/> all-units aura registry: composite <see cref="UnitKey"/>
/// separation of concurrent spawns, multi-instance window counting, death-driven close-out with a
/// closed-interval activity query, and source filtering. The enemy-facing queries over the same state
/// are covered by <see cref="EnemiesTests"/>.
/// </summary>
public sealed class CombatantsAuraRegistryTests
{
    private const int PlayerId = 25;
    private const int EffectId = 1002325;

    private static readonly ReportDungeon TestDungeon =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    [Fact]
    public async Task CompositeKey_SeparatesTwoSpawnsOfSameActorId()
    {
        var combatants = await Run(
            ApplyDebuff(1000, targetId: 100, targetInstance: 1, effectId: EffectId),
            ApplyDebuff(1000, targetId: 100, targetInstance: 2, effectId: EffectId));

        var first = combatants.GetCombatant(100, 1);
        var second = combatants.GetCombatant(100, 2);

        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        first.ShouldNotBeSameAs(second);

        combatants.AuraInstanceCount(100, 1, EffectId, 1000).ShouldBe(1);
        combatants.AuraInstanceCount(100, 2, EffectId, 1000).ShouldBe(1);
    }

    [Fact]
    public async Task AuraInstanceCount_CountsConcurrentOpenWindows()
    {
        var combatants = await Run(
            ApplyDebuff(1000, 100, 1, EffectId),
            ApplyDebuff(1100, 100, 1, EffectId),
            ApplyDebuff(1200, 100, 1, EffectId),
            ApplyDebuff(1300, 100, 1, EffectId),
            ApplyDebuff(1400, 100, 1, EffectId),
            ApplyDebuff(1500, 100, 1, EffectId));

        combatants.AuraInstanceCount(100, 1, EffectId, 1500).ShouldBe(6);
        combatants.GetCombatant(100, 1)!.GetAuraInstanceCount(EffectId, 1500).ShouldBe(6);
        combatants.AuraInstanceCount(100, 1, EffectId, 1250).ShouldBe(3);
    }

    [Fact]
    public async Task DeathCloseOut_StampsEndAndClosedIntervalQueryStillSeesWindow()
    {
        var combatants = await Run(
            ApplyDebuff(1000, 100, 1, EffectId),
            ApplyDebuff(1200, 100, 1, EffectId),
            Death(2000, targetId: 100, targetInstance: 1));

        var windows = combatants.GetCombatant(100, 1)!.Buffs.Where(b => b.Ability.Id == EffectId).ToList();
        windows.Count.ShouldBe(2);
        windows.ShouldAllBe(b => b.End == 2000);

        combatants.AuraInstanceCount(100, 1, EffectId, 2000).ShouldBe(2);
        combatants.AuraInstanceCount(100, 1, EffectId, 2001).ShouldBe(0);
    }

    [Fact]
    public async Task SourceIdFilter_RestrictsToApplyingSource()
    {
        const int otherSource = 99;

        var combatants = await Run(
            ApplyDebuff(1000, 100, 1, EffectId, sourceId: PlayerId),
            ApplyDebuff(1000, 100, 1, EffectId, sourceId: otherSource));

        combatants.AuraInstanceCount(100, 1, EffectId, 1000).ShouldBe(2);
        combatants.AuraInstanceCount(100, 1, EffectId, 1000, sourceId: PlayerId).ShouldBe(1);
        combatants.AuraInstanceCount(100, 1, EffectId, 1000, sourceId: otherSource).ShouldBe(1);
    }

    [Fact]
    public async Task PrepullAura_DerivesDebuffFromSourceAndKeepsSelectedWorking()
    {
        var info = new CombatantInfoEvent
        {
            Timestamp = 0,
            SourceId = PlayerId,
            Auras =
            [
                new Aura { Source = PlayerId, Ability = 500, Stacks = 1, Name = "Self Buff" },
                new Aura { Source = 1, Ability = 600, Stacks = 1, Name = "Applied By Other" },
            ],
        };

        var (parser, combatants) = await RunParser(info);

        var selected = parser.SelectedCombatant;
        selected.ShouldBeSameAs(combatants.GetCombatant(PlayerId, null));
        selected.GetBuff(500, forTimestamp: 0)!.IsDebuff.ShouldBeFalse();
        selected.GetBuff(600, forTimestamp: 0)!.IsDebuff.ShouldBeTrue();
    }

    [Fact]
    public async Task LoggedDuration_WithoutRemoval_EndsAtExpectedEnd()
    {
        var combatants = await Run(ApplyDebuff(10_000, 100, 1, EffectId, duration: 5_000));

        var window = combatants.GetCombatant(100, 1)!.Buffs.ShouldHaveSingleItem();
        window.ExpectedEnd.ShouldBe(15_000);
        window.End.ShouldBe(15_000);
        window.EndedByDungeonEnd.ShouldBeTrue();
        window.StackHistory[^1].Timestamp.ShouldBe(15_000);
    }

    [Fact]
    public async Task LoggedDuration_WithRemovalBeforeExpectedEnd_EndsAtRemoval()
    {
        var combatants = await Run(
            ApplyDebuff(10_000, 100, 1, EffectId, duration: 5_000),
            RemoveDebuff(12_000, 100, 1, EffectId));

        var window = combatants.GetCombatant(100, 1)!.Buffs.ShouldHaveSingleItem();
        window.End.ShouldBe(12_000);
        window.EndedByDungeonEnd.ShouldBeFalse();
    }

    [Fact]
    public async Task PermanentDuration_WithoutRemoval_EndsAtDungeonEnd()
    {
        var combatants = await Run(ApplyDebuff(10_000, 100, 1, EffectId, duration: BuffEvent.InfiniteDurationMs));

        var window = combatants.GetCombatant(100, 1)!.Buffs.ShouldHaveSingleItem();
        window.ExpectedEnd.ShouldBeNull();
        window.End.ShouldBe(60_000);
        window.EndedByDungeonEnd.ShouldBeTrue();
    }

    [Fact]
    public async Task NoLoggedDuration_WithoutRemoval_EndsAtDungeonEnd()
    {
        var combatants = await Run(ApplyDebuff(10_000, 100, 1, EffectId));

        var window = combatants.GetCombatant(100, 1)!.Buffs.ShouldHaveSingleItem();
        window.ExpectedEnd.ShouldBeNull();
        window.End.ShouldBe(60_000);
    }

    [Fact]
    public async Task ExpectedEndBeyondDungeonEnd_EndsAtDungeonEnd()
    {
        var combatants = await Run(ApplyDebuff(50_000, 100, 1, EffectId, duration: 30_000));

        combatants.GetCombatant(100, 1)!.Buffs.ShouldHaveSingleItem().End.ShouldBe(60_000);
    }

    [Fact]
    public async Task RefreshWithNewDuration_MovesExpectedEnd()
    {
        var combatants = await Run(
            ApplyDebuff(10_000, 100, 1, EffectId, duration: 6_000),
            RefreshDebuff(14_000, 100, 1, EffectId, duration: 6_000));

        var window = combatants.GetCombatant(100, 1)!.Buffs.ShouldHaveSingleItem();
        window.ExpectedEnd.ShouldBe(20_000);
        window.End.ShouldBe(20_000);
        window.RefreshHistory.ShouldBe([14_000]);
    }

    [Fact]
    public async Task RefreshWithoutDuration_KeepsExpectedEnd()
    {
        var combatants = await Run(
            ApplyDebuff(10_000, 100, 1, EffectId, duration: 6_000),
            RefreshDebuff(14_000, 100, 1, EffectId));

        combatants.GetCombatant(100, 1)!.Buffs.ShouldHaveSingleItem().End.ShouldBe(16_000);
    }

    [Fact]
    public async Task RefreshWithPermanentDuration_ClearsExpectedEnd()
    {
        var combatants = await Run(
            ApplyDebuff(10_000, 100, 1, EffectId, duration: 6_000),
            RefreshDebuff(14_000, 100, 1, EffectId, duration: BuffEvent.InfiniteDurationMs));

        var window = combatants.GetCombatant(100, 1)!.Buffs.ShouldHaveSingleItem();
        window.ExpectedEnd.ShouldBeNull();
        window.End.ShouldBe(60_000);
    }

    private static async Task<Combatants> Run(params Event[] events) =>
        (await RunParser(events)).Combatants;

    private static async Task<(CombatLogParser Parser, Combatants Combatants)> RunParser(params Event[] events)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);

        var parser = new TestParser(emitter, provider);
        await parser.Analyze([.. events], PlayerId, dungeon: TestDungeon);
        return (parser, parser.GetModule<Combatants>()!);
    }

    private static ApplyDebuffEvent ApplyDebuff(int timestamp, int targetId, int? targetInstance, int effectId, int sourceId = PlayerId, int? duration = null) => new()
    {
        Timestamp = timestamp,
        SourceId = sourceId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
        Duration = duration,
    };

    private static RefreshDebuffEvent RefreshDebuff(int timestamp, int targetId, int? targetInstance, int effectId, int? duration = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
        Duration = duration,
    };

    private static RemoveDebuffEvent RemoveDebuff(int timestamp, int targetId, int? targetInstance, int effectId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static DeathEvent Death(int timestamp, int targetId, int? targetInstance) => new()
    {
        Timestamp = timestamp,
        TargetId = targetId,
        TargetInstance = targetInstance,
    };

    private sealed class TestParser(EventEmitter emitter, IServiceProvider provider)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => [typeof(Combatants)];

        protected override Type[] GetNormalizerTypes() => [typeof(DungeonBookendNormalizer)];

        protected override object? CreateInstance(Type type) =>
            type == typeof(DungeonBookendNormalizer)
                ? new DungeonBookendNormalizer(CurrentParseContext)
                : base.CreateInstance(type);
    }
}
