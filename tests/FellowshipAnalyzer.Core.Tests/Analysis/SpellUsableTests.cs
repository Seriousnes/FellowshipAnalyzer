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
/// Tests for <see cref="SpellUsable"/>'s cooldown-rate API: per-spell and per-all
/// Apply/Remove, multiplicative stacking, in-flight rescaling, and chronological flush.
/// </summary>
public sealed partial class SpellUsableTests
{
    private const int PlayerId = 7;
    private const int SpellA = 101;
    private const int SpellB = 102;
    private const int CdSecondsA = 10;
    private const int CdSecondsB = 20;

    private static readonly ReportFight TestFight =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task ApplyCooldownRateChange_RescalesInFlightCooldown()
    {
        var (_, spellUsable, _) = await Run([CreateCast(1000, SpellA)]);

        // SpellA cast at t=1000 with base CD=10000ms → ExpectedEnd t=11000.
        // Apply 2× rate at t=2000: remaining was 9000, new remaining = 9000/2 = 4500.
        spellUsable.ApplyCooldownRateChange(SpellA, 2.0, timestamp: 2000);

        Assert.Equal(4500, spellUsable.CooldownRemaining(SpellA, atTimestamp: 2000));
    }

    [Fact]
    public async Task ApplyThenRemove_RoundTripsCooldown()
    {
        var (_, spellUsable, _) = await Run([CreateCast(1000, SpellA)]);

        spellUsable.ApplyCooldownRateChange(SpellA, 2.0, timestamp: 2000);
        spellUsable.RemoveCooldownRateChange(SpellA, 2.0, timestamp: 2000);

        var remaining = spellUsable.CooldownRemaining(SpellA, atTimestamp: 2000);
        Assert.InRange(remaining, 8999, 9001);
        Assert.Equal(1.0, spellUsable.EffectiveRate(SpellA), precision: 6);
    }

    [Fact]
    public async Task ApplyRateChange_StacksMultiplicatively()
    {
        var (_, spellUsable, _) = await Run([CreateCast(1000, SpellA)]);

        spellUsable.ApplyCooldownRateChange(SpellA, 1.5, timestamp: 1000);
        spellUsable.ApplyCooldownRateChange(SpellA, 2.0, timestamp: 1000);

        // 1.5 × 2.0 = 3.0
        Assert.Equal(3.0, spellUsable.EffectiveRate(SpellA), precision: 6);
    }

