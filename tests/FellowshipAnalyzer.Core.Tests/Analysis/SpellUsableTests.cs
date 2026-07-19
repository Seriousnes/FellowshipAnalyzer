using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

using RimeSpells = FellowshipAnalyzer.Core.Common.Spells.Rime.Spells;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for <see cref="SpellUsable"/>'s single additive cooldown recovery pool: <c>1 + haste + the
/// Cooldown Acceleration pool <see cref="StatTracker"/> tracks</c>, where haste contributes only for
/// haste-flagged abilities and tracked modifiers (e.g. Chronoshift) apply within their scope. Covers
/// in-flight rescaling on pool changes, proportional progress across rate changes, and chronological
/// flush.
/// </summary>
public sealed partial class SpellUsableTests
{
    private const int PlayerId = 7;
    private const int SpellA = 101;
    private const int SpellB = 102;
    private const int SpellC = 103;
    private const int CdSecondsA = 10;
    private const int CdSecondsB = 20;
    private const int CdSecondsC = 10;

    /// <summary>WrathOfWinterBuff is a registered 30% haste buff on the <see cref="Haste"/> module.</summary>
    private const double BuffHaste = 0.30;

    /// <summary>Ability id of the in-stream buff that triggers a modifier addition via the probe callback.</summary>
    private const int TriggerId = 8888;

    /// <summary>Ability id of the in-stream buff that triggers a modifier removal via the probe callback.</summary>
    private const int RemoveTriggerId = 8887;

    private static readonly ReportFight TestFight =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    // -------------------------------------------------------------------------
    // Tracked Cooldown Acceleration modifiers (added and removed via StatTracker)
    // -------------------------------------------------------------------------

    /// <summary>
    /// SpellA cast at t=1000 with base CD=10000ms recharges to t=11000. A 1.0 acceleration modifier
    /// added at t=2000 takes the rate to 2×: remaining was 9000, new remaining = 9000/2 = 4500.
    /// </summary>
    [Fact]
    public async Task AddedAcceleration_RescalesInFlightCooldown()
    {
        var (_, spellUsable, _) = await Run(
            [CreateCast(1000, SpellA), CreateTrigger(2000)],
            onApplyBuff: (owner, e) =>
            {
                if (e.Ability?.FSLID.Value == TriggerId)
                    owner.GetModule<StatTracker>()!.AddCooldownModifier(
                        CooldownPool.CooldownAcceleration, new CooldownModifier(1.0), e);
            });

        Assert.Equal(4500, spellUsable.CooldownRemaining(SpellA, atTimestamp: 2000));
    }

    /// <summary>
    /// Adding then removing the same acceleration modifier at the same timestamp round-trips the
    /// in-flight cooldown back to its original remaining time and rate.
    /// </summary>
    [Fact]
    public async Task AddedAcceleration_ThenRemoved_RoundTripsCooldown()
    {
        var modifier = new CooldownModifier(1.0);

        var (_, spellUsable, _) = await Run(
            [CreateCast(1000, SpellA), CreateTrigger(2000)],
            onApplyBuff: (owner, e) =>
            {
                if (e.Ability?.FSLID.Value != TriggerId) return;
                var statTracker = owner.GetModule<StatTracker>()!;
                statTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, modifier, e);
                statTracker.RemoveCooldownModifier(CooldownPool.CooldownAcceleration, modifier, e);
            });

