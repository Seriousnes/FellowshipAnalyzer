using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for the cooldown stat pools <see cref="StatTracker"/> tracks: the selected
/// <see cref="Combatant"/> resolving gem power totals into unlocked ranks at construction, the tracker
/// pooling the gear seed additively with runtime <see cref="CooldownModifier"/>s and fabricating
/// <see cref="ChangeCooldownModifierEvent"/> on every change, buff-driven registration via
/// <see cref="CooldownBuff"/>, and <see cref="SpellUsable"/> applying Ability Cooldown Reduction as
/// <c>base * (1 - acr)</c> to both base cooldowns and flat reductions.
/// </summary>
public sealed partial class StatTrackerCooldownTests
{
    private const int PlayerId = 7;
    private const int SpellA = 101;
    private const int CdSecondsA = 10;
    private const int BaseCdMs = CdSecondsA * 1000;

    private const int SpellB = 102;
    private const int CdSecondsB = 20;

    /// <summary>Emerald rank 10 ("Blessing of the Commander - II"), which is also Gems.Economy.Cap.</summary>
    private const int Rank10Power = 1500;

    /// <summary>Ability id of the in-stream buff that drives the probe callback.</summary>
    private const int TriggerId = 8888;

    /// <summary>Ability id registered with a <see cref="CooldownBuff"/> in the buff-driven tests.</summary>
    private const int CooldownBuffId = 555;

