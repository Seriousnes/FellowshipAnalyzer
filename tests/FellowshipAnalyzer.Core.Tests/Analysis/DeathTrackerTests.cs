using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Analysis.Deaths;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for <see cref="DeathTracker"/>: the lead-up window it captures around each death of the
/// analyzed player, the prevention accounting it derives from that window, and what it reports about
/// the player's defensive and healing abilities at the moment they died.
/// </summary>
public sealed class DeathTrackerTests
{
    private const int PlayerId = 7;
    private const int BossId = 20;

    private const int Cleave = 5001;
    private const int Slam = 5002;

    private const int ShortDefensive = 101;
    private const int LongDefensive = 102;
    private const int DefensiveBuff = 1_000_102;
    private const int FreeHeal = 103;
    private const int Filler = 104;

    private const int PullStart = 40_000;
    private const int DeathAt = 50_000;

    private static readonly ReportFight TestFight =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 120_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task Window_KeepsOnlyTheHitsInsideIt()
    {
        var tracker = await Analyze(
        [
            Hit(DeathAt - 20_000, Cleave, amount: 1_000),
            Hit(DeathAt - 14_000, Cleave, amount: 2_000),
            Hit(DeathAt, Slam, amount: 9_000),
            Death(DeathAt),
        ]);

        var death = tracker.Deaths.ShouldHaveSingleItem();

        death.WindowStart.ShouldBe(DeathAt - DeathTracker.RecapWindowMs);
        death.HitCount.ShouldBe(2);
        death.DamageTaken.ShouldBe(11_000);
        death.Hits[0].Timestamp.ShouldBe(DeathAt - 14_000);
    }

    [Fact]
    public async Task Window_ClampsToThePullStart()
    {
        var tracker = await Analyze(
        [
            Hit(PullStart - 1_000, Cleave, amount: 500),
            Hit(PullStart + 500, Cleave, amount: 700),
            Death(PullStart + 5_000),
        ],
            pullStart: PullStart,
            pullEnd: PullStart + 20_000);

        var death = tracker.Deaths.ShouldHaveSingleItem();

        death.WindowStart.ShouldBe(PullStart);
        death.HitCount.ShouldBe(1);
        death.DamageTaken.ShouldBe(700);
        death.Pull.ShouldNotBeNull().StartTime.ShouldBe(PullStart);
    }

    /// <summary>
    /// A block labels part of the damage-reduction figure rather than adding a third bucket, so the
    /// damage that went missing between what the hits carried and what landed is exactly damage
    /// reduction plus absorbs. Adding <see cref="IncomingHit.Blocked"/> in would break that identity.
    /// </summary>
    [Fact]
    public async Task Mitigation_AccountsForABlockWithoutDoubleCountingIt()
    {
        var tracker = await Analyze(
        [
            Hit(DeathAt - 5_000, Cleave, amount: 4_000, unmitigated: 10_000, damageReduction: 4_000, absorbed: 2_000),
            Hit(DeathAt - 2_000, Slam, amount: 1_000, unmitigated: 9_000, damageReduction: 8_000, blocked: 8_000, hitType: HitType.Block),
            Death(DeathAt),
        ]);

        var death = tracker.Deaths.ShouldHaveSingleItem();

        death.RawIncoming.ShouldBe(19_000);
        death.DamageTaken.ShouldBe(5_000);
        death.Mitigated.ShouldBe(14_000);
        (death.RawIncoming - death.DamageTaken).ShouldBe(death.Mitigated);
        death.Blocked.ShouldBe(8_000);
        death.BlockedHits.ShouldBe(1);
        death.MitigatedShare.ShouldBe(14_000 / 19_000d, 0.0001);
    }

    /// <summary>
    /// The rolling buffer is trimmed against whichever event most recently arrived, which is not always a
    /// hit. A heal landing just before the death must not carry the trim past the window's own floor and
    /// take the oldest hit with it.
    /// </summary>
    [Fact]
    public async Task Window_KeepsTheOldestHitWhenAHealArrivesJustBeforeTheDeath()
    {
        var tracker = await Analyze(
        [
            Hit(DeathAt - DeathTracker.RecapWindowMs, Cleave, amount: 1_200),
            Heal(DeathAt - 1, amount: 300),
            Death(DeathAt),
        ]);

        var death = tracker.Deaths.ShouldHaveSingleItem();

        death.HitCount.ShouldBe(1);
        death.Hits[0].Timestamp.ShouldBe(DeathAt - DeathTracker.RecapWindowMs);
        death.DamageTaken.ShouldBe(1_200);
        death.HealingReceived.ShouldBe(300);
    }

