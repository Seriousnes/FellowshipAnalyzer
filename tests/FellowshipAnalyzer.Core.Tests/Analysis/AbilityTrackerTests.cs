using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for <see cref="AbilityTracker"/>: the player's completed casts, damage, and healing are
/// tallied per ability across the whole fight and again scoped to the pull each event landed in.
/// </summary>
public sealed class AbilityTrackerTests
{
    private const int PlayerId = 7;
    private const int OtherPlayerId = 9;
    private const int SpellA = 4242;
    private const int SpellB = 4243;

    private static readonly ReportFight TestFight =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task Rows_TallyCastsDamageAndHealingPerAbility()
    {
        var tracker = await Analyze(
        [
            Cast(1000, SpellA),
            Damage(1100, SpellA, amount: 4000),
            Cast(2000, SpellA),
            Damage(2100, SpellA, amount: 6000, hitType: HitType.Crit),
            Cast(3000, SpellB),
            Heal(3100, SpellB, amount: 900, overheal: 100, hitType: HitType.Crit),
        ]);

        Assert.Equal(2, tracker.BySpell.Count);

        var spellA = tracker.For(SpellA)!;
        Assert.Equal("Spell A", spellA.Name);
        Assert.Equal(2, spellA.Casts);
        Assert.Equal(2, spellA.DamageHits);
        Assert.Equal(10_000, spellA.Damage);
        Assert.Equal(1, spellA.CriticalDamageHits);
        Assert.Equal(6000, spellA.CriticalDamage);
        Assert.Equal(5000, spellA.DamagePerCast);
        Assert.Equal(0.5, spellA.CriticalDamageRate);

        var spellB = tracker.For(SpellB)!;
        Assert.Equal(1, spellB.Casts);
        Assert.Equal(1, spellB.Heals);
        Assert.Equal(900, spellB.Healing);
        Assert.Equal(100, spellB.Overheal);
        Assert.Equal(1000, spellB.TotalHealing);
        Assert.Equal(0.1, spellB.OverhealShare);
        Assert.Equal(1, spellB.CriticalHeals);
        Assert.Equal(900, spellB.CriticalHealing);
    }

    [Fact]
    public async Task Casts_SkipActivationMarkers()
    {
        var tracker = await Analyze(
        [
            Cast(1000, SpellA, activation: true),
            Cast(2500, SpellA),
        ]);

        Assert.Equal(1, tracker.For(SpellA)!.Casts);
    }

    [Fact]
    public async Task GrievousCrits_CountAsCritical()
    {
        var tracker = await Analyze(
        [
            Damage(1000, SpellA, amount: 5000, hitType: HitType.GrievousCrit),
            Heal(2000, SpellB, amount: 800, overheal: 0, hitType: HitType.GrievousCrit),
        ]);

        Assert.Equal(1, tracker.For(SpellA)!.CriticalDamageHits);
        Assert.Equal(1, tracker.For(SpellB)!.CriticalHeals);
    }

    [Fact]
    public async Task Rows_IgnoreAnotherPlayersEvents()
    {
        var tracker = await Analyze(
        [
            Cast(1000, SpellA, sourceId: OtherPlayerId),
            Damage(1100, SpellA, amount: 9999, sourceId: OtherPlayerId),
        ]);

        Assert.Empty(tracker.BySpell);
        Assert.Null(tracker.For(SpellA));
    }

    [Fact]
    public async Task PullRows_ScopeToTheirPull()
    {
        var pull1 = MakePull(index: 0, id: 11, startTime: 1000, endTime: 5000);
        var pull2 = MakePull(index: 1, id: 12, startTime: 7000, endTime: 9000);

        var tracker = await Analyze(
        [
            Damage(500, SpellA, amount: 250),
            new PullStartEvent { Timestamp = 1000, Pull = pull1 },
            Cast(2000, SpellA),
            Damage(2500, SpellA, amount: 3000),
            new PullEndEvent { Timestamp = 5000, Pull = pull1 },
            Damage(6000, SpellA, amount: 1000),
            new PullStartEvent { Timestamp = 7000, Pull = pull2 },
            Damage(8000, SpellA, amount: 500),
            new PullEndEvent { Timestamp = 9000, Pull = pull2 },
        ]);

        var fightWide = tracker.For(SpellA)!;
        Assert.Equal(4, fightWide.DamageHits);
        Assert.Equal(4750, fightWide.Damage);
        Assert.Equal(1, fightWide.Casts);

        var inPull1 = Assert.Single(tracker.During(pull1));
        Assert.Equal(1, inPull1.Casts);
        Assert.Equal(1, inPull1.DamageHits);
        Assert.Equal(3000, inPull1.Damage);

        var inPull2 = tracker.For(SpellA, pull2)!;
        Assert.Equal(0, inPull2.Casts);
        Assert.Equal(500, inPull2.Damage);

        Assert.Null(tracker.For(SpellB, pull1));
    }

    [Fact]
    public async Task PullReads_AreEmptyForAPullWithoutPlayerEvents()
    {
        var pull = MakePull(index: 0, id: 11, startTime: 1000, endTime: 5000);
        var idlePull = MakePull(index: 1, id: 12, startTime: 7000, endTime: 9000);

        var tracker = await Analyze(
        [
            new PullStartEvent { Timestamp = 1000, Pull = pull },
            Damage(2000, SpellA, amount: 100),
            new PullEndEvent { Timestamp = 5000, Pull = pull },
            new PullStartEvent { Timestamp = 7000, Pull = idlePull },
            new PullEndEvent { Timestamp = 9000, Pull = idlePull },
        ]);

        Assert.Empty(tracker.During(idlePull));
        Assert.Null(tracker.For(SpellA, idlePull));
    }

    private static Pull MakePull(int index, int id, int startTime, int endTime) =>
        new(index, id, $"Pull {id}", startTime, endTime, PullKind.Single, IsBoss: true, Kill: true, TargetCount: 1);

    private static async Task<AbilityTracker> Analyze(List<Event> events)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);

        Type[] moduleTypes = [typeof(DebugAnnotations), typeof(Combatants), typeof(AbilityTracker)];

        var parser = new TestParser(emitter, provider, moduleTypes);
        await parser.Analyze(events, PlayerId, fight: TestFight);
        return parser.GetModule<AbilityTracker>()!;
    }

    private static CastEvent Cast(int timestamp, int spellId, int sourceId = PlayerId, bool activation = false) => new()
    {
        Timestamp = timestamp,
        SourceId = sourceId,
        TargetId = 11,
        Ability = MakeAbility(spellId),
        Activation = activation,
    };

    private static DamageEvent Damage(int timestamp, int spellId, long amount, int sourceId = PlayerId, HitType hitType = HitType.Normal) => new()
    {
        Timestamp = timestamp,
        SourceId = sourceId,
        TargetId = 11,
        Ability = MakeAbility(spellId),
        Amount = amount,
        HitType = hitType,
    };

    private static HealEvent Heal(int timestamp, int spellId, long amount, long overheal, HitType hitType = HitType.Normal) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = MakeAbility(spellId),
        Amount = amount,
        Overheal = overheal,
        HitType = hitType,
    };

    private static Ability MakeAbility(int spellId) => new()
    {
        FSLID = spellId,
        Name = spellId == SpellA ? "Spell A" : "Spell B",
    };

    private sealed class TestParser(EventEmitter emitter, IServiceProvider provider, Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => moduleTypes;
    }
}
