using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.UI.Components;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Common;

[Collection(FellowshipAnalyzer.Core.Tests.UI.CodexCollection.Name)]
public class ReportDungeonTests
{
    public ReportDungeonTests() => Codex.Use(CodexOptions.Default);

    [Theory]
    [InlineData(31, "https://cdn.codex.fellowshipanalyzer.com/ui/Codex_Dungeon_SP_full.webp")]
    [InlineData(100_021, "https://cdn.codex.fellowshipanalyzer.com/ui/Codex_Dungeon_UM_full.webp")]
    [InlineData(100_007, "https://cdn.codex.fellowshipanalyzer.com/ui/Codex_CapDungeon_FK_full.webp")]
    public void DungeonIconUrl_ReducesZoneEncounterOffset(int encounterId, string expected)
    {
        Dungeon(encounterId).DungeonIconUrl.ShouldBe(expected);
    }

    [Fact]
    public void DungeonIconUrl_IsNull_WhenNoZoneEncounter()
    {
        Dungeon(0).DungeonIconUrl.ShouldBeNull();
    }

    [Fact]
    public void DungeonIconUrl_IsNull_WhenTheGameDataDrawsNoDungeon()
    {
        Dungeon(26).DungeonIconUrl.ShouldBeNull();
    }

    private static ReportDungeon Dungeon(int encounterId) =>
        new(
            Id: 1,
            Name: "Test",
            EncounterId: encounterId,
            Kill: true,
            StartTime: 0,
            EndTime: 0,
            Difficulty: null,
            FriendlyPlayers: null,
            CompletionPercentage: null);
}
