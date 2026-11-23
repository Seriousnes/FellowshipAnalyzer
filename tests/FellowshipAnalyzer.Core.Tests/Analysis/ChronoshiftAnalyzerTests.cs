using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for <see cref="ChronoshiftAnalyzer"/>: the 8× Cooldown Recovery window (a modifier of 8 on the
/// Cooldown Recovery attribute, 8× for an ability whose time rate scales off it) is opened on the Chronoshift
/// channel, bounded to 3 seconds (or an early <see cref="EndChannelEvent"/>), and the bonus cooldown recovery
/// it grants is attributed per ability.
/// </summary>
public sealed partial class ChronoshiftAnalyzerTests
{
    private const int PlayerId = 7;
    private const int BigCdId = 500;
    private const int BigCdSeconds = 30;
    private const int StaticCdId = 501;
    private const int StaticCdSeconds = 20;

    private static readonly ReportDungeon TestDungeon =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    [Fact]
    public async Task FullChannel_AttributesBonusRecoveryToOnCooldownAbility()
    {
        var analyzer = await Analyze(
        [
            Cast(1000, BigCdId),
            BeginChannel(5000),
            Filler(9000),
        ]);

        var recovery = Assert.Single(analyzer.RecoveryByAbility);
        Assert.Equal(BigCdId, recovery.SpellId);
        Assert.InRange(recovery.RecoveredMs, 20_500, 21_500);
        Assert.Equal(analyzer.TotalRecoveredMs, recovery.RecoveredMs);
    }

    [Fact]
    public async Task AbilityComingOffCooldownMidChannel_OnlyCreditsTimeSpentOnCooldown()
    {
        var analyzer = await Analyze(
        [
            Cast(1000, BigCdId),
            BeginChannel(27_000),
            Filler(31_000),
        ]);

        var recovery = Assert.Single(analyzer.RecoveryByAbility);
        Assert.InRange(recovery.RecoveredMs, 3_200, 3_800);
    }

    [Fact]
    public async Task EndChannel_CancelsWindowEarly_GrantsLessRecovery()
    {
        var analyzer = await Analyze(
        [
            Cast(1000, BigCdId),
            BeginChannel(5000),
            EndChannel(6000),
            Filler(9000),
        ]);

        var recovery = Assert.Single(analyzer.RecoveryByAbility);
        Assert.InRange(recovery.RecoveredMs, 6_500, 7_500);
    }

    [Fact]
    public async Task RepeatedBeginChannels_DoNotCompound()
    {
        var analyzer = await Analyze(
        [
            Cast(1000, BigCdId),
            BeginChannel(5000),
            BeginChannel(5000),
            Filler(20_000),
        ]);

        var recovery = Assert.Single(analyzer.RecoveryByAbility);
        Assert.InRange(recovery.RecoveredMs, 20_500, 21_500);
    }

    [Fact]
    public async Task NoAbilitiesOnCooldown_GrantsNoRecovery()
    {
        var analyzer = await Analyze(
        [
            BeginChannel(5000),
            Filler(9000),
        ]);

        Assert.Empty(analyzer.RecoveryByAbility);
        Assert.Equal(0, analyzer.TotalRecoveredMs);
    }

    [Fact]
    public async Task FullyChannelled_ClosesWindowAtScheduledEnd_ViaFabricatedEndChannel()
    {
        var parser = await AnalyzeParser(
        [
            Cast(1000, BigCdId),
            BeginChannel(5000),
            Filler(9000),
        ]);

        var fabricatedEnd = parser.Events.OfType<EndChannelEvent>().Single(e => e.Fabricated == true);
        Assert.Equal(8000, fabricatedEnd.Timestamp);
        Assert.Equal(3, Items.Chronoshift.ChannelDuration);
        Assert.Equal(TimeSpan.FromSeconds(Items.Chronoshift.ChannelDuration!.Value).TotalMilliseconds, fabricatedEnd.Duration);

        var removal = parser.Events.OfType<ChangeCooldownModifierEvent>().Single(e => !e.Added);
        Assert.Equal(8000, removal.Timestamp);
    }

    /// <summary>
    /// Pins the accelerated cooldown's fast-rate expiry to the exact instant the Chronoshift modifier is
    /// removed. BigCd cast at 0 (30s) has 24s left when the channel opens at 6000 and is boosted to 8x,
    /// which schedules its end at 9000; the fabricated channel end at 9000 removes the modifier at that same
    /// tick. Because the scheduled end only materializes for a stream event with a strictly greater
    /// timestamp, the modifier-removal reaches SpellUsable first and rescales the cooldown back to the base
    /// rate, so the EndCooldown reports the slow ~30s recharge duration rather than the stale ~3.75s fast
    /// one. A less-than-or-equal drain would fire the end at the fast rate before the removal and this fails.
    /// </summary>
    [Fact]
    public async Task ModifierRemovalCoincidingWithFastExpiry_EndsAtSlowRate()
    {
        var parser = await AnalyzeParser(
        [
            Cast(0, BigCdId),
            BeginChannel(6000),
            Filler(12_000),
        ]);

        var endCooldown = parser.Events
            .OfType<UpdateSpellUsableEvent>()
            .Single(e => e.Ability.FSLID.Value == BigCdId && e.UpdateType == UpdateSpellUsableType.EndCooldown);

        Assert.Equal(9000, endCooldown.Timestamp);
        Assert.InRange(endCooldown.ExpectedRechargeDuration, 25_000, 31_000);
    }

    [Fact]
    public async Task AbilityScalingOffCooldownRecovery_RecoversAtEightTimesDuringTheChannel()
    {
        var parser = await AnalyzeParser(
        [
            Cast(5000, BigCdId),
            BeginChannel(5000),
            Filler(6000),
        ]);

        var rateChange = parser.Events
            .OfType<UpdateSpellUsableEvent>()
            .First(e => e.Ability.FSLID.Value == BigCdId && e.UpdateType == UpdateSpellUsableType.ChangeCooldownRate);

        Assert.Equal(5000, rateChange.Timestamp);
        Assert.Equal(5000 + 30_000 / 8, rateChange.ExpectedRechargeTimestamp);
        Assert.Equal(8.0, parser.GetModule<RateProbe>()!.Rates[(6000, BigCdId)], precision: 6);
    }

    [Fact]
    public async Task ChronoshiftOwnCooldown_RecoversAtTheNormalRateDuringTheChannel()
    {
        var parser = await AnalyzeParser(
        [
            Cast(5000, Items.Chronoshift.FSLID),
            BeginChannel(5000),
            Filler(6000),
        ]);

        Assert.Equal(1.0, parser.GetModule<RateProbe>()!.Rates[(6000, Items.Chronoshift.FSLID)], precision: 6);
        Assert.Empty(parser.GetModule<ChronoshiftAnalyzer>()!.RecoveryByAbility);
    }

    [Fact]
    public async Task AbilityNotScalingOffCooldownRecovery_GetsNoCreditForTheChannel()
    {
        var analyzer = await Analyze(
        [
            Cast(1000, BigCdId),
            Cast(1000, StaticCdId),
            BeginChannel(5000),
            Filler(9000),
        ]);

        var recovery = Assert.Single(analyzer.RecoveryByAbility);
        Assert.Equal(BigCdId, recovery.SpellId);
    }

    private static async Task<ChronoshiftAnalyzer> Analyze(List<Event> events) =>
        (await AnalyzeParser(events)).GetModule<ChronoshiftAnalyzer>()!;

    private static async Task<TestParser> AnalyzeParser(List<Event> events)
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
            typeof(ChronoshiftAnalyzer),
            typeof(RateProbe),
        ];

        var parser = new TestParser(emitter, provider, moduleTypes);
        await parser.Analyze(events, PlayerId, dungeon: TestDungeon);
        return parser;
    }

    private static CastEvent Cast(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = 11,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
        Channel = new EndChannelEvent(),
    };

    private static BeginChannelEvent BeginChannel(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { FSLID = Items.Chronoshift.FSLID, Name = "Chronoshift" },
    };

    private static EndChannelEvent EndChannel(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { FSLID = Items.Chronoshift.FSLID, Name = "Chronoshift" },
    };

    private static ApplyBuffEvent Filler(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = 9999, Name = "Filler" },
    };

    private sealed class TestParser(EventEmitter emitter, IServiceProvider provider, Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => moduleTypes;

        protected override object? CreateInstance(Type type)
        {
            if (type == typeof(TestAbilities)) return new TestAbilities();
            if (type == typeof(RateProbe)) return new RateProbe { RateOf = id => GetModule<SpellUsable>()!.EffectiveRate(id) };
            return base.CreateInstance(type);
        }
    }

    private sealed class TestAbilities : Abilities
    {
        public override IEnumerable<SpellbookAbility> Spellbook() =>
        [
            new SpellbookAbility
            {
                PrimarySpell = new Spell
                {
                    Id = BigCdId,
                    Name = "Big Cooldown",
                    Cooldown = (double)BigCdSeconds,
                    CooldownScaling = new() { DurationCooldownReduction = true, TimeRateCooldownRecovery = true },
                },
                Category = SpellCategory.Cooldowns,
            },
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = StaticCdId, Name = "Static Cooldown", Cooldown = (double)StaticCdSeconds },
                Category = SpellCategory.Cooldowns,
            },
            new SpellbookAbility { PrimarySpell = Items.Chronoshift, Category = SpellCategory.Cooldowns },
        ];
    }

    private sealed partial class RateProbe : Analyzer
    {
        public required Func<int, double> RateOf { get; init; }

        public Dictionary<(int Timestamp, int SpellId), double> Rates { get; } = [];

        [On<ApplyBuffEvent>(By = Actor.Player)]
        private void OnBuff(ApplyBuffEvent e)
        {
            Rates[(e.Timestamp, BigCdId)] = RateOf(BigCdId);
            Rates[(e.Timestamp, Items.Chronoshift.FSLID)] = RateOf(Items.Chronoshift.FSLID);
        }
    }
}