    private static readonly ReportFight TestFight =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(449, 0.0)]
    [InlineData(450, 0.04)]
    [InlineData(1499, 0.04)]
    [InlineData(1500, 0.12)]
    public void EmeraldPower_UnlocksBlessingOfTheCommander(int emerald, double expected)
    {
        var combatant = new Combatant(new CombatantInfoEvent { SourceId = PlayerId, Emerald = emerald });

        Assert.Equal(expected, combatant.Stats.AbilityCooldownReduction.Total(null), precision: 6);
    }

    [Fact]
    public void EmeraldAtCap_ReplacesLowerRank_RatherThanSummingWithIt()
    {
        var combatant = new Combatant(new CombatantInfoEvent { SourceId = PlayerId, Emerald = Rank10Power });

        Assert.Equal(0.12, combatant.Stats.AbilityCooldownReduction.Total(null), precision: 6);
        Assert.NotEqual(0.16, combatant.Stats.AbilityCooldownReduction.Total(null), precision: 6);
    }

    [Fact]
    public void SpellScopedModifier_AppliesOnlyToAMatchingAbility()
    {
        var matching = new SpellbookAbility
        {
            PrimarySpell = new Spell { Id = SpellA, Name = "Spell A" },
            Category = SpellCategory.Rotational,
        };
        var other = new SpellbookAbility
        {
            PrimarySpell = new Spell { Id = SpellB, Name = "Spell B" },
            Category = SpellCategory.Rotational,
        };

        CooldownModifierSet set =
            [new CooldownModifier(0.10, new Spell[] { new() { Id = SpellA } })];

        Assert.Equal(0.10, set.Total(matching), precision: 6);
        Assert.Equal(0.0, set.Total(other), precision: 6);
    }

    [Fact]
    public void CategoryScopedModifier_AppliesOnMatch_NeverWhenCategoryIsNull()
    {
        var major = new SpellbookAbility
        {
            PrimarySpell = new Spell { Id = SpellA, Name = "Spell A", AbilityCategory = AbilityCategory.Major },
            Category = SpellCategory.Rotational,
        };
        var unclassified = new SpellbookAbility
        {
            PrimarySpell = new Spell { Id = SpellB, Name = "Spell B" },
            Category = SpellCategory.Rotational,
        };

        CooldownModifierSet set =
            [new CooldownModifier(0.10, new[] { AbilityCategory.Major })];

        Assert.Equal(0.10, set.Total(major), precision: 6);
        Assert.Equal(0.0, set.Total(unclassified), precision: 6);
    }

    [Fact]
    public void FuncScopedModifier_AppliesWhenThePredicateMatches()
    {
        var enabled = new SpellbookAbility
        {
            PrimarySpell = new Spell { Id = SpellA, Name = "Spell A" },
            Category = SpellCategory.Rotational,
            Enabled = true,
        };
        var disabled = new SpellbookAbility
        {
            PrimarySpell = new Spell { Id = SpellB, Name = "Spell B" },
            Category = SpellCategory.Rotational,
            Enabled = false,
        };

        CooldownModifierSet set =
            [new CooldownModifier(0.10, (Func<SpellbookAbility, bool>)(a => a.Enabled))];

        Assert.Equal(0.10, set.Total(enabled), precision: 6);
        Assert.Equal(0.0, set.Total(disabled), precision: 6);
    }

    [Fact]
    public void TotalForNullAbility_IncludesOnlyUnscopedEntries()
    {
        CooldownModifierSet set =
        [
            new CooldownModifier(0.05),
            new CooldownModifier(0.10, new[] { AbilityCategory.Major }),
        ];

        Assert.Equal(0.05, set.Total(null), precision: 6);
    }

    /// <summary>
    /// The gear seed (0.12) and a runtime modifier (0.10) sum to 0.22 on the current total, while the
    /// starting total keeps reporting the seed alone.
    /// </summary>
    [Fact]
    public async Task Sources_CombineAdditively()
    {
        var (statTracker, _, _) = await Run(emerald: Rank10Power);
        statTracker.AddCooldownModifier(CooldownPool.AbilityCooldownReduction, new CooldownModifier(0.10));

        Assert.Equal(0.22, statTracker.CurrentAbilityCooldownReduction(null), precision: 6);
        Assert.Equal(0.12, statTracker.StartingAbilityCooldownReduction(null), precision: 6);
    }

    [Fact]
    public async Task TotalReduction_IsCappedAtOneHundredPercent()
    {
        var (statTracker, spellUsable, _) = await Run(emerald: Rank10Power);
        statTracker.AddCooldownModifier(CooldownPool.AbilityCooldownReduction, new CooldownModifier(2.0));

        Assert.Equal(1.0, statTracker.CurrentAbilityCooldownReduction(null), precision: 6);

        spellUsable.BeginCooldown(SpellA, timestamp: 1000);
        Assert.Equal(0, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    /// <summary>
    /// Adding a modifier during dispatch fabricates a <see cref="ChangeCooldownModifierEvent"/> carrying
    /// the pool, the modifier, and the direction of the change.
    /// </summary>
    [Fact]
    public async Task AddCooldownModifier_FabricatesChangeCooldownModifierEvent()
    {
        var modifier = new CooldownModifier(0.10);

        var (_, _, parser) = await Run(
            events: [CreateTrigger(1000)],
            onApplyBuff: (owner, e) =>
            {
                if (e.Ability?.FSLID.Value == TriggerId)
                    owner.GetModule<StatTracker>()!.AddCooldownModifier(
                        CooldownPool.AbilityCooldownReduction, modifier, e);
            });

        var change = Assert.Single(parser.GetModule<ChangeProbeModule>()!.Changes);
        Assert.Equal(CooldownPool.AbilityCooldownReduction, change.Pool);
        Assert.Same(modifier, change.Modifier);
        Assert.True(change.Added);
        Assert.Equal(1000, change.Timestamp);
    }

    /// <summary>
    /// Removing a modifier that was never added is a no-op: the pool is unchanged and no
    /// <see cref="ChangeCooldownModifierEvent"/> is fabricated.
    /// </summary>
    [Fact]
    public async Task RemoveCooldownModifier_OfAbsentModifier_FabricatesNothing()
    {
        var (statTracker, _, parser) = await Run(
            events: [CreateTrigger(1000)],
            onApplyBuff: (owner, e) =>
            {
                if (e.Ability?.FSLID.Value == TriggerId)
                    owner.GetModule<StatTracker>()!.RemoveCooldownModifier(
                        CooldownPool.CooldownAcceleration, new CooldownModifier(8.0), e);
            });

        Assert.Empty(parser.GetModule<ChangeProbeModule>()!.Changes);
        Assert.Equal(0.0, statTracker.CurrentCooldownAcceleration(null), precision: 6);
    }

    /// <summary>
    /// ACR is snapshot semantics: a modifier added mid-flight leaves the running cooldown untouched
    /// (SpellA keeps its 9000ms remaining), while the next cooldown begun after the change starts at
    /// the reduced duration (SpellB: 20000 × (1 - 0.5) = 10000ms).
    /// </summary>
    [Fact]
    public async Task AcrModifierMidFlight_LeavesInFlightCooldown_ButShortensTheNext()
    {
        var (_, spellUsable, parser) = await Run(
            events: [CreateCast(1000, SpellA), CreateTrigger(2000)],
            onApplyBuff: (owner, e) =>
            {
                if (e.Ability?.FSLID.Value == TriggerId)
                    owner.GetModule<StatTracker>()!.AddCooldownModifier(
                        CooldownPool.AbilityCooldownReduction, new CooldownModifier(0.5), e);
            });

        var updates = parser.GetModule<ChangeProbeModule>()!.Updates;
        Assert.Equal(11_000, updates.Last(e => e.Ability.FSLID == SpellA
            && e.UpdateType == UpdateSpellUsableType.BeginCooldown).ExpectedRechargeTimestamp);
        Assert.DoesNotContain(updates, e => e.Ability.FSLID == SpellA
            && e.UpdateType == UpdateSpellUsableType.ChangeCooldownRate);

        spellUsable.BeginCooldown(SpellB, timestamp: 3000);
        Assert.Equal(10_000, spellUsable.CooldownRemaining(SpellB, atTimestamp: 3000));
    }

    /// <summary>
    /// A registered <see cref="CooldownBuff"/> joins both pools when the buff applies: ACR 0.10 and
    /// CDA 0.50, taking SpellA's recovery rate to 1.5.
    /// </summary>
    [Fact]
    public async Task CooldownBuff_AppliesOnApplyBuff()
    {
        var (statTracker, spellUsable, _) = await Run(
            events: [CreateApplyBuff(1000, CooldownBuffId)],
            cooldownBuff: (CooldownBuffId, new CooldownBuff(
                AbilityCooldownReduction: new CooldownModifier(0.10),
                CooldownAcceleration: new CooldownModifier(0.50))));

        Assert.Equal(0.10, statTracker.CurrentAbilityCooldownReduction(null), precision: 6);
        Assert.Equal(0.50, statTracker.CurrentCooldownAcceleration(null), precision: 6);
        Assert.Equal(1.5, spellUsable.EffectiveRate(SpellA), precision: 6);
    }

    /// <summary>The buff's modifiers leave both pools when the buff drops, restoring the base rate.</summary>
    [Fact]
    public async Task CooldownBuff_RevertsOnRemoveBuff()
    {
        var (statTracker, spellUsable, _) = await Run(
            events: [CreateApplyBuff(1000, CooldownBuffId), CreateRemoveBuff(2000, CooldownBuffId)],
            cooldownBuff: (CooldownBuffId, new CooldownBuff(
                AbilityCooldownReduction: new CooldownModifier(0.10),
                CooldownAcceleration: new CooldownModifier(0.50))));

        Assert.Equal(0.0, statTracker.CurrentAbilityCooldownReduction(null), precision: 6);
        Assert.Equal(0.0, statTracker.CurrentCooldownAcceleration(null), precision: 6);
        Assert.Equal(1.0, spellUsable.EffectiveRate(SpellA), precision: 6);
    }

    [Fact]
    public async Task AbilityCooldownReduction_ShortensBaseCooldown()
    {
        var (_, spellUsable, _) = await Run(emerald: Rank10Power);

        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        Assert.Equal(8800, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    [Fact]
    public async Task NoGemPower_LeavesBaseCooldownUntouched()
    {
        var (_, spellUsable, _) = await Run(emerald: 0);

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
        var (_, spellUsable, _) = await Run(emerald: Rank10Power);
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
        var (_, spellUsable, _) = await Run(emerald: 0);
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
        var (_, spellUsable, _) = await Run(emerald: Rank10Power);
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
        var (_, spellUsable, _) = await Run(emerald: Rank10Power);
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
        var (statTracker, spellUsable, _) = await Run(emerald: Rank10Power);

        statTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, new CooldownModifier(8.0), timestamp: 1000);
        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        Assert.Equal(8800 / 9, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    private static async Task<(StatTracker statTracker, SpellUsable spellUsable, TestParser parser)> Run(
        int emerald = 0,
        List<Event>? events = null,
        Action<CombatLogParser, ApplyBuffEvent>? onApplyBuff = null,
        (int SpellId, CooldownBuff Buff)? cooldownBuff = null)
    {
        var emitter = new EventEmitter(NullLogger<EventEmitter>.Instance);
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ILogger<EventEmitter>)).Returns(NullLogger<EventEmitter>.Instance);

        List<Type> moduleTypes =
        [
            typeof(TestAbilities),
            typeof(DebugAnnotations),
            typeof(StatTracker),
            typeof(Combatants),
            typeof(Haste),
            typeof(SpellUsable),
            typeof(ChangeProbeModule),
        ];
        if (cooldownBuff is not null)
            moduleTypes.Add(typeof(CooldownBuffRegistrar));

        List<Event> allEvents =
        [
            new CombatantInfoEvent { Timestamp = 0, SourceId = PlayerId, Emerald = emerald },
            .. events ?? [],
        ];

        var parser = new TestParser(emitter, provider, [.. moduleTypes])
        {
            OnApplyBuff = onApplyBuff,
            CooldownBuff = cooldownBuff,
        };
        await parser.Analyze(allEvents, PlayerId, fight: TestFight);

        return (parser.GetModule<StatTracker>()!, parser.GetModule<SpellUsable>()!, parser);
    }

    private static CastEvent CreateCast(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = 11,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
        Target = null,
        Channel = new EndChannelEvent(),
    };

    private static ApplyBuffEvent CreateTrigger(int timestamp) => CreateApplyBuff(timestamp, TriggerId);

    private static ApplyBuffEvent CreateApplyBuff(int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = abilityId, Name = $"Buff {abilityId}" },
    };

    private static RemoveBuffEvent CreateRemoveBuff(int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = abilityId, Name = $"Buff {abilityId}" },
    };

    private sealed class TestParser(EventEmitter emitter, IServiceProvider provider, Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        public Action<CombatLogParser, ApplyBuffEvent>? OnApplyBuff { get; init; }

        public (int SpellId, CooldownBuff Buff)? CooldownBuff { get; init; }

        protected override Type[] GetModuleTypes() => moduleTypes;

        protected override object? CreateInstance(Type type)
        {
            if (type == typeof(TestAbilities)) return new TestAbilities();
            if (type == typeof(ChangeProbeModule)) return new ChangeProbeModule { OnApplyBuff = OnApplyBuff };
            if (type == typeof(CooldownBuffRegistrar))
                return new CooldownBuffRegistrar(new Lazy<StatTracker>(() => GetModule<StatTracker>()!))
                {
                    SpellId = CooldownBuff!.Value.SpellId,
                    Buff = CooldownBuff!.Value.Buff,
                };
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

    /// <summary>Captures every <see cref="ChangeCooldownModifierEvent"/> in dispatch order. Also
    /// forwards <see cref="ApplyBuffEvent"/>s to an optional callback so tests can drive
    /// StatTracker mutations during dispatch.</summary>
    internal sealed partial class ChangeProbeModule : Analyzer
    {
        public List<ChangeCooldownModifierEvent> Changes { get; } = [];

        public List<UpdateSpellUsableEvent> Updates { get; } = [];

        public Action<CombatLogParser, ApplyBuffEvent>? OnApplyBuff { get; init; }

        [On<ChangeCooldownModifierEvent>]
        private void OnChange(ChangeCooldownModifierEvent e) => Changes.Add(e);

        [On<UpdateSpellUsableEvent>]
        private void OnUpdate(UpdateSpellUsableEvent e) => Updates.Add(e);

        [On<ApplyBuffEvent>(By = Actor.Player)]
        private void OnBuff(ApplyBuffEvent e) => OnApplyBuff?.Invoke(Owner, e);
    }

    /// <summary>Registers a <see cref="CooldownBuff"/> on <see cref="StatTracker"/> before dispatch
    /// starts, in place of the analyzer that would own the buff in a hero module.</summary>
    private sealed class CooldownBuffRegistrar(Lazy<StatTracker> statTracker) : Analyzer
    {
        public int SpellId { get; init; }

        public CooldownBuff Buff { get; init; } = null!;

        protected override void RegisterAttributeSubscriptions() =>
            statTracker.Value.AddCooldownBuff(SpellId, Buff);
    }
}
