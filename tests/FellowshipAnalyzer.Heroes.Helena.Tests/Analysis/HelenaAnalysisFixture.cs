using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Helena.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Helena.Spells;

namespace FellowshipAnalyzer.Heroes.Helena.Tests.Analysis;

/// <summary>
/// Builds analyses through the real parser rather than a bare test parser, so the pull and fight
/// bookend normalizers run and every pull-lifetime analyzer is constructed against a real
/// <see cref="Pull"/>. Without them a pull analyzer never opens and every metric reads a silent zero.
/// </summary>
internal static class HelenaAnalysisFixture
{
    public const int PlayerId = 7;
    public const int EnemyId = 100;
    public const int PullStart = 1_000;
    public const int PullEnd = 61_000;

    /// <summary>A single boss pull spanning the whole fight, so <c>[ForPull]</c> filters match on shape.</summary>
    public static ReportFight BossFight { get; } = new(
        Id: 0, Name: "Boss", EncounterId: 1, Kill: true,
        StartTime: 0, EndTime: 62_000, Difficulty: null,
        FriendlyPlayers: null, FightPercentage: null,
        InProgress: false,
        DungeonPulls: [new DungeonPull(1, 1, true, PullStart, PullEnd, "Boss", null)]);

    public static async Task<HelenaCombatLogParser> Analyze(params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddHelenaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<HelenaCombatLogParser>();
        await parser.Analyze([Combatant(), .. events], PlayerId, BossFight);
        return parser;
    }

    public static CombatantInfoEvent Combatant() => new() { SourceId = PlayerId };

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

    /// <summary>A hit the player took, optionally carrying a Toughness snapshot on the target.</summary>
    public static DamageEvent DamageTaken(
        int timestamp,
        long amount,
        long unmitigated,
        long mitigated,
        long absorbed = 0,
        long blocked = 0,
        HitType hitType = HitType.Normal,
        int? toughness = null,
        int toughnessMax = 1_000) =>
        new()
        {
            Timestamp = timestamp,
            SourceId = EnemyId,
            TargetId = PlayerId,
            Ability = new Ability { FSLID = Spells.Attack.FSLID, Name = Spells.Attack.Name },
            AbilityGameId = Spells.Attack.FSLID,
            Amount = amount,
            UnmitigatedAmount = unmitigated,
            Mitigated = mitigated,
            Absorbed = absorbed,
            Blocked = blocked,
            HitType = hitType,
            TargetResources = toughness is null ? null : Resources(toughness.Value, toughnessMax),
        };

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
