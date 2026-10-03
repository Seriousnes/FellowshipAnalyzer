using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Vigour.Analysis;

using Microsoft.Extensions.DependencyInjection;

namespace FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis;

internal static class VigourAnalysisFixture
{
    public const int PlayerId = 1;
    public const int TankId = 2;
    public const int AllyId = 3;
    public const int EnemyId = 200;
    public const int SecondEnemyId = 201;
    public const int PullStart = 1_000;
    public const int PullEnd = 61_000;
    public const int PullDuration = PullEnd - PullStart;

    public static readonly List<ReportActor> Actors =
    [
        new(PlayerId, "Vigour", "Player", "Vigour", null, null),
        new(TankId, "Xavian", "Player", "Xavian", null, null),
        new(AllyId, "Rime", "Player", "Rime", null, null),
        new(EnemyId, "Enemy", "NPC", "NPC", null, null),
        new(SecondEnemyId, "Second Enemy", "NPC", "NPC", null, null),
    ];

    public static ReportDungeon BossDungeon { get; } = new(
        Id: 0, Name: "Boss", EncounterId: 1, Kill: true,
        StartTime: 0, EndTime: 62_000, Difficulty: null,
        FriendlyPlayers: [PlayerId, TankId, AllyId], CompletionPercentage: null,
        InProgress: false,
        DungeonPulls: [new DungeonPull(1, 1, true, PullStart, PullEnd, "Boss", null)]);

    public static Task<VigourCombatLogParser> Analyze(params Event[] events) => Analyze(Combatant(), events);

    public static async Task<VigourCombatLogParser> Analyze(CombatantInfoEvent combatant, params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddVigourAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<VigourCombatLogParser>();
        parser.Actors = Actors;
        await parser.Analyze([combatant, .. events], PlayerId, BossDungeon);
        return parser;
    }

    public static CombatantInfoEvent Combatant(params int[] nativeTalentIds) => new()
    {
        SourceId = PlayerId,
        Talents = [.. nativeTalentIds.Select(id => new TalentInfo { Id = FSLID.FromNative(SpellKind.Talent, id).Value })],
    };

    public static CastEvent Cast(int timestamp, Spell spell, int targetId = EnemyId, int? runes = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Activation = true,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
        SourceResources = runes is { } held
            ? new ActorResources { Resources = [new ClassResource { Type = ResourceTypes.Primary, Amount = held * 100, Max = 600 }] }
            : null,
    };

    public static HealEvent Heal(int timestamp, Spell spell, int targetId, long amount = 100) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
        Amount = amount,
    };

    public static DamageEvent Damage(int timestamp, Spell spell, int targetId = EnemyId, long amount = 100) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = 1,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
        Amount = amount,
        UnmitigatedAmount = amount,
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

    public static RemoveBuffEvent RemoveBuff(int timestamp, Spell spell, int targetId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
    };

    public static ApplyDebuffEvent ApplyDebuff(int timestamp, Spell spell, int targetId = EnemyId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = 1,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
    };

    public static RemoveDebuffEvent RemoveDebuff(int timestamp, Spell spell, int targetId = EnemyId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = 1,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
    };

    public static DispelEvent Dispel(int timestamp, Spell spell, int targetId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
        ExtraAbility = new Ability { FSLID = 1_009_999, Name = "Curse" },
        ExtraAbilityGameId = 1_009_999,
    };
}
