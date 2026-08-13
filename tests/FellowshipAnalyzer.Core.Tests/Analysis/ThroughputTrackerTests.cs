using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for <see cref="ThroughputTracker"/>: the selected player's damage, healing, and absorbed totals are
/// summed over the dungeon and converted into the per-second rates and shares that item statistics report a
/// contribution with.
/// </summary>
public sealed class ThroughputTrackerTests
{
    private const int PlayerId = 7;
    private const int OtherPlayerId = 9;

    private static readonly ReportDungeon TestDungeon =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    [Fact]
    public async Task Totals_SumTheSelectedPlayersOutput()
    {
        var tracker = await Analyze(
        [
            Damage(1000, amount: 4000),
            Damage(2000, amount: 2000),
            Heal(3000, amount: 900, overheal: 100),
            Absorbed(4000, amount: 500),
        ]);

        Assert.Equal(6000, tracker.TotalDamage);
        Assert.Equal(900, tracker.TotalHealing);
        Assert.Equal(100, tracker.TotalOverheal);
        Assert.Equal(500, tracker.TotalAbsorbed);
    }

    [Fact]
    public async Task Totals_IgnoreAnotherPlayersOutput()
    {
        var tracker = await Analyze(
        [
            Damage(1000, amount: 4000),
            Damage(2000, amount: 9999, sourceId: OtherPlayerId),
            Heal(3000, amount: 500, overheal: 0, sourceId: OtherPlayerId),
        ]);

        Assert.Equal(4000, tracker.TotalDamage);
        Assert.Equal(0, tracker.TotalHealing);
    }

    [Fact]
    public async Task Rates_DivideTheTotalByTheDungeonDuration()
    {
        var tracker = await Analyze([Damage(1000, amount: 60_000)]);

        Assert.Equal(60_000, tracker.DungeonDurationMs);
        Assert.Equal(1000, tracker.DamagePerSecond);
    }

    [Fact]
    public async Task Shares_ExpressAnAmountAsAFractionOfTheTotal()
    {
        var tracker = await Analyze(
        [
            Damage(1000, amount: 7500),
            Damage(2000, amount: 2500),
            Heal(3000, amount: 1000, overheal: 0),
        ]);

        Assert.Equal(0.25, tracker.ShareOfDamage(2500));
        Assert.Equal(1.0, tracker.ShareOfHealing(1000));
    }

    [Fact]
    public async Task Shares_AreZeroWhenNothingWasDone()
    {
        var tracker = await Analyze([]);

        Assert.Equal(0, tracker.ShareOfDamage(100));
        Assert.Equal(0, tracker.ShareOfHealing(100));
        Assert.Equal(0, tracker.DamagePerSecond);
    }

    private static async Task<ThroughputTracker> Analyze(List<Event> events)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);

        Type[] moduleTypes = [typeof(TestAbilities), typeof(DebugAnnotations), typeof(Combatants), typeof(ThroughputTracker)];

        var parser = new TestParser(emitter, provider, moduleTypes);
        await parser.Analyze(events, PlayerId, dungeon: TestDungeon);
        return parser.GetModule<ThroughputTracker>()!;
    }

    private static DamageEvent Damage(int timestamp, long amount, int sourceId = PlayerId) => new()
    {
        Timestamp = timestamp,
        SourceId = sourceId,
        TargetId = 11,
        Ability = new Ability { FSLID = 4242, Name = "Spell" },
        Amount = amount,
        HitType = HitType.Normal,
    };

    private static HealEvent Heal(int timestamp, long amount, long overheal, int sourceId = PlayerId) => new()
    {
        Timestamp = timestamp,
        SourceId = sourceId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = 4243, Name = "Spell" },
        Amount = amount,
        Overheal = overheal,
    };

    private static AbsorbedEvent Absorbed(int timestamp, long amount) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = 4244, Name = "Spell" },
        Amount = amount,
    };

    private sealed class TestParser(EventEmitter emitter, IServiceProvider provider, Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => moduleTypes;

        protected override object? CreateInstance(Type type)
        {
            if (type == typeof(TestAbilities)) return new TestAbilities();
            return base.CreateInstance(type);
        }
    }

    private sealed class TestAbilities : Abilities
    {
        public override IEnumerable<SpellbookAbility> Spellbook() => [];
    }
}
