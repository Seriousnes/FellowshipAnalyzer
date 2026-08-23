using FellowshipAnalyzer.Api.Core;
using FellowshipAnalyzer.Api.GraphQL;
using FellowshipAnalyzer.Core.Events;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Api.Core.Tests;

public class GraphQLMapperTests
{
    private sealed record StubAbility(double? GameID, string? Name, string? Icon, string? Type)
        : IGetReportMasterData_ReportData_Report_MasterData_Abilities;

    private sealed record StubReport(
        string Title,
        double StartTime,
        double EndTime,
        IReadOnlyList<IGetReportMasterData_ReportData_Report_Fights?>? Fights,
        IReadOnlyList<IGetReportMasterData_ReportData_Report_TargetFight?>? TargetFight,
        IGetReportMasterData_ReportData_Report_MasterData? MasterData)
        : IGetReportMasterData_ReportData_Report;

    private sealed record StubMasterData(
        IReadOnlyList<IGetReportMasterData_ReportData_Report_MasterData_Abilities?>? Abilities,
        IReadOnlyList<IGetReportMasterData_ReportData_Report_MasterData_Actors?>? Actors)
        : IGetReportMasterData_ReportData_Report_MasterData;

    private sealed record StubDungeon(
        int Id,
        string Name,
        int EncounterID,
        bool? Kill,
        double StartTime,
        double EndTime,
        int? Difficulty,
        IReadOnlyList<int?>? FriendlyPlayers,
        bool? InProgress,
        double? FightPercentage)
        : IGetReportMasterData_ReportData_Report_Fights;

    private sealed record StubTargetDungeon(
        int Id,
        IReadOnlyList<IGetReportMasterData_ReportData_Report_TargetFight_EnemyNPCs?>? EnemyNPCs,
        IReadOnlyList<IGetReportMasterData_ReportData_Report_TargetFight_DungeonPulls?>? DungeonPulls)
        : IGetReportMasterData_ReportData_Report_TargetFight;

    private sealed record StubPull(
        int Id,
        int EncounterID,
        bool? Kill,
        double StartTime,
        double EndTime,
        string Name,
        IReadOnlyList<IGetReportMasterData_ReportData_Report_TargetFight_DungeonPulls_EnemyNPCs?>? EnemyNPCs)
        : IGetReportMasterData_ReportData_Report_TargetFight_DungeonPulls;

    private static StubDungeon Dungeon(int id) =>
        new(id, $"Dungeon {id}", 5, true, 0, 1000, null, null, false, null);

    private static StubReport Report(
        IReadOnlyList<IGetReportMasterData_ReportData_Report_TargetFight?>? targetDungeons) =>
        new("Report", 0, 1000, [Dungeon(10), Dungeon(20), Dungeon(30)], targetDungeons, new StubMasterData([], []));

    [Fact]
    public void MapAbility_HasIdentityFromMasterData()
    {
        var mapped = new StubAbility(2190, "Attack", "icon.jpg", "1").MapAbility();

        mapped.FSLID.Value.ShouldBe(2190);
        mapped.Name.ShouldBe("Attack");
        mapped.Icon.ShouldBe("icon.jpg");
    }

    /// <summary>
    /// The report's <c>type</c> field does not encode a damage school: the same value covers physical
    /// and magic abilities alike. The mapper therefore ignores it whatever it holds, and the school
    /// comes from <c>data/spelldb.json</c> at compile time.
    /// </summary>
    [Theory]
    [InlineData("1")]
    [InlineData("1024")]
    [InlineData("not-a-number")]
    [InlineData("")]
    [InlineData(null)]
    public void MapAbility_IgnoresTheTypeField(string? type)
    {
        var mapped = new StubAbility(2190, "Attack", "icon.jpg", type).MapAbility();

        mapped.FSLID.Value.ShouldBe(2190);
        mapped.Name.ShouldBe("Attack");
    }

    /// <summary>
    /// Pulls are fetched for the requested dungeon alone, so every other dungeon in the report
    /// arrives without them while still appearing in <see cref="ReportInfo.Dungeons"/>.
    /// </summary>
    [Fact]
    public void MapAnalysisPreload_AttachesPullsToTheRequestedDungeonOnly()
    {
        var report = Report([new StubTargetDungeon(20, null, [new StubPull(1, 5, true, 100, 200, "First", null)])]);

        var preload = report.MapAnalysisPreload("code");

        preload.ReportInfo.Dungeons.Select(d => d.Id).ShouldBe([10, 20, 30]);
        preload.ReportInfo.Dungeons.Single(d => d.Id == 20).DungeonPulls!.Single().Name.ShouldBe("First");
        preload.ReportInfo.Dungeons.Where(d => d.Id != 20).ShouldAllBe(d => d.DungeonPulls == null);
    }

    [Fact]
    public void MapAnalysisPreload_KeepsEveryDungeonWhenNoneWasRequested()
    {
        var preload = Report(null).MapAnalysisPreload("code");

        preload.ReportInfo.Dungeons.Select(d => d.Id).ShouldBe([10, 20, 30]);
        preload.ReportInfo.Dungeons.ShouldAllBe(d => d.DungeonPulls == null);
    }
}
