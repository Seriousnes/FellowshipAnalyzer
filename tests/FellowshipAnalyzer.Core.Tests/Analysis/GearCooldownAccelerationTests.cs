using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.UI;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for the legendary "Strand of Eternity" Cooldown Recovery modifier: the selected
/// <see cref="Combatant"/> resolving the modifier of 1.1 from a legendary-quality item at construction, and
/// <see cref="SpellUsable"/> reading it through <see cref="StatTracker.CurrentCooldownRecovery"/>, where it
/// shares one multiplicative factor with Chronoshift and adds outside it with haste, and is independent of Ability
/// Cooldown Reduction.
/// </summary>
public sealed class GearCooldownAccelerationTests
{
    private const int PlayerId = 7;
    private const int SpellA = 101;
    private const int SpellB = 102;
    private const int CdSecondsA = 10;
    private const int CdSecondsB = 20;
    private const int BaseCdMsA = CdSecondsA * 1000;
    private const int BaseCdMsB = CdSecondsB * 1000;

    private const int EpicQuality = 5;
    private const int LegendaryQuality = 6;

    /// <summary>Emerald rank 10 ("Blessing of the Commander - II"), granting 12% ACR, per the S3 gear data.</summary>
    private const int EmeraldCap = 1500;

    private static readonly ReportDungeon TestDungeon =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    [Fact]
    public void Legendary_ResolvesTheCooldownRecoverySeed()
    {
        var combatant = new FullCombatant(new CombatantInfoEvent { Gear = [new Item { Id = 999, Quality = LegendaryQuality }] });

        Assert.NotNull(combatant.Legendary);
        var seed = Assert.Single(combatant.Stats.CooldownRecovery);
        Assert.Equal(1.10, seed.Value, precision: 6);
        Assert.Empty(combatant.Stats.CooldownAcceleration);
    }

    [Fact]
    public void NoLegendary_ResolvesNoCooldownRecoverySeed()
    {
        var combatant = new FullCombatant(new CombatantInfoEvent { Gear = [new Item { Id = 999, Quality = EpicQuality }] });

        Assert.Null(combatant.Legendary);
        Assert.Empty(combatant.Stats.CooldownRecovery);
    }

    [Fact]
    public async Task Legendary_GivesTheTrackerACooldownRecoveryOfOnePointOne()
    {
        var (_, statTracker) = await RunWithStatTracker(LegendaryQuality);

        Assert.Equal(1.10, statTracker.CurrentCooldownRecovery, precision: 6);
        Assert.Equal(1.10, statTracker.StartingCooldownRecovery, precision: 6);
    }

    [Fact]
    public async Task NoLegendary_GivesTheTrackerACooldownRecoveryOfOne()
    {
        var (_, statTracker) = await RunWithStatTracker(EpicQuality);

        Assert.Equal(1.0, statTracker.CurrentCooldownRecovery, precision: 6);
        Assert.Equal(1.0, statTracker.StartingCooldownRecovery, precision: 6);
    }

