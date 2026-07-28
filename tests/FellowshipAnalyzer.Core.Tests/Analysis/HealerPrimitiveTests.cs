using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for the healer-side Core primitives: <see cref="HotTracker"/> attribution,
/// <see cref="HealingEfficiencyTracker"/> per-spell absolutes, <see cref="DispelTracker"/> tallies, and
/// <see cref="AuraWindowLedger"/>'s window bookkeeping.
/// </summary>
public sealed class HealerPrimitiveTests
{
    private const int PlayerId = 7;
    private const int TankId = 8;
    private const int AllyId = 9;
    private const int HotSpell = 1_001_445;
    private const int CostedSpell = 1075;

    private static readonly ReportFight TestFight =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task HotTracker_AttributesTicksToTheOpenAssignmentForThatTarget()
    {
        var tracker = await AnalyzeHots(
        [
            ApplyBuff(1_000, HotSpell, TankId),
            ApplyBuff(1_000, HotSpell, AllyId),
            Heal(2_000, HotSpell, TankId, 500, 100),
            Heal(2_000, HotSpell, AllyId, 300, 700),
            RemoveBuff(5_000, HotSpell, TankId),
        ]);

        var assignments = tracker.AssignmentsOf(HotSpell).ToList();

        assignments.Count.ShouldBe(2);
        assignments[0].Unit.ShouldBe(new UnitKey(TankId, 0));
        assignments[0].Effective.ShouldBe(500);
        assignments[0].Overheal.ShouldBe(100);
        assignments[0].End.ShouldBe(5_000);
        assignments[1].Effective.ShouldBe(300);
        assignments[1].OverhealShare.ShouldBe(0.7, 0.001);
        assignments[1].End.ShouldBeNull();
    }

    [Fact]
    public async Task HotTracker_ReapplyingWithoutARemovalStartsAFreshAssignment()
    {
        var tracker = await AnalyzeHots(
        [
            ApplyBuff(1_000, HotSpell, TankId),
            Heal(2_000, HotSpell, TankId, 100, 0),
            ApplyBuff(3_000, HotSpell, TankId),
            Heal(4_000, HotSpell, TankId, 900, 0),
        ]);

        var assignments = tracker.AssignmentsOf(HotSpell).ToList();

        assignments.Count.ShouldBe(2);
        assignments[0].Effective.ShouldBe(100);
        assignments[0].End.ShouldBe(3_000);
        assignments[1].Effective.ShouldBe(900);
    }

    [Fact]
    public async Task HotTracker_RecordsARefreshWithoutSplittingTheAssignment()
    {
        var tracker = await AnalyzeHots(
        [
            ApplyBuff(1_000, HotSpell, TankId),
            RefreshBuff(3_000, HotSpell, TankId),
            Heal(4_000, HotSpell, TankId, 400, 0),
        ]);

        var assignment = tracker.AssignmentsOf(HotSpell).ShouldHaveSingleItem();

        assignment.Refreshes.ShouldHaveSingleItem().ShouldBe(3_000);
        assignment.Effective.ShouldBe(400);
        assignment.Start.ShouldBe(1_000);
    }

    [Fact]
    public async Task HotTracker_LeavesUntrackedSpellsAlone()
    {
        var tracker = await AnalyzeHots(
        [
            ApplyBuff(1_000, 4242, TankId),
            Heal(2_000, 4242, TankId, 500, 0),
        ]);

        tracker.Assignments.ShouldBeEmpty();
        tracker.UnattributedTicks.ShouldBe(0);
    }

    [Fact]
    public async Task HotTracker_CountsATickWithNoAssignmentAsUnattributed()
    {
        var tracker = await AnalyzeHots([Heal(2_000, HotSpell, TankId, 500, 300)]);

        tracker.UnattributedTicks.ShouldBe(1);
        tracker.UnattributedHealing.ShouldBe(800);
    }

    [Fact]
    public async Task HealingEfficiency_ReportsEachSpellsOwnAbsolutes()
    {
        var tracker = await AnalyzeEfficiency(
        [
            Cast(1_000, CostedSpell),
            Heal(2_000, CostedSpell, TankId, 1_000, 200),
            Cast(3_000, CostedSpell),
            Heal(4_000, CostedSpell, AllyId, 500, 0),
        ]);

        var spell = tracker.For(CostedSpell).ShouldNotBeNull();

        spell.Casts.ShouldBe(2);
        spell.Heals.ShouldBe(2);
        spell.Effective.ShouldBe(1_500);
        spell.Overheal.ShouldBe(200);
        spell.ResourceSpent.ShouldBe(2 * TestEfficiencyTracker.CostPerCast);
        spell.HealingPerResource.ShouldBe(1_500 / (double)(2 * TestEfficiencyTracker.CostPerCast), 0.001);
        tracker.HealingPerResource.ShouldBe(spell.HealingPerResource, 0.001);
    }

