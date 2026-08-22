using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Helena.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Helena.Spells;

namespace FellowshipAnalyzer.Heroes.Helena.Tests.Analysis;

/// <summary>
/// Builds analyses through the real parser rather than a bare test parser, so the pull and dungeon
/// bookend normalizers run and every pull-lifetime analyzer is constructed against a real
/// <see cref="PullStartEvent"/>. Without them a pull analyzer never opens and every metric reads a silent zero.
/// </summary>
internal static class HelenaAnalysisFixture
{
    public const int PlayerId = 7;
    public const int EnemyId = 100;
    public const int PullStart = 1_000;
    public const int PullEnd = 61_000;

    /// <summary>A single boss pull spanning the whole dungeon, so <c>[ForPull]</c> filters match on shape.</summary>
    public static ReportDungeon BossDungeon { get; } = new(
        Id: 0, Name: "Boss", EncounterId: 1, Kill: true,
        StartTime: 0, EndTime: 62_000, Difficulty: null,
        FriendlyPlayers: null, CompletionPercentage: null,
        InProgress: false,
        DungeonPulls: [new DungeonPull(1, 1, true, PullStart, PullEnd, "Boss", null)]);

    public static Task<HelenaCombatLogParser> Analyze(params Event[] events) =>
        AnalyzeWithTalents([], events);

    /// <summary>
    /// Runs the parser for a player who selected <paramref name="talents"/>, so <c>[RequiresTalent]</c>
    /// modules are constructed. Without them a talent-gated module never exists and every metric it
    /// carries reads a silent zero.
    /// </summary>
    public static async Task<HelenaCombatLogParser> AnalyzeWithTalents(Talent[] talents, params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddHelenaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<HelenaCombatLogParser>();
        await parser.Analyze([Combatant(talents), .. events], PlayerId, BossDungeon);
        return parser;
    }

    public static CombatantInfoEvent Combatant(params Talent[] talents) => new()
    {
        SourceId = PlayerId,
        Talents = [.. talents.Select(talent => new TalentInfo { Id = talent.FSLID.Value })],
    };

    /// <summary>
    /// A player cast, optionally carrying a Toughness snapshot. Log resource values are scaled by 100
    /// and divided back down by <c>ResourceNormalizer</c>, so the raw amounts are stored multiplied.
    /// </summary>
    public static CastEvent Cast(int timestamp, Spell spell, int? toughness = null, int toughnessMax = 1_000) =>
        new()
        {
            Timestamp = timestamp,
            SourceId = PlayerId,
            TargetId = EnemyId,
            TargetInstance = 1,
            Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
            AbilityGameId = spell.FSLID,
            SourceResources = toughness is null ? null : Resources(toughness.Value, toughnessMax),
        };

    /// <summary>
    /// A hit the player took, optionally carrying a Toughness snapshot on the target. It is dealt by
    /// <see cref="PhysicalAbility"/> when <paramref name="physical"/> is set and by Helena's own Attack
    /// otherwise, which the game data leaves unclassified so a physical-only population rejects it.
    /// </summary>
    public static DamageEvent DamageTaken(
        int timestamp,
        long amount,
        long unmitigated,
        long mitigated,
        long absorbed = 0,
        long blocked = 0,
        HitType hitType = HitType.Normal,
        int? toughness = null,
        int toughnessMax = 1_000,
        bool physical = false,
        bool tick = false)
    {
        var ability = physical ? PhysicalAbility : Spells.Attack;

        return new DamageEvent
        {
            Timestamp = timestamp,
            SourceId = EnemyId,
            TargetId = PlayerId,
            Ability = new Ability { FSLID = ability.FSLID, Name = ability.Name },
            AbilityGameId = ability.FSLID,
            Amount = amount,
            UnmitigatedAmount = unmitigated,
            Mitigated = mitigated,
            Absorbed = absorbed,
            Blocked = blocked,
            HitType = hitType,
            Tick = tick,
            TargetResources = toughness is null ? null : Resources(toughness.Value, toughnessMax),
        };
    }

    /// <summary>
    /// An ability the game data classifies as Physical, so a hit built from it survives a physical-only
    /// filter. Taken from the spellbook rather than written down as an id, because the classification
    /// lives in the game-data export and moves with it.
    /// </summary>
    public static Spell PhysicalAbility => Spells.MeasuredStrikeDamage;

    /// <summary>A physical hit the player took, for the mitigation population.</summary>
    public static DamageEvent PhysicalHit(
        int timestamp,
        long amount,
        long absorbed = 0,
        long blocked = 0,
        HitType hitType = HitType.Normal,
        int? toughness = null,
        int toughnessMax = 1_000,
        bool tick = false) =>
        DamageTaken(
            timestamp, amount, amount + absorbed + blocked, 0, absorbed, blocked, hitType,
            toughness, toughnessMax, physical: true, tick: tick);

    /// <summary>A bare event carrying only a Toughness snapshot on the player, for band sampling.</summary>
    public static DamageEvent ToughnessSample(int timestamp, int toughness, int toughnessMax = 1_000) =>
        DamageTaken(timestamp, 0, 0, 0, toughness: toughness, toughnessMax: toughnessMax);

    public static ApplyBuffEvent ApplyBuff(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
    };

    public static ApplyBuffStackEvent ApplyBuffStack(int timestamp, Spell spell, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
        Stack = stack,
    };

    public static RemoveBuffStackEvent RemoveBuffStack(int timestamp, Spell spell, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
        Stack = stack,
    };

    /// <summary>Damage the player dealt with <paramref name="spell"/>.</summary>
    public static DamageEvent DamageDealt(int timestamp, Spell spell, long amount) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        TargetInstance = 1,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
        Amount = amount,
        UnmitigatedAmount = amount,
    };

    public static RemoveBuffEvent RemoveBuff(int timestamp, Spell spell, int? absorb = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
        Absorb = absorb,
    };

    public static ApplyDebuffEvent ApplyDebuff(int timestamp, Spell spell, int targetInstance = 1) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        TargetInstance = targetInstance,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
    };

    public static ApplyDebuffStackEvent ApplyDebuffStack(int timestamp, Spell spell, int stack, int targetInstance = 1) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        TargetInstance = targetInstance,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
        Stack = stack,
    };

    public static RemoveDebuffEvent RemoveDebuff(int timestamp, Spell spell, int targetInstance = 1) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        TargetInstance = targetInstance,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
    };

    private static ActorResources Resources(int toughness, int toughnessMax) => new()
    {
        Resources = [new ClassResource { Type = ResourceTypes.Secondary, Amount = toughness * 100, Max = toughnessMax * 100 }],
    };
}
