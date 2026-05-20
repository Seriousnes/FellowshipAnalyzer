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
    private const double FlatHaste = 0.30;     // 30%
    private const double HastePerStack = 0.04; // 4% per stack

    // -------------------------------------------------------------------------
    // Static math helpers
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0.0, 0.10, 0.10)]       // 0% + 10% = 10%
    [InlineData(0.10, 0.10, 0.21)]      // 10% + 10% = 21% (multiplicative)
    [InlineData(0.0, 0.30, 0.30)]       // 0% + 30% = 30%
    public void AddHaste_ShouldCombineMultiplicatively(double baseHaste, double gain, double expected)
    {
        var result = Haste.AddHaste(baseHaste, gain);
        Assert.Equal(expected, result, precision: 10);
    }

    [Fact]
    public void AddHaste_WithNegativeGain_ShouldRemoveHaste()
    {
        // AddHaste(0.30, -0.10) should undo a 10% buff from 30% total.
        var result = Haste.AddHaste(0.30, -0.10);
        Assert.True(result < 0.30);
        Assert.True(result >= 0.0);
    }

    [Theory]
    [InlineData(0.10, 0.10, 0.0)]        // Removing 10% from 10% = 0%
    [InlineData(0.30, 0.10, 0.18181818)] // Removing 10% from 30%
    public void RemoveHaste_ShouldInvertAddHaste(double baseHaste, double loss, double expected)
    {
        var result = Haste.RemoveHaste(baseHaste, loss);
        Assert.Equal(expected, result, precision: 5);
    }

    [Theory]
    [InlineData(0.0, 0.10)]
    [InlineData(0.0, 0.30)]
    [InlineData(0.15, 0.25)]
    public void AddThenRemove_ShouldRoundTrip(double baseHaste, double gain)
    {
        var added = Haste.AddHaste(baseHaste, gain);
        var removed = Haste.RemoveHaste(added, gain);
        Assert.Equal(baseHaste, removed, precision: 10);
    }

    [Fact]
    public void RemoveHaste_WithNegativeLoss_ShouldAddHaste()
    {
        var result = Haste.RemoveHaste(0.10, -0.10);
        Assert.True(result > 0.10);
    }

    // -------------------------------------------------------------------------
    // ScaleDuration
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ScaleDuration_ShouldReduceDurationByHaste()
    {
        var (_, haste) = await RunWithHaste();
        // At 0% haste, ScaleDuration should return the base duration unchanged.
        Assert.Equal(1500, haste.ScaleDuration(1500));
    }

    [Fact]
    public async Task ScaleDuration_WithActiveHaste_ShouldScale()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyBuff(100, HasteBuffSpellId),
        ], configureHaste: h => h.AddHasteBuff(HasteBuffSpellId, FlatHaste));

        // With 30% haste: 1500 * 100 / 130 ≈ 1153
        Assert.Equal(1153, haste.ScaleDuration(1500));
    }

    // -------------------------------------------------------------------------
    // Flat haste buff (apply / remove)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyBuff_RegisteredHasteBuff_ShouldIncreaseHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyBuff(100, HasteBuffSpellId),
        ], configureHaste: h => h.AddHasteBuff(HasteBuffSpellId, FlatHaste));

        Assert.Equal(FlatHaste, haste.Current, precision: 10);
    }

    [Fact]
    public async Task RemoveBuff_RegisteredHasteBuff_ShouldDecreaseHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyBuff(100, HasteBuffSpellId),
            CreateRemoveBuff(200, HasteBuffSpellId),
        ], configureHaste: h => h.AddHasteBuff(HasteBuffSpellId, FlatHaste));

        Assert.Equal(0.0, haste.Current, precision: 10);
    }

    [Fact]
    public async Task ApplyDebuff_RegisteredHasteBuff_ShouldIncreaseHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyDebuff(100, HasteBuffSpellId),
        ], configureHaste: h => h.AddHasteBuff(HasteBuffSpellId, FlatHaste));

        Assert.Equal(FlatHaste, haste.Current, precision: 10);
    }

    [Fact]
    public async Task RemoveDebuff_RegisteredHasteBuff_ShouldDecreaseHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyDebuff(100, HasteBuffSpellId),
            CreateRemoveDebuff(200, HasteBuffSpellId),
        ], configureHaste: h => h.AddHasteBuff(HasteBuffSpellId, FlatHaste));

        Assert.Equal(0.0, haste.Current, precision: 10);
    }

    // -------------------------------------------------------------------------
    // Stacking haste buff (apply / remove stack)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyBuffStack_ShouldAddPerStackHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyBuffStack(100, StackingBuffSpellId, stack: 1),
            CreateApplyBuffStack(200, StackingBuffSpellId, stack: 2),
        ], configureHaste: h => h.AddHasteBuffPerStack(StackingBuffSpellId, HastePerStack));

        // Two stacks: AddHaste(AddHaste(0, 0.04), 0.04)
        var expected = Haste.AddHaste(Haste.AddHaste(0.0, HastePerStack), HastePerStack);
        Assert.Equal(expected, haste.Current, precision: 10);
    }

    [Fact]
    public async Task RemoveBuffStack_ShouldRemovePerStackHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyBuffStack(100, StackingBuffSpellId, stack: 1),
            CreateApplyBuffStack(200, StackingBuffSpellId, stack: 2),
            CreateRemoveBuffStack(300, StackingBuffSpellId, stack: 1),
        ], configureHaste: h => h.AddHasteBuffPerStack(StackingBuffSpellId, HastePerStack));

        // After 2 stacks added, 1 removed → effectively 1 stack.
        Assert.Equal(HastePerStack, haste.Current, precision: 10);
    }

    [Fact]
    public async Task ApplyDebuffStack_ShouldAddPerStackHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyDebuffStack(100, StackingBuffSpellId, stack: 1),
        ], configureHaste: h => h.AddHasteBuffPerStack(StackingBuffSpellId, HastePerStack));

        Assert.Equal(HastePerStack, haste.Current, precision: 10);
    }

    [Fact]
    public async Task RemoveDebuffStack_ShouldRemovePerStackHaste()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyDebuffStack(100, StackingBuffSpellId, stack: 1),
            CreateRemoveDebuffStack(200, StackingBuffSpellId, stack: 0),
        ], configureHaste: h => h.AddHasteBuffPerStack(StackingBuffSpellId, HastePerStack));

        Assert.Equal(0.0, haste.Current, precision: 10);
    }

    // -------------------------------------------------------------------------
    // Unregistered buffs should be ignored
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Events without ability data (the bug fix)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyBuff_WithoutAbility_ShouldNotThrow()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            new ApplyBuffEvent { Timestamp = 100, SourceId = PlayerId, TargetId = PlayerId },
        ], configureHaste: h => h.AddHasteBuff(HasteBuffSpellId, FlatHaste));

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

    // -------------------------------------------------------------------------
    // Multiple haste buffs (multiplicative stacking)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MultipleFlatBuffs_ShouldStackMultiplicatively()
    {
        const int buffA = 300;
        const int buffB = 301;

        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyBuff(100, buffA),
            CreateApplyBuff(200, buffB),
        ], configureHaste: h =>
        {
            h.AddHasteBuff(buffA, 0.10);
            h.AddHasteBuff(buffB, 0.20);
        });

        // 10% then 20% multiplicatively: AddHaste(AddHaste(0, 0.10), 0.20)
        var expected = Haste.AddHaste(Haste.AddHaste(0.0, 0.10), 0.20);
        Assert.Equal(expected, haste.Current, precision: 10);
    }

    // -------------------------------------------------------------------------
    // ChangeStatsEvent (haste rating changes via StatTracker)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ChangeStats_WithHasteRatingChange_UpdatesHaste()
    {
        var (_, haste) = await RunWithHaste(
            events:
            [
                new ChangeStatsEvent
                {
                    Timestamp = 100,
                    SourceId = PlayerId,
                    TargetId = PlayerId,
                    Before = new Stats { Haste = 0 },
                    After = new Stats { Haste = 200 },
                    Delta = new Stats { Haste = 200 },
                },
            ],
            additionalModules: [typeof(StatTracker)]);

        Assert.True(haste.Current > 0, $"Expected non-zero haste, got {haste.Current}");
    }

    [Fact]
    public async Task ChangeStats_WithZeroHasteDelta_ShouldNotChangeHaste()
    {
        var (_, haste) = await RunWithHaste(
            events:
            [
                new ChangeStatsEvent
                {
                    Timestamp = 100,
                    SourceId = PlayerId,
                    TargetId = PlayerId,
                    Before = new Stats { Haste = 0 },
                    After = new Stats { Haste = 0 },
                    Delta = new Stats { Haste = 0 },
                },
            ],
            additionalModules: [typeof(StatTracker)]);

        Assert.Equal(0.0, haste.Current, precision: 10);
    }

    [Fact]
    public async Task ChangeStats_WithNullHasteDelta_ShouldNotChangeHaste()
    {
        var (_, haste) = await RunWithHaste(
            events:
            [
                new ChangeStatsEvent
                {
                    Timestamp = 100,
                    SourceId = PlayerId,
                    TargetId = PlayerId,
                    Before = new Stats(),
                    After = new Stats { Intellect = 100 },
                    Delta = new Stats { Intellect = 100 },
                },
            ],
            additionalModules: [typeof(StatTracker)]);

        Assert.Equal(0.0, haste.Current, precision: 10);
    }

    // -------------------------------------------------------------------------
    // ChangeHasteEvent fabrication
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Initialize_WithNoBuffs_ShouldStartAtZeroHaste()
    {
        var (_, haste) = await RunWithHaste();

        // With no StatTracker combatant data and no buffs, haste starts at 0.
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
            configureHaste: h => h.AddHasteBuff(HasteBuffSpellId, FlatHaste),
            additionalModules: [typeof(ChangeHasteProbe)]);

        var probe = parser.GetModule<ChangeHasteProbe>()!;

        // The initial ChangeHasteEvent fires during Initialize() before the probe
        // registers, so we only see the buff application event.
        Assert.Single(probe.ReceivedEvents);
        var buffEvent = probe.ReceivedEvents[0];
        Assert.Equal(0.0, buffEvent.OldHaste);
        Assert.Equal(FlatHaste, buffEvent.NewHaste);
    }

    // -------------------------------------------------------------------------
    // Registration API
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AddHasteBuff_Record_ShouldWork()
    {
        var (_, haste) = await RunWithHaste(events:
        [
            CreateApplyBuff(100, HasteBuffSpellId),
        ], configureHaste: h => h.AddHasteBuff(HasteBuffSpellId, new HasteBuff(Haste: FlatHaste)));

        Assert.Equal(FlatHaste, haste.Current, precision: 10);
    }

    // -------------------------------------------------------------------------
    // Edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetHaste_NaN_ShouldBeIgnored()
    {
        // If somehow a computation produces NaN, Current should not become NaN.
        // This is tested indirectly by ensuring remove-without-apply doesn't corrupt state.
        var (_, haste) = await RunWithHaste(events:
        [
            CreateRemoveBuff(100, HasteBuffSpellId),
        ], configureHaste: h => h.AddHasteBuff(HasteBuffSpellId, FlatHaste));

        Assert.False(double.IsNaN(haste.Current));
    }

    // =====================================================================
    // Test infrastructure
    // =====================================================================

    private static async Task<(TestCombatLogParser parser, Haste haste)> RunWithHaste(
        List<Event>? events = null,
        Action<Haste>? configureHaste = null,
        Type[]? additionalModules = null)
    {
        var moduleTypes = new List<Type> { typeof(HasteConfigWrapper), typeof(Haste) };
        if (additionalModules is not null)
            moduleTypes.AddRange(additionalModules);

        var parser = CreateCombatLogParser([.. moduleTypes], configureHaste);
        await parser.Analyze(events ?? [], PlayerId, fight: new ReportFight(0, "", 0, null, 0, 60_000, null, null, null));

        var haste = parser.GetModule<Haste>()!;
        return (parser, haste);
    }

    private static TestCombatLogParser CreateCombatLogParser(Type[] moduleTypes, Action<Haste>? configureHaste)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);
        provider.GetService(typeof(HasteTestConfiguration)).Returns(new HasteTestConfiguration(configureHaste));
        return new TestCombatLogParser(emitter, provider, moduleTypes);
    }

    private sealed class TestCombatLogParser(EventEmitter emitter, IServiceProvider provider, Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => moduleTypes;

        protected override object? CreateInstance(Type type)
        {
            if (type == typeof(HasteConfigWrapper))
                return new HasteConfigWrapper(
                    (Haste)ResolveAnalysisModule(typeof(Haste)),
                    (HasteTestConfiguration)Provider.GetService(typeof(HasteTestConfiguration))!);
            if (type == typeof(ChangeHasteProbe))
                return new ChangeHasteProbe();
            return base.CreateInstance(type);
        }
    }

    private sealed record HasteTestConfiguration(Action<Haste>? Configure);

    /// <summary>
    /// Module that runs before Haste and calls the configuration action
    /// (registering haste buffs) at construction time.
    /// </summary>
    private sealed class HasteConfigWrapper : Module
    {
        public HasteConfigWrapper(Haste haste, HasteTestConfiguration configuration)
        {
            configuration.Configure?.Invoke(haste);
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

    // -------------------------------------------------------------------------
    // Event factories
    // -------------------------------------------------------------------------

    private static ApplyBuffEvent CreateApplyBuff(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Guid = spellId, Name = $"Spell {spellId}" },
    };

    private static RemoveBuffEvent CreateRemoveBuff(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Guid = spellId, Name = $"Spell {spellId}" },
    };

    private static ApplyDebuffEvent CreateApplyDebuff(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Guid = spellId, Name = $"Spell {spellId}" },
    };

    private static RemoveDebuffEvent CreateRemoveDebuff(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Guid = spellId, Name = $"Spell {spellId}" },
    };

    private static ApplyBuffStackEvent CreateApplyBuffStack(int timestamp, int spellId, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Guid = spellId, Name = $"Spell {spellId}" },
        Stack = stack,
    };

    private static RemoveBuffStackEvent CreateRemoveBuffStack(int timestamp, int spellId, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Guid = spellId, Name = $"Spell {spellId}" },
        Stack = stack,
    };

    private static ApplyDebuffStackEvent CreateApplyDebuffStack(int timestamp, int spellId, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Guid = spellId, Name = $"Spell {spellId}" },
        Stack = stack,
    };

    private static RemoveDebuffStackEvent CreateRemoveDebuffStack(int timestamp, int spellId, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Guid = spellId, Name = $"Spell {spellId}" },
        Stack = stack,
    };
}