    [Fact]
    public async Task BySource_SplitsTheWindowByAbilityHeaviestFirst()
    {
        var tracker = await Analyze(
        [
            Hit(DeathAt - 6_000, Cleave, amount: 1_000),
            Hit(DeathAt - 5_000, Slam, amount: 7_000),
            Hit(DeathAt - 4_000, Cleave, amount: 1_500),
            Death(DeathAt),
        ]);

        var death = tracker.Deaths.ShouldHaveSingleItem();

        death.BySource.Count.ShouldBe(2);
        death.BySource[0].AbilityId.ShouldBe(Slam);
        death.BySource[0].Taken.ShouldBe(7_000);
        death.BySource[1].AbilityId.ShouldBe(Cleave);
        death.BySource[1].Hits.ShouldBe(2);
        death.BySource[1].Taken.ShouldBe(2_500);
    }

    [Fact]
    public async Task HealingReceived_SumsEffectiveHealingInsideTheWindow()
    {
        var tracker = await Analyze(
        [
            Heal(DeathAt - 20_000, amount: 5_000),
            Heal(DeathAt - 3_000, amount: 4_000, overheal: 1_000),
            Heal(DeathAt - 1_000, amount: 2_500),
            Death(DeathAt),
        ]);

        tracker.Deaths.ShouldHaveSingleItem().HealingReceived.ShouldBe(6_500);
    }

    /// <summary>
    /// Health at the window's start and the maximum it is read against must come from the same snapshot,
    /// or a fight where maximum health moves renders a ratio of two unrelated numbers.
    /// </summary>
    [Fact]
    public async Task HitPointsAtWindowStart_AddsBackTheDamageTheFirstHitRemoved_AndPairsWithThatHitsMaximum()
    {
        var tracker = await Analyze(
        [
            Hit(DeathAt - 6_000, Cleave, amount: 3_000, hitPointsAfter: 47_000, maxHitPoints: 60_000),
            Hit(DeathAt - 1_000, Slam, amount: 40_000, hitPointsAfter: 7_000, maxHitPoints: 48_000),
            Death(DeathAt),
        ]);

        var death = tracker.Deaths.ShouldHaveSingleItem();

        death.HitPointsAtWindowStart.ShouldBe(50_000);
        death.MaxHitPoints.ShouldBe(60_000);
    }

    [Fact]
    public async Task HitPointsAtWindowStart_IsNullWhenNoHitCarriedASnapshot()
    {
        var tracker = await Analyze(
        [
            Hit(DeathAt - 2_000, Cleave, amount: 3_000),
            Death(DeathAt),
        ]);

        var death = tracker.Deaths.ShouldHaveSingleItem();

        death.HitPointsAtWindowStart.ShouldBeNull();
        death.MaxHitPoints.ShouldBeNull();
    }

    [Fact]
    public async Task Defensive_OnCooldownAtTheDeath_IsNotReportedUnused()
    {
        var tracker = await Analyze(
        [
            Cast(DeathAt - 40_000, LongDefensive),
            Death(DeathAt),
        ]);

        var death = tracker.Deaths.ShouldHaveSingleItem();
        var longDefensive = Find(death, LongDefensive);

        longDefensive.ChargesAvailable.ShouldBe(0);
        longDefensive.Unpressed.ShouldBeFalse();
        death.AvailableUnused.ShouldNotContain(longDefensive);
        death.AvailableUnused.Select(entry => entry.Ability.PrimarySpell.Id).ShouldContain(ShortDefensive);
    }

    [Fact]
    public async Task Defensive_CastInsideTheWindow_IsReportedPressedRatherThanUnused()
    {
        var tracker = await Analyze(
        [
            Cast(DeathAt - 3_000, ShortDefensive),
            Death(DeathAt),
        ]);

        var death = tracker.Deaths.ShouldHaveSingleItem();
        var pressed = Find(death, ShortDefensive);

        pressed.Pressed.ShouldBeTrue();
        pressed.CastsInWindow.ShouldBe(1);
        pressed.LastCastTimestamp.ShouldBe(DeathAt - 3_000);
        pressed.Unpressed.ShouldBeFalse();
        death.Pressed.ShouldContain(pressed);
        death.AvailableUnused.ShouldNotContain(pressed);
    }

    /// <summary>
    /// A cast-time ability logs the press carrying <c>Activation</c> and the completion a cast time
    /// later without it. Both are real events at different timestamps, so counting them both would report
    /// one press as two. The press is the moment worth reporting.
    /// </summary>
    [Fact]
    public async Task Defensive_WithACastTime_CountsThePressOnceAtItsStart()
    {
        var tracker = await Analyze(
        [
            Cast(DeathAt - 5_000, ShortDefensive, activation: true),
            Cast(DeathAt - 3_500, ShortDefensive),
            Death(DeathAt),
        ]);

        var pressed = Find(tracker.Deaths.ShouldHaveSingleItem(), ShortDefensive);

        pressed.CastsInWindow.ShouldBe(1);
        pressed.LastCastTimestamp.ShouldBe(DeathAt - 5_000);
    }

