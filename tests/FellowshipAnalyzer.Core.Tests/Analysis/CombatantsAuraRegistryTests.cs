using FellowshipAnalyzer.Core.Analysis;
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
/// closed-interval activity query, and source filtering.
/// </summary>
public sealed class CombatantsAuraRegistryTests
{
    private const int PlayerId = 25;
    private const int EffectId = 1002325;

    private static readonly ReportFight TestFight =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task CompositeKey_SeparatesTwoSpawnsOfSameActorId()
    {
        var combatants = await Run(
            ApplyDebuff(1000, targetId: 100, targetInstance: 1, effectId: EffectId),
            ApplyDebuff(1000, targetId: 100, targetInstance: 2, effectId: EffectId));

        var first = combatants.GetUnit(100, 1);
        var second = combatants.GetUnit(100, 2);

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
        combatants.GetUnit(100, 1)!.GetAuraInstanceCount(EffectId, 1500).ShouldBe(6);
        combatants.AuraInstanceCount(100, 1, EffectId, 1250).ShouldBe(3);
    }

    [Fact]
    public async Task CountEnemiesWithAura_CountsDistinctEnemies()
    {
        var combatants = await Run(
            ApplyDebuff(1000, 100, 1, EffectId),
            ApplyDebuff(1000, 101, 1, EffectId),
            ApplyDebuff(1000, 102, 1, EffectId),
            ApplyDebuff(1050, 100, 1, EffectId));

        combatants.CountEnemiesWithAura(EffectId, 1050).ShouldBe(3);
        combatants.EnemiesWithAura(EffectId, 1050).ShouldBe(
            [new UnitKey(100, 1), new UnitKey(101, 1), new UnitKey(102, 1)],
            ignoreOrder: true);
    }

    [Fact]
    public async Task DeathCloseOut_StampsEndAndClosedIntervalQueryStillSeesWindow()
    {
        var combatants = await Run(
            ApplyDebuff(1000, 100, 1, EffectId),
            ApplyDebuff(1200, 100, 1, EffectId),
            Death(2000, targetId: 100, targetInstance: 1));

        var windows = combatants.GetUnit(100, 1)!.Buffs.Where(b => b.Ability.Id == EffectId).ToList();
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
        combatants.CountEnemiesWithAura(EffectId, 1000, sourceId: PlayerId).ShouldBe(1);
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

        var combatants = await Run(info);

        combatants.Selected.ShouldBeSameAs(combatants.GetUnit(PlayerId, null));
        combatants.Selected.GetBuff(500, forTimestamp: 0)!.IsDebuff.ShouldBeFalse();
        combatants.Selected.GetBuff(600, forTimestamp: 0)!.IsDebuff.ShouldBeTrue();
    }

    private static async Task<Combatants> Run(params Event[] events)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);

        var parser = new TestParser(emitter, provider);
        await parser.Analyze([.. events], PlayerId, fight: TestFight);
        return parser.GetModule<Combatants>()!;
    }

    private static ApplyDebuffEvent ApplyDebuff(int timestamp, int targetId, int? targetInstance, int effectId, int sourceId = PlayerId) => new()
    {
        Timestamp = timestamp,
        SourceId = sourceId,
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

        protected override Type[] GetNormalizerTypes() => [];
    }
}
