using FellowshipAnalyzer.Api.GraphQL;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Riok.Mapperly.Abstractions;

namespace FellowshipAnalyzer.Api.Core;

[Mapper]
public sealed partial class GraphQLMapper
{
    // --- Ability ---

    public Ability MapAbility(IGetReportMasterData_ReportData_Report_MasterData_Abilities source)
    {
        if (!int.TryParse(source.Type, out var typeInt)) typeInt = 0;
        return new Ability
        {
            Guid = (int)(source.GameID ?? 0),
            Name = source.Name,
            Icon = source.Icon,
            Type = (MagicSchool)typeInt
        };
    }

    // --- ReportActor ---

    public ReportActor MapActor(IGetReportMasterData_ReportData_Report_MasterData_Actors source)
        => new(source.Id ?? 0, source.Name, source.Type, source.SubType, source.Server, source.Icon);

    // --- ReportFight ---

    public ReportFight MapFight(IGetReportMasterData_ReportData_Report_Fights source)
    {
        var fp = source.FriendlyPlayers?
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();
        return new ReportFight(
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

    // --- AnalysisPreload ---

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

        var fights = source.Fights?
            .Where(f => f is not null)
            .Select(f => MapFight(f!))
            .ToList() ?? [];

        var reportInfo = new ReportInfo(reportCode, source.Title, source.StartTime, source.EndTime, fights, masterActors);
        var reportMasterData = new ReportMasterData(abilities, masterActors);
        return new AnalysisPreload(reportInfo, reportMasterData);
    }

    // --- CharacterReports ---

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