    [Fact]
    public async Task BeginCooldown_AfterApplyToAll_StartsShorter()
    {
        var (_, spellUsable, _) = await Run([]);

        spellUsable.ApplyCooldownRateChangeToAll(9.0, timestamp: 1000);
        spellUsable.BeginCooldown(SpellA, timestamp: 1000);

        // Base 10s ÷ 9 = ~1111ms initial duration.
        var remaining = spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000);
        Assert.InRange(remaining, 1100, 1115);
    }

    [Fact]
    public async Task RemoveFromAll_RescalesBackToBase()
    {
        var (_, spellUsable, _) = await Run([CreateCast(0, SpellA)]);

        spellUsable.ApplyCooldownRateChangeToAll(9.0, timestamp: 1000);
        spellUsable.RemoveCooldownRateChangeFromAll(9.0, timestamp: 1000);

        Assert.Equal(1.0, spellUsable.EffectiveRate(SpellA), precision: 6);
        var remaining = spellUsable.CooldownRemaining(SpellA, atTimestamp: 1000);
        Assert.InRange(remaining, 8999, 9001);
    }

    [Fact]
    public async Task TwoCooldownAccelerationEffects_StackMultiplicatively()
    {
        // In-game model: two 50% cooldown acceleration effects (each making cooldowns elapse
        // 2× faster, i.e. 50% wallclock reduction) compose by multiplication, not addition.
        // (1-0.5)×(1-0.5) = 25% wallclock remaining = 75% total reduction → effective rate 4×.
        var (_, spellUsable, _) = await Run([]);

        spellUsable.ApplyCooldownRateChangeToAll(2.0, timestamp: 0);
        spellUsable.ApplyCooldownRateChangeToAll(2.0, timestamp: 0);

        Assert.Equal(4.0, spellUsable.EffectiveRate(SpellA), precision: 6);

        spellUsable.BeginCooldown(SpellA, timestamp: 0);
        // 10s base ÷ 4 = 2500ms effective.
        Assert.Equal(2500, spellUsable.CooldownRemaining(SpellA, atTimestamp: 0));
    }

    [Fact]
    public async Task GlobalAndPerSpellRateChanges_StackMultiplicatively()
    {
        // A 2× global rate composed with a 1.5× per-spell rate gives a 3× effective rate
        // for the targeted spell; other spells see only the global rate.
        var (_, spellUsable, _) = await Run([]);

        spellUsable.ApplyCooldownRateChangeToAll(2.0, timestamp: 0);
        spellUsable.ApplyCooldownRateChange(SpellA, 1.5, timestamp: 0);

        Assert.Equal(3.0, spellUsable.EffectiveRate(SpellA), precision: 6);
        Assert.Equal(2.0, spellUsable.EffectiveRate(SpellB), precision: 6);
    }

    [Fact]
    public async Task StackedRateChanges_UnwindIndependentlyOfRemoveOrder()
    {
        // Two 2× effects stack to 4×; removing them in any order restores rate 1.0
        // because each Remove inverts only its own factor.
        var (_, spellUsable, _) = await Run([]);

        spellUsable.ApplyCooldownRateChangeToAll(2.0, timestamp: 0);
        spellUsable.ApplyCooldownRateChangeToAll(2.0, timestamp: 0);
        Assert.Equal(4.0, spellUsable.EffectiveRate(SpellA), precision: 6);

        spellUsable.RemoveCooldownRateChangeFromAll(2.0, timestamp: 0);
        Assert.Equal(2.0, spellUsable.EffectiveRate(SpellA), precision: 6);

        spellUsable.RemoveCooldownRateChangeFromAll(2.0, timestamp: 0);
        Assert.Equal(1.0, spellUsable.EffectiveRate(SpellA), precision: 6);
    }

    [Fact]
    public async Task RateActiveAtCast_RemovedMidCooldown_PreservesProportionalProgress()
    {
        // 100% CA active at cast → 10s base CD recovers in 5s. After 2.5s wallclock
        // (50% recovered), CA drops to 0% → 50% of work remains, which at base rate
        // takes 5s. Cooldown should expire at t=7500.
        var (_, spellUsable, _) = await Run([]);

        spellUsable.ApplyCooldownRateChangeToAll(2.0, timestamp: 0);
        spellUsable.BeginCooldown(SpellA, timestamp: 0);

        Assert.Equal(5000, spellUsable.CooldownRemaining(SpellA, atTimestamp: 0));
        Assert.Equal(2500, spellUsable.CooldownRemaining(SpellA, atTimestamp: 2500));

        spellUsable.RemoveCooldownRateChangeFromAll(2.0, timestamp: 2500);

        Assert.Equal(5000, spellUsable.CooldownRemaining(SpellA, atTimestamp: 2500));
        Assert.Equal(0, spellUsable.CooldownRemaining(SpellA, atTimestamp: 7500));
        Assert.Equal(1.0, spellUsable.EffectiveRate(SpellA), precision: 6);
    }

    [Fact]
    public async Task RateToggledMidCooldown_PreservesProportionalProgressBothWays()
    {
        // Cast at base rate (10s recharge). At t=2500 (25% of work done), apply 2× rate.
        // 75% of work remains, scaled to 75% × 5000 = 3750ms wallclock.
        // At t=4375 (50% of new remaining elapsed = 37.5% more work done), remove 2×.
        // 37.5% of base work remains → 37.5% × 10000 = 3750ms wallclock.
        var (_, spellUsable, _) = await Run([CreateCast(0, SpellA)]);

        Assert.Equal(7500, spellUsable.CooldownRemaining(SpellA, atTimestamp: 2500));

        spellUsable.ApplyCooldownRateChangeToAll(2.0, timestamp: 2500);
        Assert.Equal(3750, spellUsable.CooldownRemaining(SpellA, atTimestamp: 2500));
        Assert.Equal(1875, spellUsable.CooldownRemaining(SpellA, atTimestamp: 4375));

        spellUsable.RemoveCooldownRateChangeFromAll(2.0, timestamp: 4375);
        Assert.Equal(3750, spellUsable.CooldownRemaining(SpellA, atTimestamp: 4375));
        Assert.Equal(1.0, spellUsable.EffectiveRate(SpellA), precision: 6);
    }

    [Fact]
    public async Task MultiChargeRateChange_PreservesOverallStart()
    {
        // Cast SpellB twice → both charges used; OverallStart should be the first cast.
        // The rate change must run during dispatch so the fabricated ChangeCooldownRate event
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
                    Ability = new Ability { Guid = TriggerId, Name = "RateChangeTrigger" },
                },
            ],
            onApplyBuff: (su, e) =>
            {
                if (e.Ability?.Guid == TriggerId)
                    su.ApplyCooldownRateChangeToAll(2.0, e.Timestamp);
            });

        var rateUpdate = probe.Updates
            .LastOrDefault(e => e.Ability.Guid == SpellB && e.UpdateType == UpdateSpellUsableType.ChangeCooldownRate);
        Assert.NotNull(rateUpdate);
        Assert.Equal(1000, rateUpdate!.OverallStartTimestamp);
    }

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
                Ability = new Ability { Guid = 9999, Name = "Filler" },
            },
        ]);

        var endCooldowns = probe.Updates
            .Where(e => e.UpdateType == UpdateSpellUsableType.EndCooldown)
            .ToList();
        Assert.Equal(2, endCooldowns.Count);
        Assert.Equal(SpellA, endCooldowns[0].Ability.Guid);
        Assert.Equal(SpellB, endCooldowns[1].Ability.Guid);
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
                Ability = new Ability { Guid = SpellA, Name = "Spell A" },
            },
            CreateCast(CastEnd, SpellA),
        ]);

        var beginCd = probe.Updates.FirstOrDefault(u => u.UpdateType == UpdateSpellUsableType.BeginCooldown);
        Assert.NotNull(beginCd);
        Assert.Equal(CastEnd, beginCd!.Timestamp);
        Assert.Equal(CastEnd, beginCd.ChargeStartTimestamp);
    }

    [Fact]
    public async Task ApplyHasteScaledRateChange_AtZeroHaste_LeavesRateUnchanged()
    {
        var (_, spellUsable, _) = await Run([]);

        var lease = spellUsable.ApplyHasteScaledRateChange(timestamp: 0);
        Assert.Equal(1.0, spellUsable.EffectiveRate(SpellA), precision: 6);

        spellUsable.RemoveHasteScaledRateChange(lease, timestamp: 0);
        Assert.Equal(1.0, spellUsable.EffectiveRate(SpellA), precision: 6);
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
        Ability = new Ability { Guid = spellId, Name = $"Spell {spellId}" },
        Target = null,
        Channel = new EndChannelEvent(),
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
