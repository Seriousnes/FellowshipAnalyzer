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
/// Tests for <see cref="ChronoshiftAnalyzer"/>: the +800% recovery window (9× for an ability with no
/// haste contribution) is opened on the Chronoshift channel, bounded to 3 seconds (or an early
/// <see cref="EndChannelEvent"/>), and the bonus cooldown recovery it grants is attributed per ability.
/// </summary>
public sealed class ChronoshiftAnalyzerTests
{
    private const int PlayerId = 7;
    private const int BigCdId = 500;   // 30s cooldown spell
    private const int BigCdSeconds = 30;

    private static readonly ReportFight TestFight =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task FullChannel_AttributesBonusRecoveryToOnCooldownAbility()
    {
        // BigCd cast at 1000 → 30000ms remaining. Chronoshift channels [5000, 8000): at open the
        // spell has 26000ms left, which at 9× needs ~2889ms, so it comes off cooldown just inside the
        // window. Bonus recovery = 8 × 2889 ≈ 23112ms.
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
        // BigCd cast at 1000 → ends at 31000. Chronoshift channels [27000, 30000): at open the spell
        // has 4000ms left, which at 9× needs only ~444ms. Recovery past that point is not Chronoshift's
        // to claim, so the bonus is 8 × 444 ≈ 3552ms, not 8 × the full 3000ms window.
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
        // Same setup as the full channel, but cancelled at 6000 (1000ms in): bonus recovery is
        // 8 × 1000 = 8000ms, far less than a full 3-second channel would grant.
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
        // Two overlapping begins without ends must not stack the added recovery (the 9^10 bug):
        // the pair still grants exactly one window's bonus, 8 × 2889 ≈ 23112ms.
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

    // -------------------------------------------------------------------------
    // Test infrastructure
    // -------------------------------------------------------------------------

    private static async Task<ChronoshiftAnalyzer> Analyze(List<Event> events)
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
            typeof(ChronoshiftAnalyzer),
        ];

        var parser = new TestParser(emitter, provider, moduleTypes);
        await parser.Analyze(events, PlayerId, fight: TestFight);
        return parser.GetModule<ChronoshiftAnalyzer>()!;
    }

    private static CastEvent Cast(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = 11,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
        Target = null,
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
