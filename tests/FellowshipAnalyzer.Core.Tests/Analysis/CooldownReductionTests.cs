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
/// Tests for Ability Cooldown Reduction: the selected <see cref="Combatant"/> resolving gem power totals
/// into unlocked ranks at construction, <see cref="CooldownReduction"/> pooling the gear seed additively
/// with further sources, and <see cref="SpellUsable"/> applying the pool as <c>base * (1 - acr)</c> to both
/// base cooldowns and flat reductions.
/// </summary>
public sealed class CooldownReductionTests
{
    private const int PlayerId = 7;
    private const int SpellA = 101;
    private const int CdSecondsA = 10;
    private const int BaseCdMs = CdSecondsA * 1000;

    private const int SpellB = 102;
    private const int CdSecondsB = 20;

    /// <summary>Emerald/Diamond rank 5 ("Blessing of the ..."), per s3 gear_data.json.</summary>
    private const int Rank5Power = 450;

    /// <summary>Emerald/Diamond rank 10 ("... - II"), which is also Gems.Economy.Cap.</summary>
    private const int Rank10Power = 1500;

    private static readonly ReportFight TestFight =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    // -------------------------------------------------------------------------
    // Combatant: resolving gem power into unlocked ranks (no parser needed)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(449, 0.0)]
    [InlineData(450, 0.04)]
    [InlineData(1499, 0.04)]
    [InlineData(1500, 0.12)]
    public void EmeraldPower_UnlocksBlessingOfTheCommander(int emerald, double expected)
    {
        var combatant = new Combatant(new CombatantInfoEvent { SourceId = PlayerId, Emerald = emerald });

        Assert.Equal(expected, combatant.Stats.AbilityCooldownReduction, precision: 6);
    }

    [Fact]
    public void EmeraldAtCap_ReplacesLowerRank_RatherThanSummingWithIt()
    {
        var combatant = new Combatant(new CombatantInfoEvent { SourceId = PlayerId, Emerald = Rank10Power });

        Assert.Equal(0.12, combatant.Stats.AbilityCooldownReduction, precision: 6);
        Assert.NotEqual(0.16, combatant.Stats.AbilityCooldownReduction, precision: 6);
    }

    [Fact]
    public void DiamondPower_GrantsRelicReduction_AndNotAbilityReduction()
    {
        var combatant = new Combatant(new CombatantInfoEvent { SourceId = PlayerId, Diamond = Rank10Power });

        Assert.Equal(0.24, combatant.Stats.RelicCooldownReduction, precision: 6);
        Assert.Equal(0.0, combatant.Stats.AbilityCooldownReduction, precision: 6);
    }

    /// <summary>
    /// Blessing of the Artisan is scoped to relic cooldowns, a slot the ability pool does not model, so the
    /// diamond seed must never leak into <see cref="CooldownReduction.Current"/>.
    /// </summary>
    [Fact]
    public async Task DiamondPower_DoesNotLeakIntoAbilityCooldownPool()
    {
        var (acr, _) = await Run(diamond: Rank10Power);

        Assert.Equal(0.0, acr.Current, precision: 6);
    }

    // -------------------------------------------------------------------------
    // CooldownReduction: the additive pool
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Sources_CombineAdditively()
    {
        var (acr, _) = await Run(emerald: Rank10Power);
        acr.Add(0.10);

        Assert.Equal(0.22, acr.Current, precision: 6);
    }

