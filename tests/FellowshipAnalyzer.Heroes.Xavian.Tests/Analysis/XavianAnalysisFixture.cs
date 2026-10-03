using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Xavian.Analysis;

using Microsoft.Extensions.DependencyInjection;

namespace FellowshipAnalyzer.Heroes.Xavian.Tests.Analysis;

/// <summary>
/// Builds analyses through the real parser rather than a bare test parser, so the pull and dungeon
/// bookend normalizers run and every pull-lifetime analyzer is constructed against a real
/// <see cref="PullStartEvent"/>. Without them a pull analyzer never opens and every metric reads a silent zero.
/// </summary>
internal static class XavianAnalysisFixture
{
    public const int PlayerId = 7;
    public const int AllyId = 8;
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

    public static Task<XavianCombatLogParser> Analyze(params Event[] events) =>
        AnalyzeWithTalents([], events);

    /// <summary>Runs the parser for a player who selected <paramref name="talents"/>.</summary>
    public static Task<XavianCombatLogParser> AnalyzeWithTalents(Talent[] talents, params Event[] events) =>
        Run(talents, BossDungeon, events);

    /// <summary>Runs the parser over <paramref name="dungeon"/>, for tests that need more than one pull.</summary>
    public static Task<XavianCombatLogParser> AnalyzeIn(ReportDungeon dungeon, params Event[] events) =>
        Run([], dungeon, events);

    private static async Task<XavianCombatLogParser> Run(Talent[] talents, ReportDungeon dungeon, Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddXavianAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<XavianCombatLogParser>();
        await parser.Analyze([Combatant(talents), .. events], PlayerId, dungeon);
        return parser;
    }

    public static CombatantInfoEvent Combatant(params Talent[] talents) => new()
    {
        SourceId = PlayerId,
        Talents = [.. talents.Select(talent => new TalentInfo { Id = talent.FSLID.Value })],
    };

    public static CastEvent Cast(int timestamp, Spell spell, int targetId = EnemyId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetId == EnemyId ? 1 : null,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
    };

    public static ApplyBuffEvent ApplyBuff(int timestamp, Spell spell, int targetId = PlayerId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
    };

    public static RefreshBuffEvent RefreshBuff(int timestamp, Spell spell, int targetId = PlayerId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
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

    public static RemoveBuffEvent RemoveBuff(int timestamp, Spell spell, int targetId = PlayerId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
    };

    /// <summary>A hit the player took.</summary>
    public static DamageEvent DamageTaken(int timestamp, long amount, long absorbed = 0) => new()
    {
        Timestamp = timestamp,
        SourceId = EnemyId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = Core.Common.Spells.Xavian.Spells.Attack.FSLID, Name = "Attack" },
        AbilityGameId = Core.Common.Spells.Xavian.Spells.Attack.FSLID,
        Amount = amount,
        UnmitigatedAmount = amount + absorbed,
        Absorbed = absorbed,
    };
}