        var remaining = spellUsable.CooldownRemaining(SpellA, atTimestamp: 2000);
        Assert.InRange(remaining, 8999, 9001);
        Assert.Equal(1.0, spellUsable.EffectiveRate(SpellA), precision: 6);
    }

    /// <summary>
    /// A category-scoped acceleration modifier added mid-flight rescales only the in-flight cooldowns
    /// whose ability it covers: SpellA (Major) has its remaining 9000ms halved to 4500, while SpellB
    /// (unclassified) keeps its full 19000ms remaining.
    /// </summary>
    [Fact]
    public async Task ScopedAcceleration_RescalesOnlyMatchingInFlightCooldowns()
    {
        var (_, spellUsable, _) = await Run(
            [CreateCast(1000, SpellA), CreateCast(1000, SpellB), CreateTrigger(2000)],
            onApplyBuff: (owner, e) =>
            {
                if (e.Ability?.FSLID.Value == TriggerId)
                    owner.GetModule<StatTracker>()!.AddCooldownModifier(
                        CooldownPool.CooldownAcceleration,
                        new CooldownModifier(1.0, new[] { AbilityCategory.Major }), e);
            });

        Assert.Equal(4500, spellUsable.CooldownRemaining(SpellA, atTimestamp: 2000));
        Assert.Equal(19_000, spellUsable.CooldownRemaining(SpellB, atTimestamp: 2000));
    }

    /// <summary>Base 10s at rate 9 (1 + Chronoshift's 8.0) starts at ~1111ms.</summary>
    [Fact]
    public async Task BeginCooldown_DuringAcceleration_StartsShorter()
    {
        var (parser, spellUsable, _) = await Run([]);

        parser.GetModule<StatTracker>()!.AddCooldownModifier(
            CooldownPool.CooldownAcceleration, new CooldownModifier(8.0), timestamp: 1000);
        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        var remaining = spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000);
        Assert.InRange(remaining, 1100, 1115);
    }

    /// <summary>
    /// 2× rate active at cast (t=100) recovers the 10s base CD in 5s, ending at t=5100. At t=2600
    /// (50% recovered, 2500ms remaining) the modifier is removed: 50% of the work remains, which at
    /// base rate takes 5s, so the cooldown expires at t=7600.
    /// </summary>
    [Fact]
    public async Task AccelerationActiveAtCast_RemovedMidCooldown_PreservesProportionalProgress()
    {
        var modifier = new CooldownModifier(1.0);

        var (_, spellUsable, _) = await Run(
            [CreateTrigger(0), CreateCast(100, SpellA), CreateTrigger(2600, RemoveTriggerId)],
            onApplyBuff: (owner, e) =>
            {
                var statTracker = owner.GetModule<StatTracker>()!;
                if (e.Ability?.FSLID.Value == TriggerId)
                    statTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, modifier, e);
                if (e.Ability?.FSLID.Value == RemoveTriggerId)
                    statTracker.RemoveCooldownModifier(CooldownPool.CooldownAcceleration, modifier, e);
            });

        Assert.Equal(5000, spellUsable.CooldownRemaining(SpellA, atTimestamp: 2600));
        Assert.Equal(0, spellUsable.CooldownRemaining(SpellA, atTimestamp: 7600));
        Assert.Equal(1.0, spellUsable.EffectiveRate(SpellA), precision: 6);
    }

    /// <summary>
    /// Cast SpellB twice so both charges are spent; OverallStart must remain the first cast across the
    /// mid-flight rate change. The acceleration change runs during dispatch (triggered from an in-stream
    /// ApplyBuffEvent) so the fabricated ChangeCooldownRate event is delivered to the probe.
    /// </summary>
    [Fact]
    public async Task MultiChargeAcceleration_PreservesOverallStart()
    {
        var (_, _, probe) = await Run(
            [
                CreateCast(1000, SpellB),
                CreateCast(1500, SpellB),
                CreateTrigger(2000),
            ],
            onApplyBuff: (owner, e) =>
            {
                if (e.Ability?.FSLID.Value == TriggerId)
                    owner.GetModule<StatTracker>()!.AddCooldownModifier(
                        CooldownPool.CooldownAcceleration, new CooldownModifier(1.0), e);
            });

        var rateUpdate = probe.Updates
            .LastOrDefault(e => e.Ability.FSLID == SpellB && e.UpdateType == UpdateSpellUsableType.ChangeCooldownRate);
        Assert.NotNull(rateUpdate);
        Assert.Equal(1000, rateUpdate!.OverallStartTimestamp);
    }

    // -------------------------------------------------------------------------
    // Haste's contribution to the pool (per haste-flagged ability)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Recovery and acceleration are one mechanic sharing one pool, so Chronoshift's 8.0 adds to
    /// haste rather than multiplying it: a haste-flagged spell recovers at 1 + haste + 8, not
    /// (1 + haste) × 9. Only the overlap of the two is affected; SpellA has no haste contribution.
    /// </summary>
    [Fact]
    public async Task HasteAndAddedRecovery_CombineAdditively_NotMultiplicatively()
    {
        var (parser, spellUsable, _) = await Run([CreateHasteBuff(500)]);

        var haste = parser.GetModule<Haste>()!.Current;
        Assert.True(haste >= BuffHaste, $"expected the haste buff to apply, got {haste}");

        parser.GetModule<StatTracker>()!.AddCooldownModifier(
            CooldownPool.CooldownAcceleration, new CooldownModifier(8.0), timestamp: 1000);

        Assert.Equal(1.0 + haste + 8.0, spellUsable.EffectiveRate(SpellC), precision: 6);
        Assert.Equal(9.0, spellUsable.EffectiveRate(SpellA), precision: 6);
        Assert.True(
            spellUsable.EffectiveRate(SpellC) < (1.0 + haste) * 9.0,
            "an additive pool must recover slower than a multiplicative one would");

        spellUsable.BeginCooldown(SpellC, timestamp: 1000);
        Assert.Equal(
            (int)(CdSecondsC * 1000 / (1.0 + haste + 8.0)),
            spellUsable.CooldownRemaining(SpellC, atTimestamp: 1000));
    }

    [Fact]
    public async Task HasteAcceleratedSpell_CooldownShortenedByHaste()
    {
        // The buff applies haste before the cast; SpellC is flagged CooldownReducedByHaste, so its
        // 10s base cooldown starts as 10000 / (1 + haste).
        var (parser, spellUsable, _) = await Run(
        [
            CreateHasteBuff(500),
            CreateCast(1000, SpellC),
        ]);

        var haste = parser.GetModule<Haste>()!.Current;
        Assert.True(haste >= BuffHaste, $"expected the haste buff to apply, got {haste}");
        Assert.Equal(
            (int)(CdSecondsC * 1000 / (1 + haste)),
            spellUsable.CooldownRemaining(SpellC, atTimestamp: 1000));
    }

    [Fact]
    public async Task NonHasteSpell_CooldownUnaffectedByHaste()
    {
        // SpellA is not flagged CooldownReducedByHaste, so haste does not shorten its cooldown.
        var (_, spellUsable, _) = await Run(
        [
            CreateHasteBuff(500),
            CreateCast(1000, SpellA),
        ]);

        Assert.Equal(10000, spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000));
    }

    [Fact]
    public async Task HasteIncreaseMidCooldown_RescalesAcceleratedSpellOnly()
    {
        // A haste buff landing mid-cooldown rescales the remaining time of a haste-flagged spell,
        // while leaving a non-flagged spell's cooldown untouched. Compared against a no-buff run.
        var hasted = await Run([CreateCast(1000, SpellC), CreateCast(1000, SpellA), CreateHasteBuff(3000)]);
        var baseline = await Run([CreateCast(1000, SpellC), CreateCast(1000, SpellA)]);

        Assert.True(
            hasted.spellUsable.CooldownRemaining(SpellC, atTimestamp: 5000)
            < baseline.spellUsable.CooldownRemaining(SpellC, atTimestamp: 5000),
            "the haste increase should shorten the accelerated spell's remaining cooldown");

        Assert.Equal(
            baseline.spellUsable.CooldownRemaining(SpellA, atTimestamp: 5000),
            hasted.spellUsable.CooldownRemaining(SpellA, atTimestamp: 5000));
    }

    // -------------------------------------------------------------------------
    // Chronological flush and cast-time bookkeeping
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AdvanceCooldowns_FlushesInChronologicalOrder()
    {
        // SpellA at t=0 ends at 10000; SpellB at t=100 ends at 20100. A flush event at t=21000
        // must emit EndCooldown for SpellA before SpellB.
        var (_, _, probe) = await Run(
        [
            CreateCast(0, SpellA),
            CreateCast(100, SpellB),
            new ApplyBuffEvent
            {
                Timestamp = 21000,
                SourceId = PlayerId,
                TargetId = PlayerId,
                Ability = new Ability { FSLID = 9999, Name = "Filler" },
            },
        ]);

        var endCooldowns = probe.Updates
            .Where(e => e.UpdateType == UpdateSpellUsableType.EndCooldown)
            .ToList();
        Assert.Equal(2, endCooldowns.Count);
        Assert.Equal(SpellA, endCooldowns[0].Ability.FSLID.Value);
        Assert.Equal(SpellB, endCooldowns[1].Ability.FSLID.Value);
    }

    [Fact]
    public async Task CastTimeSpell_BeginCooldownEvent_TimestampIsCastEnd()
    {
        const int CastStart = 1000;
        const int CastEnd = 2500;

        var (_, _, probe) = await Run(
        [
            new BeginCastEvent
            {
                Timestamp = CastStart,
                SourceId = PlayerId,
                Ability = new Ability { FSLID = SpellA, Name = "Spell A" },
            },
            CreateCast(CastEnd, SpellA),
        ]);

        var beginCd = probe.Updates.FirstOrDefault(u => u.UpdateType == UpdateSpellUsableType.BeginCooldown);
        Assert.NotNull(beginCd);
        Assert.Equal(CastEnd, beginCd!.Timestamp);
        Assert.Equal(CastEnd, beginCd.ChargeStartTimestamp);
    }

    // -------------------------------------------------------------------------
    // Multi-charge cooldown reduction across the charge-restore boundary
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReduceCooldown_AcrossChargeBoundary_RestoresChargeAndCarriesRemainderToNext()
    {
        // SpellB has 2 charges on a 20s recharge. Spend both, then advance to 0.5s before the recharging
        // charge would restore. A 1.0s reduction spends 0.5s ending that charge and carries the remaining
        // 0.5s onto the next charge, wasting nothing.
        var (_, spellUsable, _) = await Run([]);

        spellUsable.BeginCooldown(SpellB, timestamp: 0);   // 1 charge left, recharge ends 20000
        spellUsable.BeginCooldown(SpellB, timestamp: 0);   // 0 charges left, same recharge in flight

        Assert.Equal(0, spellUsable.ChargesAvailable(SpellB));
        Assert.Equal(500, spellUsable.CooldownRemaining(SpellB, atTimestamp: 19500));

        var cdr = spellUsable.ReduceCooldown(SpellB, 1000, timestamp: 19500);

        Assert.Equal(1000, cdr.GeneratedMs);
        Assert.Equal(1000, cdr.AppliedMs);
        Assert.Equal(0, cdr.WastedMs);

        // One charge restored; the next charge's fresh 20s recharge is 0.5s shorter (ends at 39000).
        Assert.Equal(1, spellUsable.ChargesAvailable(SpellB));
        Assert.Equal(19500, spellUsable.CooldownRemaining(SpellB, atTimestamp: 19500));
    }

    [Fact]
    public async Task ReduceCooldown_ExactlyEndingCurrentCharge_LeavesNextChargeAtFullRecharge()
    {
        var (_, spellUsable, _) = await Run([]);
        spellUsable.BeginCooldown(SpellB, timestamp: 0);
        spellUsable.BeginCooldown(SpellB, timestamp: 0);

        // Reduction exactly equal to the 500ms remaining: ends the charge with nothing to carry over.
        var cdr = spellUsable.ReduceCooldown(SpellB, 500, timestamp: 19500);

        Assert.Equal(500, cdr.AppliedMs);
        Assert.Equal(0, cdr.WastedMs);
        Assert.Equal(1, spellUsable.ChargesAvailable(SpellB));
        Assert.Equal(20000, spellUsable.CooldownRemaining(SpellB, atTimestamp: 19500));
    }

    [Fact]
    public async Task ReduceCooldown_OverflowingPastMaxCharges_WastesTheExcess()
    {
        var (_, spellUsable, _) = await Run([]);
        spellUsable.BeginCooldown(SpellB, timestamp: 0);
        spellUsable.BeginCooldown(SpellB, timestamp: 0);

        // At 19500, charge 1 has 0.5s left and charge 2 needs a further 20s. A 25s reduction restores
        // both (0.5s + 20s = 20.5s applied); the final 4.5s has no running cooldown left and is wasted.
        var cdr = spellUsable.ReduceCooldown(SpellB, 25000, timestamp: 19500);

        Assert.Equal(25000, cdr.GeneratedMs);
        Assert.Equal(20500, cdr.AppliedMs);
        Assert.Equal(4500, cdr.WastedMs);
        Assert.Equal(2, spellUsable.ChargesAvailable(SpellB));
        Assert.False(spellUsable.IsOnCooldown(SpellB));
    }

    // -------------------------------------------------------------------------
    // Test infrastructure
    // -------------------------------------------------------------------------

    private static async Task<(TestCombatLogParser parser, SpellUsable spellUsable, UpdateProbeModule probe)> Run(
        List<Event> events,
        Action<CombatLogParser, ApplyBuffEvent>? onApplyBuff = null)
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
            typeof(UpdateProbeModule),
        ];

        var parser = new TestCombatLogParser(emitter, provider, moduleTypes) { OnApplyBuff = onApplyBuff };
        await parser.Analyze(events, PlayerId, fight: TestFight);

        return (parser, parser.GetModule<SpellUsable>()!, parser.GetModule<UpdateProbeModule>()!);
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

    private static ApplyBuffEvent CreateHasteBuff(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = RimeSpells.WrathOfWinterBuff.FSLID, Name = "Wrath of Winter" },
    };

    private static ApplyBuffEvent CreateTrigger(int timestamp, int triggerId = TriggerId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = triggerId, Name = "ModifierTrigger" },
    };

    internal sealed class TestCombatLogParser(
        EventEmitter emitter,
        IServiceProvider provider,
        Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        public Action<CombatLogParser, ApplyBuffEvent>? OnApplyBuff { get; init; }

        protected override Type[] GetModuleTypes() => moduleTypes;

        protected override object? CreateInstance(Type type)
        {
            if (type == typeof(TestAbilities)) return new TestAbilities();
            if (type == typeof(UpdateProbeModule)) return new UpdateProbeModule { OnApplyBuff = OnApplyBuff };
            return base.CreateInstance(type);
        }
    }

    internal sealed class TestAbilities : Abilities
    {
        public override IEnumerable<SpellbookAbility> Spellbook() =>
        [
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = SpellA, Name = "Spell A", Cooldown = (double)CdSecondsA },
                Category = SpellCategory.Rotational,
                AbilityCategory = AbilityCategory.Major,
            },
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = SpellB, Name = "Spell B", Cooldown = (double)CdSecondsB, Charges = 2 },
                Category = SpellCategory.Rotational,
            },
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = SpellC, Name = "Spell C", Cooldown = (double)CdSecondsC },
                Category = SpellCategory.Rotational,
                CooldownReducedByHaste = true,
            },
        ];
    }

    /// <summary>Captures every <see cref="UpdateSpellUsableEvent"/> in dispatch order. Also
    /// forwards <see cref="ApplyBuffEvent"/>s to an optional callback so tests can drive
    /// SpellUsable mutations during dispatch.</summary>
    internal sealed partial class UpdateProbeModule : Analyzer
    {
        public List<UpdateSpellUsableEvent> Updates { get; } = [];

        public Action<CombatLogParser, ApplyBuffEvent>? OnApplyBuff { get; init; }

        [On<UpdateSpellUsableEvent>]
        private void OnUpdate(UpdateSpellUsableEvent e) => Updates.Add(e);

        [On<ApplyBuffEvent>(By = Actor.Player)]
        private void OnBuff(ApplyBuffEvent e) =>
            OnApplyBuff?.Invoke(Owner, e);
    }
}
