using FellowshipAnalyzer.Core.FellowshipLogs;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Common;

public class ReportFightTests
{
    [Theory]
    [InlineData(31, "https://assets.rpglogs.com/img/fellowship/bosses/31-icon.jpg")]
    [InlineData(100_021, "https://assets.rpglogs.com/img/fellowship/bosses/21-icon.jpg")]
    [InlineData(100_007, "https://assets.rpglogs.com/img/fellowship/bosses/7-icon.jpg")]
    public void DungeonIconUrl_ReducesZoneEncounterOffset(int encounterId, string expected)
    {
        Fight(encounterId).DungeonIconUrl.ShouldBe(expected);
    }

    [Fact]
    public void DungeonIconUrl_IsNull_WhenNoZoneEncounter()
    {
        Fight(0).DungeonIconUrl.ShouldBeNull();
    }

    private static ReportFight Fight(int encounterId) =>
        new(
            Id: 1,
            Name: "Test",
            EncounterId: encounterId,
            Kill: true,
            StartTime: 0,
            EndTime: 0,
            Difficulty: null,
            FriendlyPlayers: null,
            FightPercentage: null);
}
