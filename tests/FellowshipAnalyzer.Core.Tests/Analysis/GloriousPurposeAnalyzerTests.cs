using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Items;
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
/// Tests for <see cref="GloriousPurposeAnalyzer"/>: the +200% recovery window (3× for an ability with no
/// haste contribution) is opened by the Glorious Purpose buff, bounded to 6 seconds (or an early
/// <see cref="RemoveBuffEvent"/>), extended rather than reopened by a refresh, and the bonus cooldown
/// recovery it grants is attributed per ability.
/// </summary>
public sealed class GloriousPurposeAnalyzerTests
{
    private const int PlayerId = 7;
    private const int BigCdId = 500;
    private const int BigCdSeconds = 30;

    private static readonly ReportFight TestFight =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task FullWindow_AttributesBonusRecoveryToOnCooldownAbility()
    {
        var analyzer = await Analyze(
        [
            Cast(1000, BigCdId),
            ApplyBuff(5000),
            Filler(20_000),
        ]);

        var recovery = Assert.Single(analyzer.RecoveryByAbility);
        Assert.Equal(BigCdId, recovery.SpellId);
        Assert.InRange(recovery.RecoveredMs, 11_500, 12_500);
        Assert.Equal(analyzer.TotalRecoveredMs, recovery.RecoveredMs);
    }

    [Fact]
    public async Task AbilityComingOffCooldownMidWindow_OnlyCreditsTimeSpentOnCooldown()
    {
        var analyzer = await Analyze(
        [
            Cast(1000, BigCdId),
            ApplyBuff(27_000),
            Filler(40_000),
        ]);

        var recovery = Assert.Single(analyzer.RecoveryByAbility);
        Assert.InRange(recovery.RecoveredMs, 2_400, 2_900);
    }

    [Fact]
    public async Task RemoveBuff_CancelsWindowEarly_GrantsLessRecovery()
    {
        var analyzer = await Analyze(
        [
            Cast(1000, BigCdId),
            ApplyBuff(5000),
            RemoveBuff(6000),
            Filler(20_000),
        ]);

        var recovery = Assert.Single(analyzer.RecoveryByAbility);
        Assert.InRange(recovery.RecoveredMs, 1_800, 2_200);
    }

    /// <summary>
    /// A refresh inside a live window resets its clock rather than opening a second one, and the expiry
    /// fabricated by the original application must not close the extended window at the earlier instant.
    /// </summary>
    [Fact]
    public async Task RefreshInLiveWindow_ExtendsWindowRatherThanOpeningAnother()
    {
        var analyzer = await Analyze(
        [
            Cast(1000, BigCdId),
            ApplyBuff(5000),
            RefreshBuff(8000),
            Filler(30_000),
        ]);

        var window = Assert.Single(analyzer.Windows);
        Assert.Equal(5000, window.BeginTimestamp);
        Assert.Equal(14_000, window.EndTimestamp);

        var recovery = Assert.Single(analyzer.RecoveryByAbility);
        Assert.InRange(recovery.RecoveredMs, 17_000, 17_600);
    }

    /// <summary>
    /// Once the fabricated expiry has closed a window, a later refresh has no window to extend, so it
    /// opens a second one.
    /// </summary>
    [Fact]
    public async Task RefreshAfterWindowExpired_OpensAnotherWindow()
    {
        var analyzer = await Analyze(
        [
            ApplyBuff(5000),
            RefreshBuff(30_000),
            Filler(50_000),
        ]);

        Assert.Collection(
            analyzer.Windows,
            first =>
            {
                Assert.Equal(5000, first.BeginTimestamp);
                Assert.Equal(11_000, first.EndTimestamp);
            },
            second =>
            {
                Assert.Equal(30_000, second.BeginTimestamp);
                Assert.Equal(36_000, second.EndTimestamp);
            });
    }