    /// <summary>
    /// An instant ability logs one cast and it carries <c>Activation</c>, which is how nearly every cast
    /// in a real log arrives. Treating the flag as a marker to skip would discard almost every press.
    /// </summary>
    [Fact]
    public async Task Defensive_CastInstantly_CountsAsAPress()
    {
        var tracker = await Analyze(
        [
            Cast(DeathAt - 4_000, ShortDefensive, activation: true),
            Death(DeathAt),
        ]);

        var pressed = Find(tracker.Deaths.ShouldHaveSingleItem(), ShortDefensive);

        pressed.CastsInWindow.ShouldBe(1);
        pressed.Pressed.ShouldBeTrue();
        pressed.LastCastTimestamp.ShouldBe(DeathAt - 4_000);
    }

    /// <summary>
    /// Folding a completion into a press is bounded by time, so a second press of the same ability long
    /// after the first is never swallowed by it.
    /// </summary>
    [Fact]
    public async Task Defensive_CastTwice_CountsBothPresses()
    {
        var tracker = await Analyze(
        [
            Cast(DeathAt - 12_000, ShortDefensive, activation: true),
            Cast(DeathAt - 2_000, ShortDefensive),
            Death(DeathAt),
        ]);

        var pressed = Find(tracker.Deaths.ShouldHaveSingleItem(), ShortDefensive);

        pressed.CastsInWindow.ShouldBe(2);
        pressed.LastCastTimestamp.ShouldBe(DeathAt - 2_000);
    }

    [Fact]
    public async Task Defensive_StillActiveFromAPressBeforeTheWindow_IsReportedActiveRatherThanUnused()
    {
        var tracker = await Analyze(
        [
            Cast(DeathAt - 40_000, LongDefensive),
            ApplyBuff(DeathAt - 40_000, DefensiveBuff),
            Death(DeathAt),
        ]);

        var death = tracker.Deaths.ShouldHaveSingleItem();
        var active = Find(death, LongDefensive);

        active.ActiveAtDeath.ShouldBeTrue();
        active.CastsInWindow.ShouldBe(0);
        active.Unpressed.ShouldBeFalse();
        death.ActiveAtDeath.ShouldContain(active);
    }

    [Fact]
    public async Task Defensives_ExcludeNonMitigationAbilitiesAndOnesWithoutACooldown()
    {
        var tracker = await Analyze([Death(DeathAt)]);

        var death = tracker.Deaths.ShouldHaveSingleItem();
        var ids = death.Defensives.Select(entry => entry.Ability.PrimarySpell.Id).ToList();

        ids.ShouldContain(ShortDefensive);
        ids.ShouldContain(LongDefensive);
        ids.ShouldNotContain(FreeHeal);
        ids.ShouldNotContain(Filler);
    }

    [Fact]
    public async Task KillingBlow_AndKiller_ComeFromTheDeathEvent()
    {
        var tracker = await Analyze(
        [
            Hit(DeathAt, Slam, amount: 40_000),
            Death(DeathAt),
        ]);

        var death = tracker.Deaths.ShouldHaveSingleItem();

        death.KillingBlow.Id.ShouldBe(Slam);
        death.KillingBlow.Name.ShouldBe("Slam");
        death.KillerId.ShouldBe(BossId);
        death.Timestamp.ShouldBe(DeathAt);
        death.Hits.ShouldHaveSingleItem().Ability.Id.ShouldBe(Slam);
    }

    [Fact]
    public async Task AnotherPlayersDeath_IsNotCaptured()
    {
        var tracker = await Analyze(
        [
            Hit(DeathAt - 1_000, Cleave, amount: 1_000),
            new DeathEvent
            {
                Timestamp = DeathAt,
                SourceId = BossId,
                TargetId = 99,
                Ability = new Ability { FSLID = Slam, Name = "Slam" },
            },
        ]);

        tracker.Deaths.ShouldBeEmpty();
    }

    [Fact]
    public async Task FightWithNoDeaths_YieldsAnEmptyList()
    {
        var tracker = await Analyze([Hit(DeathAt, Cleave, amount: 1_000)]);

        tracker.Deaths.ShouldBeEmpty();
    }

    [Fact]
    public async Task For_FiltersTheDeathsToOnePull()
    {
        var tracker = await Analyze(
        [
            Death(PullStart + 5_000),
            Death(PullStart + 30_000),
        ],
            pullStart: PullStart,
            pullEnd: PullStart + 20_000);

        tracker.Deaths.Count.ShouldBe(2);

        var pull = tracker.Deaths[0].Pull.ShouldNotBeNull();
        tracker.For(pull).ShouldHaveSingleItem().Timestamp.ShouldBe(PullStart + 5_000);
        tracker.For(null).Count.ShouldBe(2);
        tracker.Deaths[1].Pull.ShouldBeNull();
    }

