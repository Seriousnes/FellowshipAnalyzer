using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for the legendary "Strand of Eternity" cooldown acceleration: <see cref="GearCooldownRecovery"/>
/// resolving the flat +10% from a legendary-quality item, and <see cref="SpellUsable"/> applying it as a
/// constant term on the shared recovery pool that composes additively with Chronoshift and haste.
/// </summary>
public sealed class GearCooldownRecoveryTests
{
    private const int PlayerId = 7;
    private const int SpellA = 101;
    private const int CdSecondsA = 10;
    private const int BaseCdMs = CdSecondsA * 1000;

    private const int EpicQuality = 5;
    private const int LegendaryQuality = 6;

    private static readonly ReportFight TestFight =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task Legendary_GrantsTenPercentAcceleration()
    {
        var (gear, _) = await Run(LegendaryQuality);

        Assert.Equal(0.10, gear.Current, precision: 6);
    }

    [Fact]
    public async Task NoLegendary_GrantsNothing()
    {
        var (gear, _) = await Run(EpicQuality);

        Assert.Equal(0.0, gear.Current, precision: 6);
    }

    [Fact]
    public async Task Legendary_ShortensBaseCooldownByTenPercent()
    {
        // The recovery pool divides, so 10000ms / (1 + 0.10) = 9090ms.
        var (_, spellUsable) = await Run(LegendaryQuality);

        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        Assert.Equal((int)(BaseCdMs / 1.10), spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    [Fact]
    public async Task NoLegendary_LeavesBaseCooldownUntouched()
    {
        var (_, spellUsable) = await Run(EpicQuality);

        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        Assert.Equal(BaseCdMs, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    [Fact]
    public async Task Legendary_ComposesAdditivelyWithChronoshift()
    {
        // One additive pool: the legendary's 0.10 adds to Chronoshift's 8.0 rather than multiplying, so a
        // non-hasted ability recovers at 9.1×: 10000ms / (1 + 0.10 + 8.0) = 1098ms.
        var (_, spellUsable) = await Run(LegendaryQuality);

        spellUsable.SetAddedCooldownRecovery(8.0, timestamp: 1000);
        Assert.Equal(9.10, spellUsable.EffectiveRate(SpellA), precision: 6);

        spellUsable.BeginCooldown(SpellA, timestamp: 1000);
        Assert.Equal((int)(BaseCdMs / 9.10), spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    [Fact]
    public async Task Legendary_ShortensRechargeDuration()
    {
        // RechargeDuration reports the effective (accelerated) period a fresh charge takes: 10000 / 1.10.
        var (_, spellUsable) = await Run(LegendaryQuality);

        Assert.Equal((int)(BaseCdMs / 1.10), spellUsable.RechargeDuration(SpellA));
    }

    [Fact]
    public async Task NoLegendary_RechargeDurationIsBaseCooldown()
    {
        var (_, spellUsable) = await Run(EpicQuality);

        Assert.Equal(BaseCdMs, spellUsable.RechargeDuration(SpellA));
    }

    // -------------------------------------------------------------------------
    // Test infrastructure
    // -------------------------------------------------------------------------

    private static async Task<(GearCooldownRecovery Gear, SpellUsable SpellUsable)> Run(int itemQuality)
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
            typeof(GemPowers),
            typeof(CooldownReduction),
            typeof(GearCooldownRecovery),
            typeof(SpellUsable),
        ];

        List<Event> events =
        [
            new CombatantInfoEvent
            {
                Timestamp = 0,
                SourceId = PlayerId,
                Gear = [new Item { Id = 999, Quality = itemQuality }],
            },
        ];

        var parser = new TestParser(emitter, provider, moduleTypes);
        await parser.Analyze(events, PlayerId, fight: TestFight);

        return (parser.GetModule<GearCooldownRecovery>()!, parser.GetModule<SpellUsable>()!);
    }

    private sealed class TestParser(EventEmitter emitter, IServiceProvider provider, Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => moduleTypes;

        protected override Type[] GetNormalizerTypes() => [typeof(FightBookendNormalizer)];

        protected override object? CreateInstance(Type type)
        {
            if (type == typeof(TestAbilities)) return new TestAbilities();
            if (type == typeof(FightBookendNormalizer)) return new FightBookendNormalizer(CurrentParseContext);
            return base.CreateInstance(type);
        }
    }

    private sealed class TestAbilities : Abilities
    {
        public override IEnumerable<SpellbookAbility> Spellbook() =>
        [
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = SpellA, Name = "Spell A", Cooldown = (double)CdSecondsA },
                Category = SpellCategory.Rotational,
            },
        ];
    }
}