    [Fact]
    public async Task TotalReduction_IsCappedAtOneHundredPercent()
    {
        var (acr, spellUsable) = await Run(emerald: Rank10Power);
        acr.Add(2.0);

        Assert.Equal(1.0, acr.Current, precision: 6);

        spellUsable.BeginCooldown(SpellA, timestamp: 1000);
        Assert.Equal(0, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    // -------------------------------------------------------------------------
    // SpellUsable: applying the pool
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AbilityCooldownReduction_ShortensBaseCooldown()
    {
        var (_, spellUsable) = await Run(emerald: Rank10Power);

        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        Assert.Equal(8800, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    [Fact]
    public async Task NoGemPower_LeavesBaseCooldownUntouched()
    {
        var (_, spellUsable) = await Run(emerald: 0);

        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        Assert.Equal(BaseCdMs, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    /// <summary>
    /// ACR shortens flat reductions too: at 12% ACR the base recharge is 8800ms and a 1000ms
    /// Rolling-Flames-style reduction generates 880ms, shortening the running cooldown by exactly that.
    /// </summary>
    [Fact]
    public async Task FlatReduction_IsScaledByAcr()
    {
        var (_, spellUsable) = await Run(emerald: Rank10Power);
        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        var cdr = spellUsable.ReduceCooldown(SpellA, 1000, timestamp: 1000);

        Assert.Equal(880, cdr.GeneratedMs);
        Assert.Equal(880, cdr.AppliedMs);
        Assert.Equal(0, cdr.WastedMs);
        Assert.Equal(8800 - 880, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    /// <summary>With no ACR the flat reduction passes through unscaled: a 1000ms request generates 1000ms.</summary>
    [Fact]
    public async Task FlatReduction_WithZeroAcr_GeneratesFullAmount()
    {
        var (_, spellUsable) = await Run(emerald: 0);
        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        var cdr = spellUsable.ReduceCooldown(SpellA, 1000, timestamp: 1000);

        Assert.Equal(1000, cdr.GeneratedMs);
        Assert.Equal(1000, cdr.AppliedMs);
        Assert.Equal(BaseCdMs - 1000, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    /// <summary>
    /// Waste means reduction with no running cooldown left to shorten. The request is scaled by ACR
    /// (100000 × 0.88 = 88000ms generated); only what lands on the ACR-shortened base cooldown is applied.
    /// </summary>
    [Fact]
    public async Task FlatReductionBeyondRemainingCooldown_IsGeneratedButNotApplied()
    {
        var (_, spellUsable) = await Run(emerald: Rank10Power);
        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        var cdr = spellUsable.ReduceCooldown(SpellA, 100_000, timestamp: 1000);

        Assert.Equal(88_000, cdr.GeneratedMs);
        Assert.Equal(8800, cdr.AppliedMs);
        Assert.Equal(88_000 - 8800, cdr.WastedMs);
        Assert.False(spellUsable.IsOnCooldown(SpellA));
    }

    /// <summary>
    /// ACR shortens the base recharge (SpellB: 20000 × 0.88 = 17600ms) and the flat reduction alike.
    /// With both charges spent and 500ms left on the recharging one, a 1000ms request generates 880ms:
    /// 500ms ends that charge and the remaining 380ms carries onto the next charge, wasting nothing.
    /// </summary>
    [Fact]
    public async Task FlatReduction_ScaledByAcr_CarriesAcrossChargeBoundary()
    {
        var (_, spellUsable) = await Run(emerald: Rank10Power);
        spellUsable.BeginCooldown(SpellB, timestamp: 0);
        spellUsable.BeginCooldown(SpellB, timestamp: 0);

        Assert.Equal(500, spellUsable.CooldownRemaining(SpellB, atTimestamp: 17_100));

        var cdr = spellUsable.ReduceCooldown(SpellB, 1000, timestamp: 17_100);

        Assert.Equal(880, cdr.GeneratedMs);
        Assert.Equal(880, cdr.AppliedMs);
        Assert.Equal(0, cdr.WastedMs);
        Assert.Equal(1, spellUsable.ChargesAvailable(SpellB));
        Assert.Equal(17_600 - 380, spellUsable.CooldownRemaining(SpellB, atTimestamp: 17_100));
    }

    /// <summary>
    /// The two are distinct mechanics: ACR multiplies the base, recovery divides it. Chronoshift's 8.0
    /// added recovery takes a non-hasted spell to 9×, so 10000 × (1 - 0.12) / 9 = 977ms.
    /// </summary>
    [Fact]
    public async Task AbilityCooldownReduction_ComposesWithCooldownRecovery()
    {
        var (_, spellUsable) = await Run(emerald: Rank10Power);

        spellUsable.SetAddedCooldownRecovery(8.0, timestamp: 1000);
        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        Assert.Equal(8800 / 9, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    // -------------------------------------------------------------------------
    // Test infrastructure
    // -------------------------------------------------------------------------

    private static async Task<(CooldownReduction acr, SpellUsable spellUsable)> Run(int emerald = 0, int diamond = 0)
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
            typeof(CooldownReduction),
            typeof(SpellUsable),
        ];

        List<Event> events =
        [
            new CombatantInfoEvent { Timestamp = 0, SourceId = PlayerId, Emerald = emerald, Diamond = diamond },
        ];

        var parser = new TestParser(emitter, provider, moduleTypes);
        await parser.Analyze(events, PlayerId, fight: TestFight);

        return (parser.GetModule<CooldownReduction>()!, parser.GetModule<SpellUsable>()!);
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
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = SpellB, Name = "Spell B", Cooldown = (double)CdSecondsB, Charges = 2 },
                Category = SpellCategory.Rotational,
            },
        ];
    }
}