    /// <summary>The recovery pool divides, so 10000ms / (1 + 0.10) = 9090.9ms, which returns the charge at the first whole millisecond after it: 9091ms.</summary>
    [Fact]
    public async Task Legendary_ShortensBaseCooldownByTenPercent()
    {
        var spellUsable = await Run(LegendaryQuality);

        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        Assert.Equal(9091, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    [Fact]
    public async Task NoLegendary_LeavesBaseCooldownUntouched()
    {
        var spellUsable = await Run(EpicQuality);

        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        Assert.Equal(BaseCdMsA, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    /// <summary>
    /// One multiplicative factor: the legendary's 1.1 and Chronoshift's 8 add their parts above 1 rather
    /// than multiplying, so an ability scaling off Cooldown Recovery recovers at 8.1×: 10000ms / (1 + 0.10 + 7.0) = 1234.6ms, which returns the charge at the first whole millisecond after it: 1235ms.
    /// </summary>
    [Fact]
    public async Task Legendary_ComposesAdditivelyWithChronoshift()
    {
        var (spellUsable, statTracker) = await RunWithStatTracker(LegendaryQuality);

        statTracker.AddCooldownModifier(CooldownPool.CooldownRecovery, new CooldownModifier(8.0), timestamp: 1000);
        Assert.Equal(8.10, spellUsable.EffectiveRate(SpellA), precision: 6);

        spellUsable.BeginCooldown(SpellA, timestamp: 1000);
        Assert.Equal(1235, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    /// <summary>RechargeDuration reports the effective (accelerated) period a fresh charge takes, rounded to the nearest millisecond: 10000 / 1.10 = 9091.</summary>
    [Fact]
    public async Task Legendary_ShortensRechargeDuration()
    {
        var spellUsable = await Run(LegendaryQuality);

        Assert.Equal(9091, spellUsable.RechargeDuration(SpellA));
    }

    [Fact]
    public async Task NoLegendary_RechargeDurationIsBaseCooldown()
    {
        var spellUsable = await Run(EpicQuality);

        Assert.Equal(BaseCdMsA, spellUsable.RechargeDuration(SpellA));
    }

    /// <summary>
    /// ACR shortens the base (SpellB: 20000 × 0.88 = 17600ms). Without a legendary that is the whole
    /// recharge; with one the acceleration divides the pool: 17600 / (1 + 0.10) = 16000ms.
    /// </summary>
    [Fact]
    public async Task AcrAndAcceleration_Compose_OnTheRechargeDuration()
    {
        var acrOnly = await Run(EpicQuality, emerald: EmeraldCap);
        Assert.Equal(17_600, acrOnly.RechargeDuration(SpellB));

        var acrAndAcceleration = await Run(LegendaryQuality, emerald: EmeraldCap);
        Assert.Equal(16_000, acrAndAcceleration.RechargeDuration(SpellB));
    }

    /// <summary>
    /// Acceleration divides base cooldowns, but a flat reduction is not part of that pool. With a
    /// legendary equipped and no ACR, a 1000ms reduction still generates and applies the whole 1000ms.
    /// </summary>
    [Fact]
    public async Task FlatReduction_IsNotDividedByAcceleration()
    {
        var spellUsable = await Run(LegendaryQuality);
        spellUsable.BeginCooldown(SpellB, timestamp: 1000);

        var cdr = spellUsable.ReduceCooldown(SpellB, 1000, timestamp: 1000);

        Assert.Equal(1000, cdr.Total);
        Assert.Equal(1000, cdr.Effective);
        Assert.Equal(0, cdr.Wasted);
    }

    /// <summary>
    /// A category-scoped acceleration term only divides the cooldowns of abilities in that category. SpellA is
    /// <see cref="AbilityCategory.Major"/> and gets 10000ms / (1 + 1.0) = 5000ms; SpellB is unclassified and is
    /// left at its full base cooldown.
    /// </summary>
    [Fact]
    public async Task CategoryScopedAcceleration_SpeedsUpOnlyTheMatchingSpell()
    {
        CooldownModifierSet acceleration =
            [new CooldownModifier(1.0, new[] { AbilityCategory.Major })];

        var spellUsable = await RunWithScopedAcceleration(acceleration);

        spellUsable.BeginCooldown(SpellA, timestamp: 1000);
        spellUsable.BeginCooldown(SpellB, timestamp: 1000);

        Assert.Equal((int)(BaseCdMsA / 2.0), spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
        Assert.Equal(BaseCdMsB, spellUsable.CooldownRemaining(SpellB, atTimestamp: 1000));
    }

    private static async Task<SpellUsable> RunWithScopedAcceleration(CooldownModifierSet acceleration)
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
        ];

        List<Event> events =
        [
            new CombatantInfoEvent { Timestamp = 0, SourceId = PlayerId },
        ];

        var parser = new TestParser(emitter, provider, moduleTypes, acceleration);
        await parser.Analyze(events, PlayerId, dungeon: TestDungeon);

        return parser.GetModule<SpellUsable>()!;
    }

    private static async Task<(SpellUsable spellUsable, StatTracker statTracker)> RunWithStatTracker(int itemQuality, int emerald = 0)
    {
        var spellUsable = await Run(itemQuality, emerald);
        return (spellUsable, spellUsable.Owner.GetModule<StatTracker>()!);
    }

    private static async Task<SpellUsable> Run(int itemQuality, int emerald = 0)
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
        ];

        List<Event> events =
        [
            new CombatantInfoEvent
            {
                Timestamp = 0,
                SourceId = PlayerId,
                Emerald = emerald,
                Gear = [new Item { Id = 999, Quality = itemQuality }],
            },
        ];

        var parser = new TestParser(emitter, provider, moduleTypes);
        await parser.Analyze(events, PlayerId, dungeon: TestDungeon);

        return parser.GetModule<SpellUsable>()!;
    }

    private sealed class TestParser(
        EventEmitter emitter,
        IServiceProvider provider,
        Type[] moduleTypes,
        CooldownModifierSet? acceleration = null)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => moduleTypes;

        protected override Type[] GetNormalizerTypes() => [typeof(DungeonBookendNormalizer)];

        protected override FullCombatant CreateSelectedCombatant(CombatantInfoEvent info) =>
            acceleration is null
                ? base.CreateSelectedCombatant(info)
                : new FullCombatant(info) { Stats = new CombatantStats { CooldownAcceleration = acceleration } };

        protected override object? CreateInstance(Type type)
        {
            if (type == typeof(TestAbilities)) return new TestAbilities();
            if (type == typeof(DungeonBookendNormalizer)) return new DungeonBookendNormalizer(CurrentParseContext);
            return base.CreateInstance(type);
        }
    }

    private sealed class TestAbilities : Abilities
    {
        public override IEnumerable<SpellbookAbility> Spellbook() =>
        [
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = SpellA, Name = "Spell A", Cooldown = (double)CdSecondsA, AbilityCategory = AbilityCategory.Major, CooldownScaling = new() { DurationCooldownReduction = true, TimeRateCooldownRecovery = true } },
                Category = SpellCategory.Rotational,
            },
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = SpellB, Name = "Spell B", Cooldown = (double)CdSecondsB, CooldownScaling = new() { DurationCooldownReduction = true, TimeRateCooldownRecovery = true } },
                Category = SpellCategory.Rotational,
            },
        ];
    }
}
