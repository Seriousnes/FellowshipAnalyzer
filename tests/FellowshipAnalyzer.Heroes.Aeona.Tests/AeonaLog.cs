using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests;

/// <summary>
/// Event builders shaped like the Fellowship Logs stream for an Aeona player, and the parser harness.
/// Chrona and Stagger amounts are written at the raw log scale because <c>ResourceNormalizer</c>
/// divides them by 100 before dispatch.
/// </summary>
internal static class AeonaLog
{
    /// <summary>The Aeona player's actor id.</summary>
    public const int PlayerId = 1;
    /// <summary>The tank's actor id.</summary>
    public const int TankId = 17;
    /// <summary>The first ally's actor id.</summary>
    public const int AllyId = 27;
    /// <summary>The second ally's actor id.</summary>
    public const int SecondAllyId = 28;
    /// <summary>The first enemy's actor id.</summary>
    public const int EnemyId = 29;
    /// <summary>The second enemy's actor id.</summary>
    public const int SecondEnemyId = 30;
    /// <summary>The tank's maximum hit points.</summary>
    public const long TankMaxHitPoints = 40_000;
    /// <summary>The end timestamp, in milliseconds, of a pull built by <see cref="BossPull"/> or <see cref="TrashPull"/>.</summary>
    public const int PullEnd = 60_000;

    /// <summary>Every actor in the report: the player, the tank, two allies, and two enemies.</summary>
    public static readonly List<ReportActor> Actors =
    [
        new(PlayerId, "Aeona", "Player", "Aeona", null, null),
        new(TankId, "Xavian", "Player", "Xavian", null, null),
        new(AllyId, "Rime", "Player", "Rime", null, null),
        new(SecondAllyId, "Ardeos", "Player", "Ardeos", null, null),
        new(EnemyId, "Enemy", "NPC", "NPC", null, null),
        new(SecondEnemyId, "Second Enemy", "NPC", "NPC", null, null),
    ];

    /// <summary>The player's combatantinfo: talents by native id, and the legendary item by item id.</summary>
    public static CombatantInfoEvent Info(int[] talents, int? legendaryItemId = null) => new()
    {
        SourceId = PlayerId,
        Talents = [.. talents.Select(id => new TalentInfo { Id = id })],
        Gear = legendaryItemId is { } item ? [new Item { Id = item, Quality = 6 }] : [],
    };

    /// <summary>An instant cast, or the activation half of a cast-time ability.</summary>
    public static CastEvent Activation(int timestamp, Spell spell, int targetId = EnemyId, int? instance = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Activation = true,
        Ability = new Ability { Id = spell.FSLID },
    };

    /// <summary>The completion half of a cast-time ability.</summary>
    public static CastEvent Completion(int timestamp, Spell spell, int targetId = EnemyId, int? instance = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Activation = false,
        Ability = new Ability { Id = spell.FSLID },
    };

    /// <summary>A heal by the player on an ally.</summary>
    public static HealEvent Heal(int timestamp, Spell spell, int targetId, long amount, long overheal = 0) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Amount = amount,
        Overheal = overheal,
        Ability = new Ability { Id = spell.FSLID },
    };

    /// <summary>A damage hit by the player on an enemy.</summary>
    public static DamageEvent Damage(int timestamp, Spell spell, long amount, int targetId = EnemyId, int? instance = null, bool tick = false) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Amount = amount,
        Tick = tick,
        Ability = new Ability { Id = spell.FSLID },
    };

    /// <summary>A buff applied by the player.</summary>
    public static ApplyBuffEvent ApplyBuff(int timestamp, Spell spell, int targetId = PlayerId, int? absorb = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Absorb = absorb,
        Ability = new Ability { Id = spell.FSLID },
    };

    /// <summary>A buff refreshed by the player.</summary>
    public static RefreshBuffEvent RefreshBuff(int timestamp, Spell spell, int targetId = PlayerId, int? absorb = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Absorb = absorb,
        Ability = new Ability { Id = spell.FSLID },
    };

    /// <summary>A buff stack applied by the player.</summary>
    public static ApplyBuffStackEvent ApplyBuffStack(int timestamp, Spell spell, int stack, int targetId = PlayerId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Stack = stack,
        Ability = new Ability { Id = spell.FSLID },
    };

    /// <summary>A buff removed from its target.</summary>
    public static RemoveBuffEvent RemoveBuff(int timestamp, Spell spell, int targetId = PlayerId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { Id = spell.FSLID },
    };

    /// <summary>A debuff applied by the player.</summary>
    public static ApplyDebuffEvent ApplyDebuff(int timestamp, Spell spell, int targetId = EnemyId, int? instance = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Ability = new Ability { Id = spell.FSLID },
    };

    /// <summary>A debuff refreshed by the player.</summary>
    public static RefreshDebuffEvent RefreshDebuff(int timestamp, Spell spell, int targetId = EnemyId, int? instance = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Ability = new Ability { Id = spell.FSLID },
    };

    /// <summary>A debuff stack applied by the player.</summary>
    public static ApplyDebuffStackEvent ApplyDebuffStack(int timestamp, Spell spell, int stack, int targetId = EnemyId, int? instance = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Stack = stack,
        Ability = new Ability { Id = spell.FSLID },
    };

    /// <summary>A debuff removed from its target.</summary>
    public static RemoveDebuffEvent RemoveDebuff(int timestamp, Spell spell, int targetId = EnemyId, int? instance = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = instance,
        Ability = new Ability { Id = spell.FSLID },
    };

    /// <summary>A hit absorbed by one of the player's effects on an ally.</summary>
    public static AbsorbedEvent Absorbed(int timestamp, Spell spell, int targetId, long amount) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        AttackerId = EnemyId,
        Amount = amount,
        Ability = new Ability { Id = spell.FSLID },
    };

    /// <summary>An event on the tank carrying its Stagger pool and hit points.</summary>
    public static HealEvent TankStagger(int timestamp, int staggerHitPoints, long hitPoints = TankMaxHitPoints / 2) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = TankId,
        Amount = 1,
        Ability = new Ability { Id = Spells.EchoesOfRuin.FSLID },
        TargetResources = new ActorResources
        {
            HitPoints = hitPoints,
            MaxHitPoints = TankMaxHitPoints,
            Resources = [new ClassResource { Type = ResourceTypes.Stagger, Amount = staggerHitPoints * 100, Max = -100 }],
        },
    };

    /// <summary>The death of a unit.</summary>
    public static DeathEvent Death(int timestamp, int unitId, int? instance = null) => new()
    {
        Timestamp = timestamp,
        SourceId = unitId,
        TargetId = unitId,
        TargetInstance = instance,
    };

    /// <summary>A boss pull from 0 to <paramref name="end"/>.</summary>
    public static ReportDungeon BossPull(int end = PullEnd) => new(0, "Boss", 1, true, 0, end, null, null, null);

    /// <summary>A trash pull from 0 to <paramref name="end"/>.</summary>
    public static ReportDungeon TrashPull(int end = PullEnd) => new(0, "Trash", 0, null, 0, end, null, null, null);

    /// <summary>Runs the Aeona parser over <paramref name="events"/> for one dungeon.</summary>
    public static async Task<AeonaCombatLogParser> Analyze(ReportDungeon dungeon, params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        parser.Actors = Actors;
        await parser.Analyze([.. events], PlayerId, dungeon);
        return parser;
    }
}