    [Fact]
    public void UnclassifiedEnemyAbility_HasNoCuratedEntry()
    {
        EnemyAbilities.MaybeGet(Slam).ShouldBeNull();
    }

    [Fact]
    public void EnemyAbilityTags_FollowTheCuratedFlags()
    {
        var untagged = new EnemyAbility { AbilityId = Slam, Name = "Slam" };
        untagged.Tags.ShouldBeEmpty();

        var tagged = new EnemyAbility
        {
            AbilityId = Cleave,
            Name = "Cleave",
            Avoidable = true,
            ShouldMitigate = true,
        };

        tagged.Tags.ShouldBe([EnemyAbilityTag.Avoidable, EnemyAbilityTag.ShouldMitigate]);
    }

    private static DefensiveReadiness Find(PlayerDeath death, int spellId) =>
        death.Defensives.Single(entry => entry.Ability.PrimarySpell.Id == spellId);

    private static async Task<DeathTracker> Analyze(
        List<Event> events,
        int? pullStart = null,
        int? pullEnd = null)
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
            typeof(DeathTracker),
        ];

        var stream = new List<Event>(events);
        if (pullStart is { } start && pullEnd is { } end)
        {
            var pull = new Pull(
                Index: 0, Id: 1, Name: "Test Pull", StartTime: start, EndTime: end,
                Targets: PullKind.Single, IsBoss: true, Kill: false, TargetCount: 1);

            stream.Add(new PullStartEvent { Timestamp = start, Pull = pull });
            stream.Add(new PullEndEvent { Timestamp = end, Pull = pull });
        }

        stream.Add(new FightEndEvent { Timestamp = (int)TestFight.EndTime });

        var parser = new TestParser(emitter, provider, moduleTypes);
        await parser.Analyze([.. stream.OrderBy(e => e.Timestamp)], PlayerId, fight: TestFight);
        return parser.GetModule<DeathTracker>()!;
    }

    private static DamageEvent Hit(
        int timestamp,
        int spellId,
        long amount,
        long? unmitigated = null,
        long damageReduction = 0,
        long absorbed = 0,
        long blocked = 0,
        HitType hitType = HitType.Normal,
        long? hitPointsAfter = null,
        long? maxHitPoints = null) => new()
        {
            Timestamp = timestamp,
            SourceId = BossId,
            TargetId = PlayerId,
            Ability = new Ability { FSLID = spellId, Name = spellId == Cleave ? "Cleave" : "Slam" },
            Amount = amount,
            UnmitigatedAmount = unmitigated ?? amount,
            Mitigated = damageReduction,
            Absorbed = absorbed,
            Blocked = blocked,
            HitType = hitType,
            TargetResources = hitPointsAfter is null && maxHitPoints is null
                ? null
                : new ActorResources
                {
                    HitPoints = hitPointsAfter ?? 0,
                    MaxHitPoints = maxHitPoints ?? 0,
                },
        };

    private static HealEvent Heal(int timestamp, long amount, long overheal = 0) => new()
    {
        Timestamp = timestamp,
        SourceId = 8,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = 900, Name = "Mend" },
        Amount = amount,
        Overheal = overheal,
    };

    private static CastEvent Cast(int timestamp, int spellId, bool activation = false) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Spell {spellId}" },
        Activation = activation,
    };

    private static ApplyBuffEvent ApplyBuff(int timestamp, int spellId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spellId, Name = $"Buff {spellId}" },
    };

    private static DeathEvent Death(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = BossId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = Slam, Name = "Slam" },
    };

    private sealed class TestParser(EventEmitter emitter, IServiceProvider provider, Type[] moduleTypes)
        : CombatLogParser(emitter, provider)
    {
        protected override Type[] GetModuleTypes() => moduleTypes;

        protected override object? CreateInstance(Type type) =>
            type == typeof(TestAbilities) ? new TestAbilities() : base.CreateInstance(type);
    }

    private sealed class TestAbilities : Abilities
    {
        public override IEnumerable<SpellbookAbility> Spellbook() =>
        [
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = ShortDefensive, Name = "Short Defensive", Cooldown = 30d },
                Category = SpellCategory.Defensive,
            },
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = LongDefensive, Name = "Long Defensive", Cooldown = 120d },
                AdditionalSpells = [new Spell { Id = DefensiveBuff, Name = "Long Defensive" }],
                Category = SpellCategory.Cooldowns,
                IsDefensive = true,
            },
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = FreeHeal, Name = "Free Heal" },
                Category = SpellCategory.Healing,
            },
            new SpellbookAbility
            {
                PrimarySpell = new Spell { Id = Filler, Name = "Filler", Cooldown = 8d },
                Category = SpellCategory.Rotational,
            },
        ];
    }
}
