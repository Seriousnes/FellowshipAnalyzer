using FellowshipAnalyzer.Api.GraphQL;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Riok.Mapperly.Abstractions;

namespace FellowshipAnalyzer.Api.Core;

[Mapper(PropertyNameMappingStrategy = PropertyNameMappingStrategy.CaseInsensitive)]
public static partial class GraphQLMapper
{
    [MapperIgnoreSource(nameof(IGetReportMasterData_ReportData_Report_MasterData_Abilities.Type))]
    [MapperIgnoreTarget(nameof(Ability.Id))]
    [MapProperty(
        nameof(IGetReportMasterData_ReportData_Report_MasterData_Abilities.GameID),
        nameof(Ability.FSLID),
        Use = nameof(ToFSLID))]
    [MapProperty(
        nameof(IGetReportMasterData_ReportData_Report_MasterData_Abilities.Name),
        nameof(Ability.Name),
        Use = nameof(OrEmpty))]
    [MapProperty(
        nameof(IGetReportMasterData_ReportData_Report_MasterData_Abilities.Icon),
        nameof(Ability.Icon),
        Use = nameof(OrEmpty))]
    public static partial Ability MapAbility(this IGetReportMasterData_ReportData_Report_MasterData_Abilities source);

    [MapProperty(
        nameof(IGetReportMasterData_ReportData_Report_MasterData_Actors.Id),
        nameof(ReportActor.Id),
        Use = nameof(OrZero))]
    [MapProperty(
        nameof(IGetReportMasterData_ReportData_Report_MasterData_Actors.Name),
        nameof(ReportActor.Name),
        Use = nameof(OrEmpty))]
    [MapProperty(
        nameof(IGetReportMasterData_ReportData_Report_MasterData_Actors.Type),
        nameof(ReportActor.Type),
        Use = nameof(OrEmpty))]
    public static partial ReportActor MapActor(this IGetReportMasterData_ReportData_Report_MasterData_Actors source);

    [MapperIgnoreTarget(nameof(ReportDungeon.DungeonPulls))]
    [MapperIgnoreTarget(nameof(ReportDungeon.EnemyNpcs))]
    [MapProperty(
        nameof(IGetReportMasterData_ReportData_Report_Fights.FightPercentage),
        nameof(ReportDungeon.CompletionPercentage))]
    [MapProperty(
        nameof(IGetReportMasterData_ReportData_Report_Fights.FriendlyPlayers),
        nameof(ReportDungeon.FriendlyPlayers),
        Use = nameof(FriendlyPlayerIds))]
    [MapProperty(
        nameof(IGetReportMasterData_ReportData_Report_Fights.InProgress),
        nameof(ReportDungeon.InProgress),
        Use = nameof(OrFalse))]
    public static partial ReportDungeon MapDungeon(this IGetReportMasterData_ReportData_Report_Fights source);

    [MapProperty(
        nameof(IGetReportMasterData_ReportData_Report_TargetFight_EnemyNPCs.Id),
        nameof(DungeonNpc.Id),
        Use = nameof(OrZero))]
    [MapProperty(
        nameof(IGetReportMasterData_ReportData_Report_TargetFight_EnemyNPCs.GameID),
        nameof(DungeonNpc.GameId),
        Use = nameof(OrZero))]
    [MapProperty(
        nameof(IGetReportMasterData_ReportData_Report_TargetFight_EnemyNPCs.InstanceCount),
        nameof(DungeonNpc.InstanceCount),
        Use = nameof(OrOne))]
    [MapProperty(
        nameof(IGetReportMasterData_ReportData_Report_TargetFight_EnemyNPCs.GroupCount),
        nameof(DungeonNpc.GroupCount),
        Use = nameof(OrOne))]
    public static partial DungeonNpc MapDungeonNpc(this IGetReportMasterData_ReportData_Report_TargetFight_EnemyNPCs source);

    [MapProperty(
        nameof(IGetReportMasterData_ReportData_Report_TargetFight_DungeonPulls.EnemyNPCs),
        nameof(DungeonPull.EnemyNpcs),
        Use = nameof(MapDungeonPullNpcs))]
    public static partial DungeonPull MapDungeonPull(this IGetReportMasterData_ReportData_Report_TargetFight_DungeonPulls source);

