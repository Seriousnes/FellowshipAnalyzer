using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed partial class HasteTests
{
    private const int PlayerId = 7;
    private const int HasteBuffSpellId = 100;
    private const int StackingBuffSpellId = 200;
    private const double FlatHaste = 0.30;
    private const double HastePerStack = 0.04;

    [Fact]
    public async Task ScaleDuration_WithNoHaste_ShouldLeaveDurationUnchanged()
    {
        var (_, haste) = await RunWithHaste();
        Assert.Equal(1500, haste.ScaleDuration(1500));
    }

    [Fact]
    public async Task ScaleDuration_WithActiveHaste_ShouldScale()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyBuff(100, HasteBuffSpellId),
        ], configureStats: s => s.AddPercentageBuff(HasteBuffSpellId, new StatPercentageBuff { Haste = FlatHaste }));

        Assert.Equal(1153, haste.ScaleDuration(1500));
    }

    [Fact]
    public async Task ApplyBuff_RegisteredHasteBuff_ShouldIncreaseHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyBuff(100, HasteBuffSpellId),
        ], configureStats: s => s.AddPercentageBuff(HasteBuffSpellId, new StatPercentageBuff { Haste = FlatHaste }));

        Assert.Equal(FlatHaste, haste.Current, precision: 10);
    }

    [Fact]
    public async Task RemoveBuff_RegisteredHasteBuff_ShouldDecreaseHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyBuff(100, HasteBuffSpellId),
            CreateRemoveBuff(200, HasteBuffSpellId),
        ], configureStats: s => s.AddPercentageBuff(HasteBuffSpellId, new StatPercentageBuff { Haste = FlatHaste }));

        Assert.Equal(0.0, haste.Current, precision: 10);
    }

    [Fact]
    public async Task ApplyDebuff_RegisteredHasteBuff_ShouldIncreaseHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyDebuff(100, HasteBuffSpellId),
        ], configureStats: s => s.AddPercentageBuff(HasteBuffSpellId, new StatPercentageBuff { Haste = FlatHaste }));

        Assert.Equal(FlatHaste, haste.Current, precision: 10);
    }

    [Fact]
    public async Task RemoveDebuff_RegisteredHasteBuff_ShouldDecreaseHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyDebuff(100, HasteBuffSpellId),
            CreateRemoveDebuff(200, HasteBuffSpellId),
        ], configureStats: s => s.AddPercentageBuff(HasteBuffSpellId, new StatPercentageBuff { Haste = FlatHaste }));

        Assert.Equal(0.0, haste.Current, precision: 10);
    }

    [Fact]
    public async Task ApplyBuffStack_ShouldAddPerStackHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyBuffStack(100, StackingBuffSpellId, stack: 1),
            CreateApplyBuffStack(200, StackingBuffSpellId, stack: 2),
        ], configureStats: s => s.AddPercentageBuff(StackingBuffSpellId, new StatPercentageBuff { Haste = HastePerStack, PerStack = true }));

        Assert.Equal(HastePerStack * 2, haste.Current, precision: 10);
    }

    [Fact]
    public async Task RemoveBuffStack_ShouldRemovePerStackHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyBuffStack(100, StackingBuffSpellId, stack: 1),
            CreateApplyBuffStack(200, StackingBuffSpellId, stack: 2),
            CreateRemoveBuffStack(300, StackingBuffSpellId, stack: 1),
        ], configureStats: s => s.AddPercentageBuff(StackingBuffSpellId, new StatPercentageBuff { Haste = HastePerStack, PerStack = true }));

        Assert.Equal(HastePerStack, haste.Current, precision: 10);
    }

    [Fact]
    public async Task ApplyDebuffStack_ShouldAddPerStackHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyDebuffStack(100, StackingBuffSpellId, stack: 1),
        ], configureStats: s => s.AddPercentageBuff(StackingBuffSpellId, new StatPercentageBuff { Haste = HastePerStack, PerStack = true }));

        Assert.Equal(HastePerStack, haste.Current, precision: 10);
    }

    [Fact]
    public async Task RemoveDebuffStack_ShouldRemovePerStackHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyDebuffStack(100, StackingBuffSpellId, stack: 1),
            CreateRemoveDebuffStack(200, StackingBuffSpellId, stack: 0),
        ], configureStats: s => s.AddPercentageBuff(StackingBuffSpellId, new StatPercentageBuff { Haste = HastePerStack, PerStack = true }));

        Assert.Equal(0.0, haste.Current, precision: 10);
    }

    [Fact]
    public async Task ApplyBuffStack_OnAFlatBuff_ShouldNotMultiplyIt()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyBuff(100, HasteBuffSpellId),
            CreateApplyBuffStack(200, HasteBuffSpellId, stack: 2),
            CreateApplyBuffStack(300, HasteBuffSpellId, stack: 3),
        ], configureStats: s => s.AddPercentageBuff(HasteBuffSpellId, new StatPercentageBuff { Haste = FlatHaste }));

        Assert.Equal(FlatHaste, haste.Current, precision: 10);
    }

    [Fact]
    public async Task ApplyBuff_UnregisteredSpell_ShouldNotChangeHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyBuff(100, spellId: 999),
        ]);

        Assert.Equal(0.0, haste.Current, precision: 10);
    }

    [Fact]
    public async Task RemoveBuff_UnregisteredSpell_ShouldNotChangeHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateRemoveBuff(100, spellId: 999),
        ]);

        Assert.Equal(0.0, haste.Current, precision: 10);
    }

    [Fact]
    public async Task ApplyBuff_WithoutAbility_ShouldNotThrow()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            new ApplyBuffEvent { Timestamp = 100, SourceId = PlayerId, TargetId = PlayerId },
        ], configureStats: s => s.AddPercentageBuff(HasteBuffSpellId, new StatPercentageBuff { Haste = FlatHaste }));

        Assert.Equal(0.0, haste.Current, precision: 10);
    }

    [Fact]
    public async Task RemoveBuff_WithoutAbility_ShouldNotThrow()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            new RemoveBuffEvent { Timestamp = 100, SourceId = PlayerId, TargetId = PlayerId },
        ]);

        Assert.Equal(0.0, haste.Current, precision: 10);
    }

    [Fact]
    public async Task ApplyBuffStack_WithoutAbility_ShouldNotThrow()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            new ApplyBuffStackEvent { Timestamp = 100, SourceId = PlayerId, TargetId = PlayerId, Stack = 1 },
        ]);

        Assert.Equal(0.0, haste.Current, precision: 10);
    }

    [Fact]
    public async Task MultipleFlatBuffs_ShouldStackAdditively()
    {
        const int buffA = 300;
        const int buffB = 301;

        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyBuff(100, buffA),
            CreateApplyBuff(200, buffB),
        ], configureStats: s =>
        {
            s.AddPercentageBuff(buffA, new StatPercentageBuff { Haste = 0.10 });
            s.AddPercentageBuff(buffB, new StatPercentageBuff { Haste = 0.20 });
        });

        Assert.Equal(0.30, haste.Current, precision: 10);
    }

    [Fact]
    public async Task RatingHaste_AndFlatHaste_ShouldStackAdditively()
    {
        var (_, haste) = await RunWithHaste(
            events:
            [
                CreateApplyBuff(100, RatingBuffSpellId),
                CreateApplyBuff(200, HasteBuffSpellId),
            ],
            configureStats: s =>
            {
                s.Add(RatingBuffSpellId, new StatBuff { Haste = 200.0 });
                s.AddPercentageBuff(HasteBuffSpellId, new StatPercentageBuff { Haste = FlatHaste });
            });

        Assert.Equal(StatTracker.RatingToPercentage(200) + FlatHaste, haste.Current, precision: 10);
    }

    [Fact]
    public async Task RatingHasteChange_ShouldUpdateHaste()
    {
        var (_, haste) = await RunWithHaste(
            events: [CreateApplyBuff(100, RatingBuffSpellId)],
            configureStats: s => s.Add(RatingBuffSpellId, new StatBuff { Haste = 200.0 }));

        Assert.Equal(StatTracker.RatingToPercentage(200), haste.Current, precision: 10);
        Assert.True(haste.Current > 0);
    }

    [Fact]
    public async Task StatChangeThatLeavesHasteAlone_ShouldNotChangeHaste()
    {
        var (_, haste) = await RunWithHaste(
            events: [CreateApplyBuff(100, RatingBuffSpellId)],
            configureStats: s => s.Add(RatingBuffSpellId, new StatBuff { MainStat = 100.0 }));

        Assert.Equal(0.0, haste.Current, precision: 10);
    }

    [Fact]
    public async Task Initialize_WithNoBuffs_ShouldStartAtZeroHaste()
    {
        var (_, haste) = await RunWithHaste();

        Assert.Equal(0.0, haste.Current, precision: 10);
    }

    [Fact]
    public async Task ApplyBuff_ShouldFabricateChangeHasteEvent()
    {
        var (parser, _) = await RunWithHaste(
            events:
            [
                CreateApplyBuff(100, HasteBuffSpellId),
            ],
            configureStats: s => s.AddPercentageBuff(HasteBuffSpellId, new StatPercentageBuff { Haste = FlatHaste }),
            additionalModules: [typeof(ChangeHasteProbe)]);

        var probe = parser.GetModule<ChangeHasteProbe>()!;

        Assert.Single(probe.ReceivedEvents);
        var buffEvent = probe.ReceivedEvents[0];
        Assert.Equal(0.0, buffEvent.OldHaste);
        Assert.Equal(FlatHaste, buffEvent.NewHaste);
    }

    [Fact]
    public async Task DungeonStart_WithHasteAlreadyOnThePlayer_ShouldLeaveOldHasteNull()
    {
        var (parser, _) = await RunWithHaste(
            events:
            [
                new CombatantInfoEvent
                {
                    Timestamp = 0,
                    SourceId = PlayerId,
                    Auras = [new Aura { Ability = HasteBuffSpellId, Stacks = 1, Source = PlayerId }],
                },
                new DungeonStartEvent { Timestamp = 0 },
            ],
            configureStats: s => s.AddPercentageBuff(HasteBuffSpellId, new StatPercentageBuff { Haste = FlatHaste }),
            additionalModules: [typeof(ChangeHasteProbe)]);

        var probe = parser.GetModule<ChangeHasteProbe>()!;

        Assert.Single(probe.ReceivedEvents);
        var startEvent = probe.ReceivedEvents[0];
        Assert.Null(startEvent.OldHaste);
        Assert.Equal(FlatHaste, startEvent.NewHaste);
    }

    [Fact]
    public async Task StatChangeThatLeavesHasteAlone_ShouldNotFabricateChangeHasteEvent()
    {
        var (parser, _) = await RunWithHaste(
            events: [CreateApplyBuff(100, RatingBuffSpellId)],
            configureStats: s => s.Add(RatingBuffSpellId, new StatBuff { MainStat = 100.0 }),
            additionalModules: [typeof(ChangeHasteProbe)]);

        Assert.Empty(parser.GetModule<ChangeHasteProbe>()!.ReceivedEvents);
    }

    [Fact]
    public async Task RemoveBuff_ThatWasNeverApplied_ShouldLeaveHasteAtZero()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateRemoveBuff(100, HasteBuffSpellId),
        ], configureStats: s => s.AddPercentageBuff(HasteBuffSpellId, new StatPercentageBuff { Haste = FlatHaste }));

        Assert.False(double.IsNaN(haste.Current));
        Assert.Equal(0.0, haste.Current, precision: 10);
    }

    private const int RatingBuffSpellId = 400;

    private static async Task<(TestCombatLogParser parser, Haste haste)> RunWithHaste(
        List<Event>? events = null,
        Action<StatTracker>? configureStats = null,
        Type[]? additionalModules = null)
    {
        var moduleTypes = new List<Type> { typeof(StatTracker), typeof(StatConfigWrapper), typeof(Haste) };
        if (additionalModules is not null)
            moduleTypes.AddRange(additionalModules);

        var parser = CreateCombatLogParser([.. moduleTypes], configureStats);
        await parser.Analyze(events ?? [], PlayerId, dungeon: new ReportDungeon(0, "", 0, null, 0, 60_000, null, null, null));

        var haste = parser.GetModule<Haste>()!;
        return (parser, haste);
    }

    private static TestCombatLogParser CreateCombatLogParser(Type[] moduleTypes, Action<StatTracker>? configureStats)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);
        provider.GetService(typeof(StatTestConfiguration)).Returns(new StatTestConfiguration(configureStats));
        return new TestCombatLogParser(emitter, provider, moduleTypes);
    }

    private sealed class TestCombatLogParser(EventEmitter emitter, IServiceProvider provider, Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => moduleTypes;

        protected override object? CreateInstance(Type type)
        {
            if (type == typeof(StatConfigWrapper))
                return new StatConfigWrapper(
                    (StatTracker)ResolveAnalysisModule(typeof(StatTracker)),
                    (StatTestConfiguration)Provider.GetService(typeof(StatTestConfiguration))!);
            if (type == typeof(ChangeHasteProbe))
                return new ChangeHasteProbe();
            return base.CreateInstance(type);
        }
    }

    private sealed record StatTestConfiguration(Action<StatTracker>? Configure);

    /// <summary>
    /// Module that runs before Haste and calls the configuration action
    /// (registering stat buffs) at construction time.
    /// </summary>
    private sealed class StatConfigWrapper : Module
    {
        public StatConfigWrapper(StatTracker statTracker, StatTestConfiguration configuration)
        {
            configuration.Configure?.Invoke(statTracker);
        }
    }

    /// <summary>
    /// Probe that listens for fabricated <see cref="ChangeHasteEvent"/>s.
    /// </summary>
    private sealed partial class ChangeHasteProbe : Analyzer
    {
        public List<ChangeHasteEvent> ReceivedEvents { get; } = [];

        [On<ChangeHasteEvent>]
        private void OnChangeHaste(ChangeHasteEvent e)
        {
            ReceivedEvents.Add(e);
        }
    }

    private static ApplyBuffEvent CreateApplyBuff(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
    };

    private static RemoveBuffEvent CreateRemoveBuff(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
    };

    private static ApplyDebuffEvent CreateApplyDebuff(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
    };

    private static RemoveDebuffEvent CreateRemoveDebuff(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
    };

    private static ApplyBuffStackEvent CreateApplyBuffStack(int timestamp, int spellId, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
        Stack = stack,
    };

    private static RemoveBuffStackEvent CreateRemoveBuffStack(int timestamp, int spellId, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
        Stack = stack,
    };

    private static ApplyDebuffStackEvent CreateApplyDebuffStack(int timestamp, int spellId, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
        Stack = stack,
    };

    private static RemoveDebuffStackEvent CreateRemoveDebuffStack(int timestamp, int spellId, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
        Stack = stack,
    };
}
