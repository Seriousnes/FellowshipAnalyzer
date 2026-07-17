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
/// Tests for <see cref="SpellUsable"/>'s two cooldown-speed pools: the haste-driven
/// <b>acceleration</b> modifier (per haste-flagged ability) and the global <b>recovery</b> modifier
/// (set, not stacked, e.g. Chronoshift). Covers in-flight rescaling, idempotent recovery,
/// proportional progress across rate changes, and chronological flush.
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

    private static readonly ReportFight TestFight =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    // -------------------------------------------------------------------------
    // Cooldown recovery (global, set-not-stacked pool)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetRecovery_RescalesInFlightCooldown()
    {
        var (_, spellUsable, _) = await Run([CreateCast(1000, SpellA)]);

        // SpellA cast at t=1000 with base CD=10000ms → ExpectedEnd t=11000.
        // Recovery 2× at t=2000: remaining was 9000, new remaining = 9000/2 = 4500.
        spellUsable.SetCooldownRecoveryRate(2.0, timestamp: 2000);

        Assert.Equal(4500, spellUsable.CooldownRemaining(SpellA, atTimestamp: 2000));
    }

    [Fact]
    public async Task SetRecovery_ThenReset_RoundTripsCooldown()
    {
        var (_, spellUsable, _) = await Run([CreateCast(1000, SpellA)]);

        spellUsable.SetCooldownRecoveryRate(2.0, timestamp: 2000);
        spellUsable.SetCooldownRecoveryRate(1.0, timestamp: 2000);

        var remaining = spellUsable.CooldownRemaining(SpellA, atTimestamp: 2000);
        Assert.InRange(remaining, 8999, 9001);
        Assert.Equal(1.0, spellUsable.EffectiveRate(SpellA), precision: 6);
    }

    [Fact]
    public async Task SetRecovery_IsIdempotent_NeverCompounds()
    {
        // Regression guard: a recovery source (re)applied without a matching removal must not
        // compound. Setting 9× three times leaves the rate at 9×, not 9³; resetting once restores 1×.
        // (This is the Chronoshift begin/end imbalance that previously drove the rate to 9^10.)
        var (_, spellUsable, _) = await Run([]);

        spellUsable.SetCooldownRecoveryRate(9.0, timestamp: 1000);
        spellUsable.SetCooldownRecoveryRate(9.0, timestamp: 1000);
        spellUsable.SetCooldownRecoveryRate(9.0, timestamp: 1000);
        Assert.Equal(9.0, spellUsable.EffectiveRate(SpellA), precision: 6);

        spellUsable.SetCooldownRecoveryRate(1.0, timestamp: 1000);
        Assert.Equal(1.0, spellUsable.EffectiveRate(SpellA), precision: 6);
    }

    [Fact]
    public async Task BeginCooldown_DuringRecovery_StartsShorter()
    {
        var (_, spellUsable, _) = await Run([]);

        spellUsable.SetCooldownRecoveryRate(9.0, timestamp: 1000);
        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        // Base 10s ÷ 9 = ~1111ms initial duration.
        var remaining = spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000);
        Assert.InRange(remaining, 1100, 1115);
    }

    [Fact]
    public async Task RecoveryActiveAtCast_RemovedMidCooldown_PreservesProportionalProgress()
    {
        // 2× recovery active at cast → 10s base CD recovers in 5s. After 2.5s wallclock
        // (50% recovered), recovery drops to 1× → 50% of work remains, which at base rate
        // takes 5s. Cooldown should expire at t=7500.
        var (_, spellUsable, _) = await Run([]);

        spellUsable.SetCooldownRecoveryRate(2.0, timestamp: 0);
        spellUsable.BeginCooldown(SpellA, timestamp: 0);

        Assert.Equal(5000, spellUsable.CooldownRemaining(SpellA, atTimestamp: 0));
        Assert.Equal(2500, spellUsable.CooldownRemaining(SpellA, atTimestamp: 2500));

        spellUsable.SetCooldownRecoveryRate(1.0, timestamp: 2500);

        Assert.Equal(5000, spellUsable.CooldownRemaining(SpellA, atTimestamp: 2500));
        Assert.Equal(0, spellUsable.CooldownRemaining(SpellA, atTimestamp: 7500));
        Assert.Equal(1.0, spellUsable.EffectiveRate(SpellA), precision: 6);
    }

    [Fact]
    public async Task MultiChargeRecovery_PreservesOverallStart()
    {
        // Cast SpellB twice → both charges used; OverallStart should be the first cast.
        // The recovery change must run during dispatch so the fabricated ChangeCooldownRate event
        // is delivered to the probe; we trigger it from an in-stream ApplyBuffEvent.
        const int TriggerId = 8888;

        var (_, _, probe) = await Run(
            [
                CreateCast(1000, SpellB),
                CreateCast(1500, SpellB),
                new ApplyBuffEvent
                {
                    Timestamp = 2000,
                    SourceId = PlayerId,
                    TargetId = PlayerId,
                    Ability = new Ability { FSLID = TriggerId, Name = "RecoveryTrigger" },
                },
            ],
            onApplyBuff: (su, e) =>
            {
                if (e.Ability?.FSLID.Value == TriggerId)
                    su.SetCooldownRecoveryRate(2.0, e.Timestamp);
            });

        var rateUpdate = probe.Updates
            .LastOrDefault(e => e.Ability.FSLID == SpellB && e.UpdateType == UpdateSpellUsableType.ChangeCooldownRate);
        Assert.NotNull(rateUpdate);
        Assert.Equal(1000, rateUpdate!.OverallStartTimestamp);
    }

    // -------------------------------------------------------------------------
    // Cooldown acceleration (haste-driven, per haste-flagged ability)
    // -------------------------------------------------------------------------

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
    // Test infrastructure
    // -------------------------------------------------------------------------

    private static async Task<(TestCombatLogParser parser, SpellUsable spellUsable, UpdateProbeModule probe)> Run(
        List<Event> events,
        Action<SpellUsable, ApplyBuffEvent>? onApplyBuff = null)
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

    internal sealed class TestCombatLogParser(
        EventEmitter emitter,
        IServiceProvider provider,
        Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        public Action<SpellUsable, ApplyBuffEvent>? OnApplyBuff { get; init; }

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

        public Action<SpellUsable, ApplyBuffEvent>? OnApplyBuff { get; init; }

        [On<UpdateSpellUsableEvent>]
        private void OnUpdate(UpdateSpellUsableEvent e) => Updates.Add(e);

        [On<ApplyBuffEvent>(By = Actor.Player)]
        private void OnBuff(ApplyBuffEvent e) =>
            OnApplyBuff?.Invoke(Owner.GetModule<SpellUsable>()!, e);
    }
}
