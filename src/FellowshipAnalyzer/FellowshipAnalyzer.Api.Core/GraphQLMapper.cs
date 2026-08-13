using FellowshipAnalyzer.Api.GraphQL;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Riok.Mapperly.Abstractions;

namespace FellowshipAnalyzer.Api.Core;

[Mapper]
public sealed partial class GraphQLMapper
{
    public Ability MapAbility(IGetReportMasterData_ReportData_Report_MasterData_Abilities source) =>
        new()
        {
            FSLID = (int)(source.GameID ?? 0),
            Name = source.Name,
            Icon = source.Icon,
        };

    public ReportActor MapActor(IGetReportMasterData_ReportData_Report_MasterData_Actors source)
        => new(source.Id ?? 0, source.Name, source.Type, source.SubType, source.Server, source.Icon);

    public ReportDungeon MapDungeon(IGetReportMasterData_ReportData_Report_Fights source)
    {
        var fp = source.FriendlyPlayers?
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();
        return new ReportDungeon(
            source.Id,
            source.Name,
            source.EncounterID,
            source.Kill,
            source.StartTime,
            source.EndTime,
            source.Difficulty,
            fp,
            source.FightPercentage,
            source.InProgress ?? false);
    }

    public DungeonNpc MapDungeonNpc(IGetReportMasterData_ReportData_Report_TargetFight_EnemyNPCs source)
        => new(source.Id ?? 0, source.GameID ?? 0, source.InstanceCount ?? 1, source.GroupCount ?? 1, source.PetOwner);

    public DungeonPull MapDungeonPull(IGetReportMasterData_ReportData_Report_TargetFight_DungeonPulls source)
    {
        var enemyNpcs = source.EnemyNPCs?
            .Where(n => n is not null)
            .Select(n => MapDungeonPullNpc(n!))
            .ToList();
        return new DungeonPull(
            source.Id,
            source.EncounterID,
            source.Kill,
            source.StartTime,
            source.EndTime,
            source.Name,
            enemyNpcs);
    }

    public DungeonPullNpc MapDungeonPullNpc(IGetReportMasterData_ReportData_Report_TargetFight_DungeonPulls_EnemyNPCs source)
        => new(
            source.Id,
            source.GameID,
            source.MinimumInstanceID,
            source.MaximumInstanceID,
            source.MinimumInstanceGroupID,
            source.MaximumInstanceGroupID);

    public AnalysisPreload MapAnalysisPreload(string reportCode, IGetReportMasterData_ReportData_Report source)
    {
        var masterData = source.MasterData
            ?? throw new InvalidOperationException("GraphQL response did not contain expected master data.");

        var abilities = masterData.Abilities?
            .Where(a => a is not null)
            .Select(a => MapAbility(a!))
            .ToList() ?? [];

        var masterActors = masterData.Actors?
            .Where(a => a is not null)
            .Select(a => MapActor(a!))
            .ToList() ?? [];

        var dungeons = source.Fights?
            .Where(f => f is not null)
            .Select(f => MapDungeon(f!))
            .ToList() ?? [];

        if (source.TargetFight is { } targetDungeons)
        {
            foreach (var target in targetDungeons)
            {
                if (target is null)
                    continue;

                var index = dungeons.FindIndex(d => d.Id == target.Id);
                if (index < 0)
                    continue;

                dungeons[index] = dungeons[index] with
                {
                    DungeonPulls = target.DungeonPulls?
                        .Where(p => p is not null)
                        .Select(p => MapDungeonPull(p!))
                        .ToList(),
                    EnemyNpcs = target.EnemyNPCs?
                        .Where(n => n?.Id is not null)
                        .Select(n => MapDungeonNpc(n!))
                        .ToList(),
                };
            }
        }

        var reportInfo = new ReportInfo(reportCode, source.Title, source.StartTime, source.EndTime, dungeons, masterActors);
        var reportMasterData = new ReportMasterData(abilities, masterActors);
        return new AnalysisPreload(reportInfo, reportMasterData);
    }

    public CharacterReports MapCharacterReports(IGetCharacterReports_CharacterData_Character source)
    {
        var reports = source.RecentReports?.Data?
            .Where(r => r is not null)
            .Select(r => new ReportSummary(
                r!.Code,
                r.Title,
                r.StartTime,
                r.EndTime,
                r.Fights?.Count(f => f is not null) ?? 0))
            .ToList() ?? [];

        return new CharacterReports(source.Name, reports);
    }
}
