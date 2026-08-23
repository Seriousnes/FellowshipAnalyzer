using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
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

    /// <summary>Ability id of the test-local flat haste buff registered on <see cref="StatTracker"/>.</summary>
    private const int HasteBuffId = 8886;

    /// <summary>The flat haste <see cref="HasteBuffId"/> grants.</summary>
    private const double BuffHaste = 0.30;

    /// <summary>Ability id of the in-stream buff that triggers a modifier addition via the probe callback.</summary>
    private const int TriggerId = 8888;

    /// <summary>Ability id of the in-stream buff that triggers a modifier removal via the probe callback.</summary>
    private const int RemoveTriggerId = 8887;

    private static readonly ReportDungeon TestDungeon =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    /// <summary>
    /// SpellA cast at t=1000 with base CD=10000ms recharges to t=11000. A 1.0 acceleration modifier
    /// added at t=2000 takes the rate to 2×: remaining was 9000, new remaining = 9000/2 = 4500, so the
    /// rescaled expiry moves to t=6500.
    /// </summary>
    [Fact]
    public async Task AddedAcceleration_RescalesInFlightCooldown()
    {
        var (_, _, probe) = await Run(
            [CreateCast(1000, SpellA), CreateTrigger(2000)],
            onApplyBuff: (owner, e) =>
            {
                if (e.Ability?.FSLID.Value == TriggerId)
                    owner.GetModule<StatTracker>()!.AddCooldownModifier(
                        CooldownPool.CooldownAcceleration, new CooldownModifier(1.0), e);
            });

        Assert.Equal(6500, LastUpdate(probe, SpellA, UpdateSpellUsableType.ChangeCooldownRate).ExpectedRechargeTimestamp);
    }

    /// <summary>
    /// Adding then removing the same acceleration modifier at the same timestamp round-trips the
    /// in-flight cooldown back to its original remaining time and rate.
    /// </summary>
    [Fact]
    public async Task AddedAcceleration_ThenRemoved_RoundTripsCooldown()
    {
        var modifier = new CooldownModifier(1.0);

        var (_, spellUsable, probe) = await Run(
            [CreateCast(1000, SpellA), CreateTrigger(2000)],
            onApplyBuff: (owner, e) =>
            {
                if (e.Ability?.FSLID.Value != TriggerId) return;
                var statTracker = owner.GetModule<StatTracker>()!;
                statTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, modifier, e);
                statTracker.RemoveCooldownModifier(CooldownPool.CooldownAcceleration, modifier, e);
            });

        Assert.Equal(11_000, LastUpdate(probe, SpellA, UpdateSpellUsableType.BeginCooldown).ExpectedRechargeTimestamp);
        Assert.DoesNotContain(probe.Updates, e => e.Ability.FSLID == SpellA
            && e.UpdateType == UpdateSpellUsableType.ChangeCooldownRate);
        Assert.Equal(1.0, spellUsable.EffectiveRate(SpellA), precision: 6);
    }

    /// <summary>
    /// A category-scoped acceleration modifier added mid-flight rescales only the in-flight cooldowns
    /// whose ability it covers: SpellA (Major) has its remaining 9000ms halved to 4500 (expiry moves to
    /// t=6500), while SpellB (unclassified) is never rescaled and keeps its original t=21000 expiry.
    /// </summary>
    [Fact]
    public async Task ScopedAcceleration_RescalesOnlyMatchingInFlightCooldowns()
    {
        var (_, _, probe) = await Run(
            [CreateCast(1000, SpellA), CreateCast(1000, SpellB), CreateTrigger(2000)],
            onApplyBuff: (owner, e) =>
            {
                if (e.Ability?.FSLID.Value == TriggerId)
                    owner.GetModule<StatTracker>()!.AddCooldownModifier(
                        CooldownPool.CooldownAcceleration,
                        new CooldownModifier(1.0, new[] { AbilityCategory.Major }), e);
            });

        Assert.Equal(6500, LastUpdate(probe, SpellA, UpdateSpellUsableType.ChangeCooldownRate).ExpectedRechargeTimestamp);
        Assert.DoesNotContain(probe.Updates, e => e.Ability.FSLID == SpellB
            && e.UpdateType == UpdateSpellUsableType.ChangeCooldownRate);
        Assert.Equal(21_000, LastUpdate(probe, SpellB, UpdateSpellUsableType.BeginCooldown).ExpectedRechargeTimestamp);
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

        var (_, spellUsable, probe) = await Run(
            [CreateTrigger(0), CreateCast(100, SpellA), CreateTrigger(2600, RemoveTriggerId)],
            onApplyBuff: (owner, e) =>
            {
                var statTracker = owner.GetModule<StatTracker>()!;
                if (e.Ability?.FSLID.Value == TriggerId)
                    statTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, modifier, e);
                if (e.Ability?.FSLID.Value == RemoveTriggerId)
                    statTracker.RemoveCooldownModifier(CooldownPool.CooldownAcceleration, modifier, e);
            });

        Assert.Equal(7600, LastUpdate(probe, SpellA, UpdateSpellUsableType.ChangeCooldownRate).ExpectedRechargeTimestamp);
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
        var (parser, _, probe) = await Run(
        [
            CreateHasteBuff(500),
            CreateCast(1000, SpellC),
        ]);

        var haste = parser.GetModule<Haste>()!.Current;
        Assert.True(haste >= BuffHaste, $"expected the haste buff to apply, got {haste}");
        Assert.Equal(
            1000 + (int)(CdSecondsC * 1000 / (1 + haste)),
            LastUpdate(probe, SpellC, UpdateSpellUsableType.BeginCooldown).ExpectedRechargeTimestamp);
    }

    [Fact]
    public async Task NonHasteSpell_CooldownUnaffectedByHaste()
    {
        var (_, _, probe) = await Run(
        [
            CreateHasteBuff(500),
            CreateCast(1000, SpellA),
        ]);

        Assert.Equal(11000, LastUpdate(probe, SpellA, UpdateSpellUsableType.BeginCooldown).ExpectedRechargeTimestamp);
    }

    [Fact]
    public async Task HasteIncreaseMidCooldown_RescalesAcceleratedSpellOnly()
    {
        var (parser, spellUsable, probe) = await Run([CreateCast(1000, SpellC), CreateCast(1000, SpellA), CreateHasteBuff(3000)]);
        var baseline = await Run([CreateCast(1000, SpellC), CreateCast(1000, SpellA)]);

        Assert.True(
            probe.Updates.Last(e => e.Ability.FSLID == SpellC).ExpectedRechargeTimestamp
            < baseline.probe.Updates.Last(e => e.Ability.FSLID == SpellC).ExpectedRechargeTimestamp,
            "the haste increase should shorten the accelerated spell's remaining cooldown");

        Assert.Equal(
            baseline.probe.Updates.Last(e => e.Ability.FSLID == SpellA).ExpectedRechargeTimestamp,
            probe.Updates.Last(e => e.Ability.FSLID == SpellA).ExpectedRechargeTimestamp);
    }

    /// <summary>
    /// SpellA cast at t=0 ends at 10000; SpellB cast at t=100 ends at 20100. Both natural expiries fall
    /// before the filler at t=21000, so they materialize in ascending expiry order: EndCooldown for SpellA
    /// before SpellB.
    /// </summary>
    [Fact]
    public async Task NaturalExpiry_FiresEndCooldownsInChronologicalOrder()
    {
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

    /// <summary>
    /// SpellA cast at 0 ends at 10000, and nothing else is logged until a filler at 21000. The EndCooldown
    /// fires at the true expiry (10000), not at the next observed event - the natural end is scheduled at
    /// its real instant rather than discovered late.
    /// </summary>
    [Fact]
    public async Task NaturalExpiry_EndCooldownEvent_FiresAtTrueExpiry()
    {
        var (_, _, probe) = await Run(
        [
            CreateCast(0, SpellA),
            new ApplyBuffEvent
            {
                Timestamp = 21000,
                SourceId = PlayerId,
                TargetId = PlayerId,
                Ability = new Ability { FSLID = 9999, Name = "Filler" },
            },
        ]);

        var endCooldown = probe.Updates.Single(e => e.UpdateType == UpdateSpellUsableType.EndCooldown);
        Assert.Equal(10000, endCooldown.Timestamp);
    }

    /// <summary>
    /// A cooldown whose true end falls in a wide gap between two logged events fires its EndCooldown at that
    /// true end (10000), never deferred to the far-away next event (50000).
    /// </summary>
    [Fact]
    public async Task NaturalExpiry_InGapBetweenEvents_FiresAtTrueExpiry()
    {
        var (_, _, probe) = await Run(
        [
            CreateCast(0, SpellA),
            new ApplyBuffEvent
            {
                Timestamp = 50000,
                SourceId = PlayerId,
                TargetId = PlayerId,
                Ability = new Ability { FSLID = 9999, Name = "Filler" },
            },
        ]);

        var endCooldown = probe.Updates.Single(e => e.UpdateType == UpdateSpellUsableType.EndCooldown);
        Assert.Equal(10000, endCooldown.Timestamp);
    }

    /// <summary>
    /// A cooldown whose true expiry falls past the final logged event is deliberately not fired: an end with
    /// no later event to precede is dead time after the dungeon and is dropped rather than dispatched in
    /// post-combat time. In a real report the appended dungeon-end bookend is the final event, so every expiry
    /// inside the dungeon still fires via the in-stream drain; only ends past it are left unfired, and the
    /// cooldown stays in flight in the tracker rather than being force-completed.
    /// </summary>
    [Fact]
    public async Task NaturalExpiry_PastFinalLoggedEvent_IsNotFired_AndCooldownLingers()
    {
        var (_, spellUsable, probe) = await Run([CreateCast(0, SpellA)]);

        Assert.DoesNotContain(probe.Updates, e => e.UpdateType == UpdateSpellUsableType.EndCooldown);
        Assert.True(spellUsable.IsOnCooldown(SpellA));
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

    [Fact]
    public async Task ReduceCooldown_AcrossChargeBoundary_RestoresChargeAndAppliesRemainderToNext()
    {
        var (_, spellUsable, _) = await Run([]);

        spellUsable.BeginCooldown(SpellB, timestamp: 0);
        spellUsable.BeginCooldown(SpellB, timestamp: 0);

        Assert.Equal(0, spellUsable.ChargesAvailable(SpellB));
        Assert.Equal(500, spellUsable.CooldownRemaining(SpellB, atTimestamp: 19500));

        var cdr = spellUsable.ReduceCooldown(SpellB, 1000, timestamp: 19500);

        Assert.Equal(1000, cdr.Total);
        Assert.Equal(1000, cdr.Effective);
        Assert.Equal(0, cdr.Wasted);

        Assert.Equal(1, spellUsable.ChargesAvailable(SpellB));
        Assert.Equal(19500, spellUsable.CooldownRemaining(SpellB, atTimestamp: 19500));
    }

    [Fact]
    public async Task ReduceCooldown_ExactlyEndingCurrentCharge_LeavesNextChargeAtFullRecharge()
    {
        var (_, spellUsable, _) = await Run([]);
        spellUsable.BeginCooldown(SpellB, timestamp: 0);
        spellUsable.BeginCooldown(SpellB, timestamp: 0);

        var cdr = spellUsable.ReduceCooldown(SpellB, 500, timestamp: 19500);

        Assert.Equal(500, cdr.Effective);
        Assert.Equal(0, cdr.Wasted);
        Assert.Equal(1, spellUsable.ChargesAvailable(SpellB));
        Assert.Equal(20000, spellUsable.CooldownRemaining(SpellB, atTimestamp: 19500));
    }

    [Fact]
    public async Task ReduceCooldown_OverflowingPastMaxCharges_WastesTheExcess()
    {
        var (_, spellUsable, _) = await Run([]);
        spellUsable.BeginCooldown(SpellB, timestamp: 0);
        spellUsable.BeginCooldown(SpellB, timestamp: 0);

        var cdr = spellUsable.ReduceCooldown(SpellB, 25000, timestamp: 19500);

        Assert.Equal(25000, cdr.Total);
        Assert.Equal(20500, cdr.Effective);
        Assert.Equal(4500, cdr.Wasted);
        Assert.Equal(2, spellUsable.ChargesAvailable(SpellB));
        Assert.False(spellUsable.IsOnCooldown(SpellB));
    }

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
            typeof(HasteBuffRegistrar),
            typeof(Combatants),
            typeof(Haste),
            typeof(SpellUsable),
            typeof(UpdateProbeModule),
        ];

        var parser = new TestCombatLogParser(emitter, provider, moduleTypes) { OnApplyBuff = onApplyBuff };
        await parser.Analyze(events, PlayerId, dungeon: TestDungeon);

        return (parser, parser.GetModule<SpellUsable>()!, parser.GetModule<UpdateProbeModule>()!);
    }

    private static UpdateSpellUsableEvent LastUpdate(UpdateProbeModule probe, int spellId, UpdateSpellUsableType updateType) =>
        probe.Updates.Last(e => e.Ability.FSLID == spellId && e.UpdateType == updateType);

    private static CastEvent CreateCast(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = 11,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
        Channel = new EndChannelEvent(),
    };

    private static ApplyBuffEvent CreateHasteBuff(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = HasteBuffId, Name = "Haste Buff" },
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
            if (type == typeof(HasteBuffRegistrar))
                return new HasteBuffRegistrar((StatTracker)ResolveAnalysisModule(typeof(StatTracker)));
            if (type == typeof(UpdateProbeModule)) return new UpdateProbeModule { OnApplyBuff = OnApplyBuff };
            return base.CreateInstance(type);
        }
    }

    /// <summary>Registers the test-local flat haste buff on <see cref="StatTracker"/> at construction time.</summary>
    internal sealed class HasteBuffRegistrar : Module
    {
        public HasteBuffRegistrar(StatTracker statTracker) =>
            statTracker.AddPercentageBuff(HasteBuffId, new StatPercentageBuff { Haste = BuffHaste });
    }

    internal sealed class TestAbilities : Abilities
    {
        public override IEnumerable<SpellbookAbility> Spellbook() =>
        [
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = SpellA, Name = "Spell A", Cooldown = (double)CdSecondsA, AbilityCategory = AbilityCategory.Major },
                Category = SpellCategory.Rotational,
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
