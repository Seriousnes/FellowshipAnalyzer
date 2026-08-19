using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for <see cref="StatTracker"/>'s flat percentage channel: the values that are added to a
/// rating-derived percentage rather than multiplied with it, including the auras a player already carries
/// when the dungeon starts and the magnitudes that are a function of the player's state at application.
/// </summary>
public sealed partial class StatTrackerPercentageTests
{
    private const int PlayerId = 7;
    private const int HasteBuffId = 100;
    private const int CritBuffId = 101;
    private const int SpiritScaledBuffId = 102;
    private const int StackingBuffId = 103;
    private const int RatingBuffId = 104;

    private static readonly ReportDungeon TestDungeon =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    [Fact]
    public async Task AuraActiveAtDungeonStart_ContributesItsPercentage()
    {
        var tracker = await Run(
            auras: [new Aura { Ability = HasteBuffId, Stacks = 1, Source = PlayerId }],
            configure: s => s.AddPercentageBuff(HasteBuffId, new StatPercentageBuff { Haste = 0.30 }));

        Assert.Equal(0.30, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task AuraActiveAtDungeonStart_ThenRemoved_DropsBackToZero()
    {
        var tracker = await Run(
            auras: [new Aura { Ability = HasteBuffId, Stacks = 1, Source = PlayerId }],
            events: [RemoveBuff(5000, HasteBuffId)],
            configure: s => s.AddPercentageBuff(HasteBuffId, new StatPercentageBuff { Haste = 0.30 }));

        Assert.Equal(0.0, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task AuraActiveAtDungeonStart_ThenReapplied_IsNotCountedTwice()
    {
        var tracker = await Run(
            auras: [new Aura { Ability = HasteBuffId, Stacks = 1, Source = PlayerId }],
            events: [ApplyBuff(5000, HasteBuffId)],
            configure: s => s.AddPercentageBuff(HasteBuffId, new StatPercentageBuff { Haste = 0.30 }));

        Assert.Equal(0.30, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task FlatCrit_AddsOnTopOfTheBaseChanceAndTheRating()
    {
        var tracker = await Run(
            events: [ApplyBuff(1000, RatingBuffId), ApplyBuff(2000, CritBuffId)],
            configure: s =>
            {
                s.Add(RatingBuffId, new StatBuff { Crit = 150.0 });
                s.AddPercentageBuff(CritBuffId, new StatPercentageBuff { Crit = 0.04 });
            });

        Assert.Equal(
            StatTracker.BaseCritChance + StatTracker.RatingToPercentage(150) + 0.04,
            tracker.CurrentCritPercentage,
            precision: 10);
    }

    [Fact]
    public async Task FlatExpertiseAndSpirit_AddToTheirOwnPercentages()
    {
        var tracker = await Run(
            events: [ApplyBuff(1000, HasteBuffId)],
            configure: s => s.AddPercentageBuff(HasteBuffId, new StatPercentageBuff { Expertise = 0.20, Spirit = 0.20 }));

        Assert.Equal(0.20, tracker.CurrentExpertisePercentage, precision: 10);
        Assert.Equal(0.20, tracker.CurrentSpiritPercentage, precision: 10);
        Assert.Equal(0.0, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task CritPowerMoveSpeedAndDamageReduction_StartFromTheirBases()
    {
        var tracker = await Run(
            events: [ApplyBuff(1000, HasteBuffId)],
            configure: s => s.AddPercentageBuff(HasteBuffId, new StatPercentageBuff
            {
                CritPower = 0.20,
                MoveSpeed = 0.15,
                DamageReduction = 0.05,
            }));

        Assert.Equal(StatTracker.BaseCritPower + 0.20, tracker.CurrentCritPower, precision: 10);
        Assert.Equal(1.15, tracker.CurrentMoveSpeed, precision: 10);
        Assert.Equal(0.05, tracker.CurrentDamageReduction, precision: 10);
    }

    [Fact]
    public async Task DamageReduction_IsCappedBelowTotalImmunity()
    {
        var tracker = await Run(
            events: [ApplyBuff(1000, HasteBuffId)],
            configure: s => s.AddPercentageBuff(HasteBuffId, new StatPercentageBuff { DamageReduction = 2.0 }));

        Assert.Equal(0.99, tracker.CurrentDamageReduction, precision: 10);
    }

    [Fact]
    public async Task MagnitudeThatReadsTheTrigger_ScalesWithResourcesAtApplication()
    {
        var tracker = await Run(
            events: [SpiritApplyBuff(1000, SpiritScaledBuffId, spirit: 40, maxSpirit: 100)],
            configure: s => s.AddPercentageBuff(SpiritScaledBuffId, new StatPercentageBuff
            {
                Haste = new BuffVal((Func<StatBuffContext, double>)(ctx => Math.Min(ctx.ResourceFraction(ResourceTypes.Spirit), 0.5) / 0.01 * 0.002)),
            }));

        Assert.Equal(0.08, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task MagnitudeThatReadsTheTrigger_IsReversedByTheValueItWasAppliedWith()
    {
        var tracker = await Run(
            events:
            [
                SpiritApplyBuff(1000, SpiritScaledBuffId, spirit: 40, maxSpirit: 100),
                SpiritRemoveBuff(2000, SpiritScaledBuffId, spirit: 5, maxSpirit: 100),
            ],
            configure: s => s.AddPercentageBuff(SpiritScaledBuffId, new StatPercentageBuff
            {
                Haste = new BuffVal((Func<StatBuffContext, double>)(ctx => Math.Min(ctx.ResourceFraction(ResourceTypes.Spirit), 0.5) / 0.01 * 0.002)),
            }));

        Assert.Equal(0.0, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task PerStackPercentage_TracksTheReportedStackCount()
    {
        var tracker = await Run(
            events:
            [
                ApplyBuff(1000, StackingBuffId),
                ApplyBuffStack(2000, StackingBuffId, stack: 3),
            ],
            configure: s => s.AddPercentageBuff(StackingBuffId, new StatPercentageBuff { Haste = 0.02, PerStack = true }));

        Assert.Equal(0.06, tracker.CurrentHastePercentage, precision: 10);
    }

    [Fact]
    public async Task PercentageChange_FabricatesAChangeStatsEvent()
    {
        var (tracker, parser) = await RunWithParser(
            events: [ApplyBuff(1000, HasteBuffId)],
            configure: s => s.AddPercentageBuff(HasteBuffId, new StatPercentageBuff { Haste = 0.30 }));

        var change = parser.Events.OfType<ChangeStatsEvent>().Last(e => e is not ChangeHasteEvent);

        Assert.Equal(0.0, change.Before.AdditionalHaste);
        Assert.Equal(0.30, change.After.AdditionalHaste);
        Assert.Equal(0.30, change.Delta.AdditionalHaste);
        Assert.Equal(0.30, tracker.CurrentHastePercentage, precision: 10);
    }

    private static async Task<StatTracker> Run(
        List<Event>? events = null,
        List<Aura>? auras = null,
        Action<StatTracker>? configure = null)
    {
        var (tracker, _) = await RunWithParser(events, auras, configure);
        return tracker;
    }

    private static async Task<(StatTracker tracker, TestParser parser)> RunWithParser(
        List<Event>? events = null,
        List<Aura>? auras = null,
        Action<StatTracker>? configure = null)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);

        List<Event> stream =
        [
            new CombatantInfoEvent { Timestamp = 0, SourceId = PlayerId, Auras = auras ?? [] },
            new DungeonStartEvent { Timestamp = 0 },
            .. events ?? [],
        ];

        var parser = new TestParser(emitter, provider, [typeof(StatTracker), typeof(StatConfigWrapper)])
        {
            Configure = configure,
        };
        await parser.Analyze(stream, PlayerId, dungeon: TestDungeon);

        return (parser.GetModule<StatTracker>()!, parser);
    }

    private sealed class TestParser(EventEmitter emitter, IServiceProvider provider, Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        public Action<StatTracker>? Configure { get; init; }

        protected override Type[] GetModuleTypes() => moduleTypes;

        protected override object? CreateInstance(Type type)
        {
            if (type == typeof(StatConfigWrapper))
                return new StatConfigWrapper((StatTracker)ResolveAnalysisModule(typeof(StatTracker)), Configure);
            return base.CreateInstance(type);
        }
    }

    private sealed class StatConfigWrapper : Module
    {
        public StatConfigWrapper(StatTracker statTracker, Action<StatTracker>? configure) => configure?.Invoke(statTracker);
    }

    private static ApplyBuffEvent ApplyBuff(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
    };

    private static RemoveBuffEvent RemoveBuff(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
    };

    private static ApplyBuffStackEvent ApplyBuffStack(int timestamp, int spellId, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
        Stack = stack,
    };

    private static ApplyBuffEvent SpiritApplyBuff(int timestamp, int spellId, int spirit, int maxSpirit)
    {
        var e = ApplyBuff(timestamp, spellId);
        e.TargetResources = SpiritResources(spirit, maxSpirit);
        return e;
    }

    private static RemoveBuffEvent SpiritRemoveBuff(int timestamp, int spellId, int spirit, int maxSpirit)
    {
        var e = RemoveBuff(timestamp, spellId);
        e.TargetResources = SpiritResources(spirit, maxSpirit);
        return e;
    }

    private static ActorResources SpiritResources(int spirit, int maxSpirit) => new()
    {
        Resources = [new ClassResource { Type = ResourceTypes.Spirit, Amount = spirit, Max = maxSpirit }],
    };
}
