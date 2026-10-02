using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Meiko.Analysis;

using Microsoft.Extensions.DependencyInjection;

namespace FellowshipAnalyzer.Heroes.Meiko.Tests.Analysis;

internal static class MeikoAnalysisFixture
{
    public const int PlayerId = 7;
    public const int EnemyId = 100;
    public const int PullStart = 1_000;
    public const int PullEnd = 61_000;

    public static ReportDungeon BossDungeon { get; } = new(
        Id: 0, Name: "Boss", EncounterId: 1, Kill: true,
        StartTime: 0, EndTime: 62_000, Difficulty: null,
        FriendlyPlayers: null, CompletionPercentage: null,
        InProgress: false,
        DungeonPulls: [new DungeonPull(1, 1, true, PullStart, PullEnd, "Boss", null)]);

    public static Task<MeikoCombatLogParser> Analyze(params Event[] events) =>
        AnalyzeWithTalents([], events);

    public static async Task<MeikoCombatLogParser> AnalyzeWithTalents(Talent[] talents, params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddMeikoAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<MeikoCombatLogParser>();
        await parser.Analyze([Combatant(talents), .. events], PlayerId, BossDungeon);
        return parser;
    }

    public static CombatantInfoEvent Combatant(params Talent[] talents) => new()
    {
        SourceId = PlayerId,
        Talents = [.. talents.Select(talent => new TalentInfo { Id = talent.FSLID.Value })],
    };

    public static CastEvent Cast(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        TargetInstance = 1,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
    };

    public static ApplyBuffEvent ApplyBuff(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
    };

    public static RefreshBuffEvent RefreshBuff(int timestamp, Spell spell) => new()
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

    public static RemoveBuffEvent RemoveBuff(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = spell.FSLID, Name = spell.Name },
        AbilityGameId = spell.FSLID,
    };
}