    [Fact]
    public async Task RepeatedApplies_DoNotCompound()
    {
        var analyzer = await Analyze(
        [
            Cast(1000, BigCdId),
            ApplyBuff(5000),
            ApplyBuff(5000),
            Filler(30_000),
        ]);

        var recovery = Assert.Single(analyzer.RecoveryByAbility);
        Assert.InRange(recovery.RecoveredMs, 11_500, 12_500);
    }

    [Fact]
    public async Task NoAbilitiesOnCooldown_GrantsNoRecovery()
    {
        var analyzer = await Analyze(
        [
            ApplyBuff(5000),
            Filler(20_000),
        ]);

        Assert.Empty(analyzer.RecoveryByAbility);
        Assert.Equal(0, analyzer.TotalRecoveredMs);
    }

    [Fact]
    public async Task UnendedBuff_ClosesWindowAtScheduledExpiry_ViaFabricatedRemoveBuff()
    {
        var parser = await AnalyzeParser(
        [
            Cast(1000, BigCdId),
            ApplyBuff(5000),
            Filler(20_000),
        ]);

        var fabricated = parser.Events.OfType<RemoveBuffEvent>().Single(e => e.Fabricated == true);
        Assert.Equal(11_000, fabricated.Timestamp);

        var removal = parser.Events.OfType<ChangeCooldownModifierEvent>().Single(e => !e.Added);
        Assert.Equal(11_000, removal.Timestamp);
    }

    [Fact]
    public async Task FatedStrikeCasts_AreCounted()
    {
        var analyzer = await Analyze(
        [
            Cast(1000, Items.FatedStrike.FSLID),
            ApplyBuff(1000),
            Cast(70_000, Items.FatedStrike.FSLID),
        ]);

        Assert.Equal(2, analyzer.Activations);
    }

    [Fact]
    public async Task NoActivationsOrWindows_ContributesNoStatisticsCard()
    {
        var analyzer = await Analyze([Cast(1000, BigCdId)]);

        Assert.Equal(0, analyzer.Activations);
        Assert.Empty(analyzer.Windows);
        Assert.Null(analyzer.Statistic);
    }

    /// <summary>
    /// A cast with no accompanying buff events still evidences the weapon, so the card is contributed
    /// on the activation alone.
    /// </summary>
    [Fact]
    public async Task ActivationWithNoBuffEvents_ContributesTheStatisticsCard()
    {
        var analyzer = await Analyze([Cast(1000, Items.FatedStrike.FSLID)]);

        Assert.Equal(1, analyzer.Activations);
        Assert.Empty(analyzer.Windows);
        Assert.NotNull(analyzer.Statistic);
    }

    [Fact]
    public async Task WindowWithNoActivation_ContributesTheStatisticsCard()
    {
        var analyzer = await Analyze(
        [
            Cast(1000, BigCdId),
            ApplyBuff(5000),
            Filler(20_000),
        ]);

        Assert.Single(analyzer.Windows);
        Assert.NotNull(analyzer.Statistic);
        Assert.Equal(StatisticCategory.Items, analyzer.StatisticCategory);
    }

    private static async Task<GloriousPurposeAnalyzer> Analyze(List<Event> events) =>
        (await AnalyzeParser(events)).GetModule<GloriousPurposeAnalyzer>()!;

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
            typeof(GloriousPurposeAnalyzer),
        ];

        var parser = new TestParser(emitter, provider, moduleTypes);
        await parser.Analyze(events, PlayerId, fight: TestFight);
        return parser;
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

    private static ApplyBuffEvent ApplyBuff(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = Items.GloriousPurpose.FSLID, Name = "Glorious Purpose" },
    };

    private static RefreshBuffEvent RefreshBuff(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = Items.GloriousPurpose.FSLID, Name = "Glorious Purpose" },
    };

    private static RemoveBuffEvent RemoveBuff(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = Items.GloriousPurpose.FSLID, Name = "Glorious Purpose" },
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