    public static partial DungeonPullNpc MapDungeonPullNpc(this IGetReportMasterData_ReportData_Report_TargetFight_DungeonPulls_EnemyNPCs source);

    public static AnalysisPreload MapAnalysisPreload(this IGetReportMasterData_ReportData_Report source, string reportCode)
    {
        var masterData = source.MasterData
            ?? throw new InvalidOperationException("GraphQL response did not contain expected master data.");

        List<Ability> abilities = masterData.Abilities is { } sourceAbilities
            ? [.. sourceAbilities.Where(a => a is not null).Select(a => MapAbility(a!))]
            : [];

        List<ReportActor> masterActors = masterData.Actors is { } sourceActors
            ? [.. sourceActors.Where(a => a is not null).Select(a => MapActor(a!))]
            : [];

        List<ReportDungeon> dungeons = source.Fights is { } sourceDungeons
            ? [.. sourceDungeons.Where(f => f is not null).Select(f => MapDungeon(f!))]
            : [];

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
                    DungeonPulls = MapDungeonPulls(target.DungeonPulls),
                    EnemyNpcs = MapDungeonNpcs(target.EnemyNPCs),
                };
            }
        }

        var reportInfo = new ReportInfo(reportCode, source.Title, source.StartTime, source.EndTime, dungeons, masterActors);
        var reportMasterData = new ReportMasterData(abilities, masterActors);
        return new AnalysisPreload(reportInfo, reportMasterData);
    }

    public static CharacterReports MapCharacterReports(this IGetCharacterReports_CharacterData_Character source)
    {
        List<ReportSummary> reports = source.RecentReports?.Data is { } sourceReports
            ? [.. sourceReports.Where(r => r is not null).Select(r => MapReportSummary(r!))]
            : [];

        return new CharacterReports(source.Name, reports);
    }

    [MapProperty(
        nameof(IGetCharacterReports_CharacterData_Character_RecentReports_Data.Fights),
        nameof(ReportSummary.DungeonCount),
        Use = nameof(CountDungeons))]
    private static partial ReportSummary MapReportSummary(IGetCharacterReports_CharacterData_Character_RecentReports_Data source);

    [UserMapping(Default = false)]
    private static FSLID ToFSLID(double? gameId) => (int)(gameId ?? 0);

    [UserMapping(Default = false)]
    private static string OrEmpty(string? value) => value ?? string.Empty;

    [UserMapping(Default = false)]
    private static int OrOne(int? count) => count ?? 1;

    [UserMapping(Default = false)]
    private static int OrZero(int? value) => value ?? 0;

    [UserMapping(Default = false)]
    private static bool OrFalse(bool? value) => value ?? false;

    [UserMapping(Default = false)]
    private static List<int>? FriendlyPlayerIds(IReadOnlyList<int?>? source) =>
        source is null ? null : [.. source.Where(id => id.HasValue).Select(id => id!.Value)];

    [UserMapping(Default = false)]
    private static List<DungeonPullNpc>? MapDungeonPullNpcs(
        IReadOnlyList<IGetReportMasterData_ReportData_Report_TargetFight_DungeonPulls_EnemyNPCs?>? source) =>
        source is null ? null : [.. source.Where(n => n is not null).Select(n => MapDungeonPullNpc(n!))];

    [UserMapping(Default = false)]
    private static List<DungeonPull>? MapDungeonPulls(
        IReadOnlyList<IGetReportMasterData_ReportData_Report_TargetFight_DungeonPulls?>? source) =>
        source is null ? null : [.. source.Where(p => p is not null).Select(p => MapDungeonPull(p!))];

    [UserMapping(Default = false)]
    private static List<DungeonNpc>? MapDungeonNpcs(
        IReadOnlyList<IGetReportMasterData_ReportData_Report_TargetFight_EnemyNPCs?>? source) =>
        source is null ? null : [.. source.Where(n => n?.Id is not null).Select(n => MapDungeonNpc(n!))];

    [UserMapping(Default = false)]
    private static int CountDungeons(
        IReadOnlyList<IGetCharacterReports_CharacterData_Character_RecentReports_Data_Fights?>? source) =>
        source?.Count(f => f is not null) ?? 0;
}
