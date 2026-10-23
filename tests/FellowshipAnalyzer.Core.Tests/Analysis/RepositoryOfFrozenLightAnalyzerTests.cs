using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for <see cref="RepositoryOfFrozenLightAnalyzer"/>: a Frozen Light removal at or after 30 seconds from
/// its application takes 0.3 seconds off Repository of Frozen Light's cooldown for each stack still on the buff.
/// </summary>
public sealed class RepositoryOfFrozenLightAnalyzerTests
{
    private const int PlayerId = 7;
    private const int AllyId = 8;
    private const int CooldownSeconds = 120;

    private static readonly ReportDungeon TestDungeon =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    [Fact]
    public async Task ExpiryWithStacks_ReducesCooldownByStacksRemaining()
    {
        var analyzer = await Analyze(
        [
            Cast(1000),
            ApplyBuff(2500, AllyId),
            ApplyBuffStack(2500, AllyId, 15),
            RemoveBuffStack(5000, AllyId, 7),
            RemoveBuff(32_500, AllyId),
        ]);

        Assert.Equal(new CooldownReductionResult(2100, 2100), analyzer.CooldownReduction);
    }

    [Fact]
    public async Task ExpiryOnSeveralPlayers_ReducesCooldownForEachPlayersStacks()
    {
        var analyzer = await Analyze(
        [
            Cast(1000),
            ApplyBuff(2500, PlayerId),
            ApplyBuffStack(2500, PlayerId, 15),
            ApplyBuff(2500, AllyId),
            ApplyBuffStack(2500, AllyId, 15),
            RemoveBuffStack(5000, AllyId, 7),
            RemoveBuff(32_500, PlayerId),
            RemoveBuff(32_500, AllyId),
        ]);

        Assert.Equal(new CooldownReductionResult(6600, 6600), analyzer.CooldownReduction);
    }

    [Fact]
    public async Task RemovalBefore30Seconds_DoesNotReduceCooldown()
    {
        var analyzer = await Analyze(
        [
            Cast(1000),
            ApplyBuff(2500, AllyId),
            ApplyBuffStack(2500, AllyId, 15),
            RemoveBuff(32_499, AllyId),
        ]);

        Assert.Equal(new CooldownReductionResult(0, 0), analyzer.CooldownReduction);
    }

    [Fact]
    public async Task ExpiryWithZeroStacks_DoesNotReduceCooldown()
    {
        var analyzer = await Analyze(
        [
            Cast(1000),
            ApplyBuff(2500, AllyId),
            ApplyBuffStack(2500, AllyId, 15),
            RemoveBuffStack(6000, AllyId, 0),
            RemoveBuff(32_500, AllyId),
        ]);

        Assert.Equal(new CooldownReductionResult(0, 0), analyzer.CooldownReduction);
    }

    [Fact]
    public async Task ConsumedFinalStack_DoesNotReduceCooldown()
    {
        var analyzer = await Analyze(
        [
            Cast(1000),
            ApplyBuff(2500, AllyId),
            ApplyBuffStack(2500, AllyId, 15),
            RemoveBuffStack(21_500, AllyId, 1),
            RemoveBuff(23_500, AllyId),
        ]);

        Assert.Equal(new CooldownReductionResult(0, 0), analyzer.CooldownReduction);
    }

    [Fact]
    public void ConsumedFinalStack_DropsTheStackChangeAndZeroesTheRemovalStack()
    {
        var removal = RemoveBuff(23_500, AllyId);

        var normalized = new FrozenLightRemovalNormalizer().Normalize(
        [
            ApplyBuff(2500, AllyId),
            ApplyBuffStack(2500, AllyId, 15),
            RemoveBuffStack(21_500, AllyId, 1),
            removal,
        ], PlayerId);

        Assert.DoesNotContain(normalized, e => e is RemoveBuffStackEvent);
        Assert.Equal(0, removal.Stack);
    }

    [Fact]
    public async Task ExpiryWithThreeStacks_ReducesCooldownByNineTenthsOfASecond()
    {
        var analyzer = await Analyze(
        [
            Cast(1000),
            ApplyBuff(2500, AllyId),
            ApplyBuffStack(2500, AllyId, 15),
            RemoveBuffStack(10_000, AllyId, 3),
            RemoveBuff(32_500, AllyId),
        ]);

        Assert.Equal(new CooldownReductionResult(900, 900), analyzer.CooldownReduction);
    }

    [Fact]
    public async Task StackChangeToOneBeforeExpiryAt30Seconds_KeepsTheStack()
    {
        var analyzer = await Analyze(
        [
            Cast(1000),
            ApplyBuff(2500, AllyId),
            ApplyBuffStack(2500, AllyId, 15),
            RemoveBuffStack(30_500, AllyId, 1),
            RemoveBuff(32_500, AllyId),
        ]);

        Assert.Equal(new CooldownReductionResult(300, 300), analyzer.CooldownReduction);
    }

    private static async Task<RepositoryOfFrozenLightAnalyzer> Analyze(List<Event> events)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);

        Type[] moduleTypes =
        [
            typeof(TestAbilities),
            typeof(DebugAnnotations),
            typeof(StatTracker),
            typeof(Combatants),
            typeof(Haste),
            typeof(SpellUsable),
            typeof(RepositoryOfFrozenLightAnalyzer),
        ];

        var parser = new TestParser(emitter, provider, moduleTypes);
        await parser.Analyze(events, PlayerId, dungeon: TestDungeon);
        return parser.GetModule<RepositoryOfFrozenLightAnalyzer>()!;
    }

    private static CastEvent Cast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = Items.RepositoryOfFrozenLight.FSLID, Name = "Repository of Frozen Light" },
        Channel = new EndChannelEvent(),
    };

    private static ApplyBuffEvent ApplyBuff(int timestamp, int targetId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = FrozenLight(),
    };

    private static ApplyBuffStackEvent ApplyBuffStack(int timestamp, int targetId, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = FrozenLight(),
        Stack = stack,
    };

    private static RemoveBuffStackEvent RemoveBuffStack(int timestamp, int targetId, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = FrozenLight(),
        Stack = stack,
    };

    private static RemoveBuffEvent RemoveBuff(int timestamp, int targetId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = FrozenLight(),
    };

    private static Ability FrozenLight() =>
        new() { FSLID = Items.RepositoryOfFrozenLightBuff.FSLID, Name = "Frozen Light" };

    private sealed class TestParser(EventEmitter emitter, IServiceProvider provider, Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => moduleTypes;

        protected override Type[] GetNormalizerTypes() => [typeof(FrozenLightRemovalNormalizer)];

        protected override object? CreateInstance(Type type)
        {
            if (type == typeof(TestAbilities)) return new TestAbilities();
            if (type == typeof(FrozenLightRemovalNormalizer)) return new FrozenLightRemovalNormalizer();
            return base.CreateInstance(type);
        }
    }

    private sealed class TestAbilities : Abilities
    {
        public override IEnumerable<SpellbookAbility> Spellbook() =>
        [
            new SpellbookAbility
            {
                PrimarySpell = Items.RepositoryOfFrozenLight with { Cooldown = (double)CooldownSeconds },
                Category = SpellCategory.Cooldowns,
            },
        ];
    }
}
