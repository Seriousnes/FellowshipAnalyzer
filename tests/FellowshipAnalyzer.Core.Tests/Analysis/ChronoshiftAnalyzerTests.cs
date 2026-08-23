using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for <see cref="ChronoshiftAnalyzer"/>: the +800% recovery window (9× for an ability with no
/// haste contribution) is opened on the Chronoshift channel, bounded to 3 seconds (or an early
/// <see cref="EndChannelEvent"/>), and the bonus cooldown recovery it grants is attributed per ability.
/// </summary>
public sealed class ChronoshiftAnalyzerTests
{
    private const int PlayerId = 7;
    private const int BigCdId = 500;
    private const int BigCdSeconds = 30;

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
        Assert.InRange(recovery.RecoveredMs, 22_500, 23_500);
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
        Assert.InRange(recovery.RecoveredMs, 7_500, 8_500);
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
        Assert.InRange(recovery.RecoveredMs, 22_500, 23_500);
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

        var removal = parser.Events.OfType<ChangeCooldownModifierEvent>().Single(e => !e.Added);
        Assert.Equal(8000, removal.Timestamp);
    }

    /// <summary>
    /// Pins the accelerated cooldown's fast-rate expiry to the exact instant the Chronoshift modifier is
    /// removed. BigCd cast at 0 (30s) is boosted to 9x when the channel opens at 3000, which schedules its
    /// end at 6000; the fabricated channel end at 6000 removes the modifier at that same tick. Because the
    /// scheduled end only materializes for a stream event with a strictly greater timestamp, the
    /// modifier-removal reaches SpellUsable first and rescales the cooldown back to the base rate, so the
    /// EndCooldown carries the slow ~30s recharge duration rather than the stale ~3.3s fast one. A
    /// less-than-or-equal drain would fire the end at the fast rate before the removal and this fails.
    /// </summary>
    [Fact]
    public async Task ModifierRemovalCoincidingWithFastExpiry_EndsAtSlowRate()
    {
        var parser = await AnalyzeParser(
        [
            Cast(0, BigCdId),
            BeginChannel(3000),
            Filler(9000),
        ]);

        var endCooldown = parser.Events
            .OfType<UpdateSpellUsableEvent>()
            .Single(e => e.Ability.FSLID.Value == BigCdId && e.UpdateType == UpdateSpellUsableType.EndCooldown);

        Assert.Equal(6000, endCooldown.Timestamp);
        Assert.InRange(endCooldown.ExpectedRechargeDuration, 25_000, 31_000);
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
        Ability = new Ability { FSLID = Spells.Chronoshift.FSLID, Name = "Chronoshift" },
    };

    private static EndChannelEvent EndChannel(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { FSLID = Spells.Chronoshift.FSLID, Name = "Chronoshift" },
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
            return base.CreateInstance(type);
        }
    }

    private sealed class TestAbilities : Abilities
    {
        public override IEnumerable<SpellbookAbility> Spellbook() =>
        [
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = BigCdId, Name = "Big Cooldown", Cooldown = (double)BigCdSeconds },
                Category = SpellCategory.Cooldowns,
            },
        ];
    }
}