    [Fact]
    public async Task HealingEfficiency_LeavesAFreeSpellsRateAtZeroRatherThanRankingIt()
    {
        var tracker = await AnalyzeEfficiency(
        [
            Cast(1_000, 4242),
            Heal(2_000, 4242, TankId, 5_000, 0),
        ]);

        var spell = tracker.For(4242).ShouldNotBeNull();

        spell.ResourceSpent.ShouldBe(0);
        spell.HealingPerResource.ShouldBe(0);
        spell.Effective.ShouldBe(5_000);
    }

    [Fact]
    public async Task DispelTracker_TalliesPerAbilityPerAuraAndPerTarget()
    {
        var tracker = await AnalyzeDispels(
        [
            Dispel(1_000, 1054, TankId, removed: 2_938),
            Dispel(2_000, 1054, AllyId, removed: 2_938),
            Dispel(3_000, 187, TankId, removed: 2_949),
        ]);

        tracker.TotalDispels.ShouldBe(3);
        tracker.DispelsWith(1054).ShouldBe(2);
        tracker.ByRemovedAura[2_938].ShouldBe(2);
        tracker.ByTarget[new UnitKey(TankId, 0)].ShouldBe(2);
        tracker.BySpell[187].ShouldBe(1);
        tracker.Between(1_500, 3_500).Count().ShouldBe(2);
    }

    [Fact]
    public void AuraWindowLedger_ClosesAnOpenWindowAtTheLastObservation()
    {
        var ledger = new AuraWindowLedger();
        var target = ApplyBuff(1_000, HotSpell, TankId);

        ledger.Open(target, 1_000);
        ledger.Observe(target, 4_000);

        var windows = ledger.Build()[new UnitKey(TankId, 0)];

        windows.ShouldHaveSingleItem().ShouldBe(new AuraWindow(1_000, 4_000));
    }

    [Fact]
    public void AuraWindowLedger_CountsOverlappingWindowsOnce()
    {
        AuraWindowLedger.CoveredMs([new AuraWindow(0, 10_000), new AuraWindow(5_000, 15_000)]).ShouldBe(15_000);
        AuraWindowLedger.CoveredMs([new AuraWindow(0, 5_000), new AuraWindow(10_000, 15_000)]).ShouldBe(10_000);
        AuraWindowLedger.CoveredMs([]).ShouldBe(0);
    }

    private static async Task<TestHotTracker> AnalyzeHots(List<Event> events) =>
        await Analyze<TestHotTracker>(events, typeof(TestHotTracker));

    private static async Task<TestEfficiencyTracker> AnalyzeEfficiency(List<Event> events) =>
        await Analyze<TestEfficiencyTracker>(events, typeof(TestEfficiencyTracker));

    private static async Task<DispelTracker> AnalyzeDispels(List<Event> events) =>
        await Analyze<DispelTracker>(events, typeof(DispelTracker));

    private static async Task<T> Analyze<T>(List<Event> events, Type moduleType) where T : Module
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);

        Type[] moduleTypes = [typeof(TestAbilities), typeof(DebugAnnotations), typeof(Combatants), moduleType];

        var parser = new TestParser(emitter, provider, moduleTypes);
        await parser.Analyze(events, PlayerId, fight: TestFight);
        return parser.GetModule<T>()!;
    }

    private static ApplyBuffEvent ApplyBuff(int timestamp, int spellId, int targetId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = spellId, Name = "Aura" },
    };

    private static RefreshBuffEvent RefreshBuff(int timestamp, int spellId, int targetId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = spellId, Name = "Aura" },
    };

    private static RemoveBuffEvent RemoveBuff(int timestamp, int spellId, int targetId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = spellId, Name = "Aura" },
    };

    private static HealEvent Heal(int timestamp, int spellId, int targetId, long amount, long overheal) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = spellId, Name = "Heal" },
        Amount = amount,
        Overheal = overheal,
    };

    private static CastEvent Cast(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = TankId,
        Ability = new Ability { FSLID = spellId, Name = "Cast" },
    };

    private static DispelEvent Dispel(int timestamp, int spellId, int targetId, int removed) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = spellId, Name = "Dispel" },
        ExtraAbility = new Ability { FSLID = removed, Name = "Removed" },
    };

    private sealed class TestHotTracker : HotTracker
    {
        protected override bool TracksSpell(int spellId) => spellId == HotSpell;
    }

    private sealed class TestEfficiencyTracker : HealingEfficiencyTracker
    {
        public const int CostPerCast = 118;

        protected override ResourceTypes Resource => ResourceTypes.Mana;

        protected override int ResourceCostOf(CastEvent castEvent) =>
            castEvent.Ability.Id == CostedSpell ? CostPerCast : 0;
    }

    private sealed class TestParser(EventEmitter emitter, IServiceProvider provider, Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => moduleTypes;

        protected override object? CreateInstance(Type type)
        {
            if (type == typeof(TestAbilities)) return new TestAbilities();
            if (type == typeof(TestHotTracker)) return new TestHotTracker();
            if (type == typeof(TestEfficiencyTracker)) return new TestEfficiencyTracker();
            if (type == typeof(DispelTracker)) return new DispelTracker();
            return base.CreateInstance(type);
        }
    }

    private sealed class TestAbilities : Abilities
    {
        public override IEnumerable<SpellbookAbility> Spellbook() => [];
    }
}
