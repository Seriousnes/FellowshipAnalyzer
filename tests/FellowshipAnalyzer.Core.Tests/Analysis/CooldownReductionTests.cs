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
/// Tests for Ability Cooldown Reduction: <see cref="GemPowers"/> resolving gem power totals into unlocked
/// ranks, <see cref="CooldownReduction"/> pooling them additively, and <see cref="SpellUsable"/> applying
/// the pool as <c>base × (1 - acr)</c> to both base cooldowns and flat reductions.
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
    // GemPowers: resolving gem power into unlocked ranks
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(449, 0.0)]
    [InlineData(450, 0.04)]
    [InlineData(1499, 0.04)]
    [InlineData(1500, 0.12)]
    public async Task EmeraldPower_UnlocksBlessingOfTheCommander(int emerald, double expected)
    {
        var (gems, _, _) = await Run(emerald: emerald);

        Assert.Equal(expected, gems.AbilityCooldownReduction, precision: 6);
    }

    [Fact]
    public async Task EmeraldAtCap_ReplacesLowerRank_RatherThanSummingWithIt()
    {
        // Rank 10's description says "(Replaces Blessing of the Commander)", so 1500 power grants 12%,
        // not rank 5's 4% plus rank 10's 12%.
        var (gems, _, _) = await Run(emerald: Rank10Power);

        Assert.Equal(0.12, gems.AbilityCooldownReduction, precision: 6);
        Assert.NotEqual(0.16, gems.AbilityCooldownReduction, precision: 6);
    }

    [Fact]
    public async Task DiamondPower_GrantsRelicReduction_AndNotAbilityReduction()
    {
        // Blessing of the Artisan is scoped to relic cooldowns, a slot the pipeline does not model, so it
        // must never leak into the ability pool.
        var (gems, acr, _) = await Run(emerald: 0, diamond: Rank10Power);

        Assert.Equal(0.24, gems.RelicCooldownReduction, precision: 6);
        Assert.Equal(0.0, gems.AbilityCooldownReduction, precision: 6);
        Assert.Equal(0.0, acr.Current, precision: 6);
    }

    // -------------------------------------------------------------------------
    // CooldownReduction: the additive pool
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Sources_CombineAdditively()
    {
        var (_, acr, _) = await Run(emerald: Rank10Power);
        acr.Add(0.10);

        Assert.Equal(0.22, acr.Current, precision: 6);
    }

    [Fact]
    public async Task TotalReduction_IsCappedAtOneHundredPercent()
    {
        // 100% ACR means no cooldown at all; beyond that a cooldown must not go negative.
        var (_, acr, spellUsable) = await Run(emerald: Rank10Power);
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
        var (_, _, spellUsable) = await Run(emerald: Rank10Power);

        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        // 10000ms × (1 - 0.12) = 8800ms.
        Assert.Equal(8800, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    [Fact]
    public async Task NoGemPower_LeavesBaseCooldownUntouched()
    {
        var (_, _, spellUsable) = await Run(emerald: 0);

        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        Assert.Equal(BaseCdMs, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    [Fact]
    public async Task AbilityCooldownReduction_DoesNotWeakenFlatReduction()
    {
        // ACR shortens base cooldowns but leaves flat reductions at full strength: at 12% ACR the base
        // recharge is 8800ms, yet a 1000ms Rolling-Flames-style reduction still generates and applies the
        // whole 1000ms.
        var (_, _, spellUsable) = await Run(emerald: Rank10Power);
        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        var cdr = spellUsable.ReduceCooldown(SpellA, 1000, timestamp: 1000);

        Assert.Equal(1000, cdr.GeneratedMs);
        Assert.Equal(1000, cdr.AppliedMs);
        Assert.Equal(0, cdr.WastedMs);
        Assert.Equal(8800 - 1000, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    [Fact]
    public async Task FlatReductionBeyondRemainingCooldown_IsGeneratedButNotApplied()
    {
        // Waste means reduction with no running cooldown left to shorten. The flat request is not scaled by
        // ACR, so it generates in full; only what lands on the ACR-shortened base cooldown is applied.
        var (_, _, spellUsable) = await Run(emerald: Rank10Power);
        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        var cdr = spellUsable.ReduceCooldown(SpellA, 100_000, timestamp: 1000);

        Assert.Equal(100_000, cdr.GeneratedMs);
        Assert.Equal(8800, cdr.AppliedMs);
        Assert.Equal(100_000 - 8800, cdr.WastedMs);
        Assert.False(spellUsable.IsOnCooldown(SpellA));
    }

    [Fact]
    public async Task FlatReduction_AppliesInFull_EvenWhileAcrShortensTheBaseRecharge()
    {
        // ACR shortens the base recharge (SpellB: 20000 × 0.88 = 17600ms) but does not touch the flat
        // reduction. With both charges spent and 0.5s left on the recharging one, a full 1000ms reduction
        // ends that charge with 500ms and carries the remaining 500ms onto the next charge, wasting nothing.
        var (_, _, spellUsable) = await Run(emerald: Rank10Power);
        spellUsable.BeginCooldown(SpellB, timestamp: 0);
        spellUsable.BeginCooldown(SpellB, timestamp: 0);

        Assert.Equal(500, spellUsable.CooldownRemaining(SpellB, atTimestamp: 17_100));

        var cdr = spellUsable.ReduceCooldown(SpellB, 1000, timestamp: 17_100);

        Assert.Equal(1000, cdr.GeneratedMs);
        Assert.Equal(1000, cdr.AppliedMs);
        Assert.Equal(0, cdr.WastedMs);
        Assert.Equal(1, spellUsable.ChargesAvailable(SpellB));
        Assert.Equal(17_600 - 500, spellUsable.CooldownRemaining(SpellB, atTimestamp: 17_100));
    }

    [Fact]
    public async Task AbilityCooldownReduction_ComposesWithCooldownRecovery()
    {
        // The two are distinct mechanics: ACR multiplies the base, recovery divides it. Chronoshift's 8.0
        // added recovery takes a non-hasted spell to 9×, so 10000 × (1 - 0.12) / 9 = 977ms.
        var (_, _, spellUsable) = await Run(emerald: Rank10Power);

        spellUsable.SetAddedCooldownRecovery(8.0, timestamp: 1000);
        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        Assert.Equal(8800 / 9, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    // -------------------------------------------------------------------------
    // Test infrastructure
    // -------------------------------------------------------------------------

    private static async Task<(GemPowers gems, CooldownReduction acr, SpellUsable spellUsable)> Run(
        int emerald = 0,
        int diamond = 0)
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
            typeof(SpellUsable),
        ];

        List<Event> events =
        [
            new CombatantInfoEvent { Timestamp = 0, SourceId = PlayerId, Emerald = emerald, Diamond = diamond },
        ];

        var parser = new TestParser(emitter, provider, moduleTypes);
        await parser.Analyze(events, PlayerId, fight: TestFight);

        return (parser.GetModule<GemPowers>()!, parser.GetModule<CooldownReduction>()!, parser.GetModule<SpellUsable>()!);
    }

    /// <summary>
    /// Gem power is resolved at <see cref="FightStartEvent"/>, which only exists because
    /// <see cref="FightBookendNormalizer"/> prepends it. The base parser's
    /// <c>GetNormalizerTypes</c> returns nothing (concrete parsers get a generated override), so a
    /// hand-written test parser has to ask for the normalizer explicitly or no fight start is dispatched.
    /// </summary>
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
