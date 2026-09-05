using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Sylvie.Analysis;

using Microsoft.Extensions.DependencyInjection;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

/// <summary>
/// Builds analyses through the real parser rather than a bare test parser, so the pull and dungeon
/// bookend normalizers run and every pull-lifetime analyzer is constructed against a real
/// <see cref="PullStartEvent"/>. Without them a pull analyzer never opens and every metric reads a silent zero.
/// </summary>
internal static class SylvieAnalysisFixture
{
    public const int PlayerId = 118;
    public const int TankId = 109;
    public const int AllyId = 120;
    public const int EnemyId = 200;
    public const int PullStart = 1_000;
    public const int PullEnd = 61_000;

    /// <summary>A single boss pull spanning the whole dungeon, so <c>[ForPull]</c> filters match on shape.</summary>
    public static ReportDungeon BossDungeon { get; } = new(
        Id: 0, Name: "Boss", EncounterId: 1, Kill: true,
        StartTime: 0, EndTime: 62_000, Difficulty: null,
        FriendlyPlayers: null, CompletionPercentage: null,
        InProgress: false,
        DungeonPulls: [new DungeonPull(1, 1, true, PullStart, PullEnd, "Boss", null)]);

    public static async Task<SylvieCombatLogParser> Analyze(params Event[] events) =>
        await Analyze(Combatant(), events);

    public static async Task<SylvieCombatLogParser> Analyze(CombatantInfoEvent combatant, params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddSylvieAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<SylvieCombatLogParser>();
        await parser.Analyze([combatant, .. events], PlayerId, BossDungeon);
        return parser;
    }

    /// <summary>The selected player, optionally with the native talent ids their build ran.</summary>
    public static CombatantInfoEvent Combatant(params int[] nativeTalentIds) => new()
    {
        SourceId = PlayerId,
        Talents = [.. nativeTalentIds.Select(id => new TalentInfo { Id = FSLID.FromNative(SpellKind.Talent, id).Value })],
    };

    public static CastEvent Cast(int timestamp, Spell spell, int targetId = EnemyId, int? mana = null, int manaMax = 1_440) =>
        new()
        {
            Timestamp = timestamp,
            SourceId = PlayerId,
            TargetId = targetId,
            Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
            AbilityGameId = spell.FSLID,
            SourceResources = mana is null ? null : Mana(mana.Value, manaMax),
        };

    public static HealEvent Heal(int timestamp, Spell spell, int targetId, long amount, long overheal = 0) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
        Amount = amount,
        Overheal = overheal,
    };

    public static DamageEvent Damage(int timestamp, Spell spell, long amount, HitType hitType = HitType.Normal) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        TargetInstance = 1,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
        Amount = amount,
        UnmitigatedAmount = amount,
        HitType = hitType,
    };

    public static ApplyBuffEvent ApplyBuff(int timestamp, Spell spell, int targetId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
    };

    public static RefreshBuffEvent RefreshBuff(int timestamp, Spell spell, int targetId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
    };

    public static RemoveBuffEvent RemoveBuff(int timestamp, Spell spell, int targetId, int? absorb = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
        Absorb = absorb,
    };

    public static AbsorbedEvent Absorbed(int timestamp, Spell spell, int targetId, long amount) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
        Amount = amount,
    };

    public static DispelEvent Dispel(int timestamp, Spell spell, int targetId, Spell removed) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
        ExtraAbility = new Ability { FSLID = removed.FSLID, Name = removed.Name },
        ExtraAbilityGameId = removed.FSLID,
    };

    /// <summary>A bare event with only the player's mana on it, for pool sampling.</summary>
    public static DamageEvent ManaSample(int timestamp, int mana, int manaMax = 1_440) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        TargetInstance = 1,
        Ability = new Ability { FSLID = Core.Common.Spells.Sylvie.Spells.Nettlebolt.FSLID },
        AbilityGameId = Core.Common.Spells.Sylvie.Spells.Nettlebolt.FSLID,
        SourceResources = Mana(mana, manaMax),
    };

    /// <summary>A bare event with only the player's unassigned Flutterfly count on it.</summary>
    public static DamageEvent FlutterflySample(int timestamp, int unassigned) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        TargetInstance = 1,
        Ability = new Ability { FSLID = Core.Common.Spells.Sylvie.Spells.PricklyVineDamage.FSLID },
        AbilityGameId = Core.Common.Spells.Sylvie.Spells.PricklyVineDamage.FSLID,
        SourceResources = new ActorResources
        {
            Resources = [new ClassResource { Type = ResourceTypes.Tertiary, Amount = unassigned * 100, Max = 400 }],
        },
    };

    /// <summary>Log resource values are scaled by 100 and divided back down by <c>ResourceNormalizer</c>.</summary>
    private static ActorResources Mana(int mana, int manaMax) => new()
    {
        Resources = [new ClassResource { Type = ResourceTypes.Mana, Amount = mana * 100, Max = manaMax * 100 }],
    };
}
