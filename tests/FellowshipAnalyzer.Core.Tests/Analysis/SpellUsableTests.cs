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
    public async Task MultiChargeRateChange_PreservesOverallStart()
    {
        // Cast SpellB twice → both charges used; OverallStart should be the first cast.
        var (_, spellUsable, probe) = await Run(
        [
            CreateCast(1000, SpellB),
            CreateCast(1500, SpellB),
        ]);

        spellUsable.ApplyCooldownRateChangeToAll(2.0, timestamp: 2000);

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
        List<Event> events)
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

        var parser = new TestCombatLogParser(emitter, provider, moduleTypes);
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
        protected override Type[] GetModuleTypes() => moduleTypes;
    }

    internal sealed class TestAbilities : Abilities
    {
        public override IEnumerable<SpellbookAbility> Spellbook() =>
        [
            new SpellbookAbility
            {
                PrimarySpell = new Spell(SpellA, "Spell A"),
                Category = SpellCategory.Rotational,
                Cooldown = (double)CdSecondsA,
            },
            new SpellbookAbility
            {
                PrimarySpell = new Spell(SpellB, "Spell B"),
                Category = SpellCategory.Rotational,
                Cooldown = (double)CdSecondsB,
                Charges = 2,
            },
        ];
    }

    /// <summary>Captures every <see cref="UpdateSpellUsableEvent"/> in dispatch order.</summary>
    internal sealed partial class UpdateProbeModule : Analyzer
    {
        public List<UpdateSpellUsableEvent> Updates { get; } = [];

        [On<UpdateSpellUsableEvent>]
        private void OnUpdate(UpdateSpellUsableEvent e) => Updates.Add(e);
    }
}
