using FellowshipAnalyzer.Core.FellowshipLogs;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Common;

public class ReportInfoTests
{
    [Fact]
    public void FindNpcIconUrl_ReturnsIconUrlOfMatchingNpc()
    {
        var report = Report(
            Actor(1, "Grovetender", "NPC", "boss.jpg"),
            Actor(2, "Sapling", "NPC", "sapling.jpg"));

        report.FindNpcIconUrl("Grovetender")
            .ShouldBe("https://assets.rpglogs.com/img/fellowship/abilities/boss.jpg");
    }

    [Fact]
    public void FindNpcIconUrl_MatchesNameCaseInsensitively()
    {
        var report = Report(Actor(1, "Grovetender", "NPC", "boss.jpg"));

        report.FindNpcIconUrl("GROVETENDER")
            .ShouldBe("https://assets.rpglogs.com/img/fellowship/abilities/boss.jpg");
    }

    [Fact]
    public void FindNpcIconUrl_SkipsPlayersSharingTheName()
    {
        var report = Report(
            Actor(1, "Grovetender", "Player", "player.jpg"),
            Actor(2, "Grovetender", "NPC", "boss.jpg"));

        report.FindNpcIconUrl("Grovetender")
            .ShouldBe("https://assets.rpglogs.com/img/fellowship/abilities/boss.jpg");
    }

    [Fact]
    public void FindNpcIconUrl_SkipsMatchingNpcsWithoutAnIcon()
    {
        var report = Report(
            Actor(1, "Grovetender", "NPC", null),
            Actor(2, "Grovetender", "NPC", ""),
            Actor(3, "Grovetender", "NPC", "boss.jpg"));

        report.FindNpcIconUrl("Grovetender")
            .ShouldBe("https://assets.rpglogs.com/img/fellowship/abilities/boss.jpg");
    }

    [Fact]
    public void FindNpcIconUrl_ToleratesNpcsThatDifferOnlyByCase()
    {
        var report = Report(
            Actor(1, "grovetender", "NPC", "lower.jpg"),
            Actor(2, "Grovetender", "NPC", "upper.jpg"));

        report.FindNpcIconUrl("Grovetender")
            .ShouldBe("https://assets.rpglogs.com/img/fellowship/abilities/lower.jpg");
    }

    [Fact]
    public void FindNpcIconUrl_ToleratesUnnamedActors()
    {
        var report = Report(
            Actor(1, null, "NPC", "unnamed.jpg"),
            Actor(2, "Grovetender", "NPC", "boss.jpg"));

        report.FindNpcIconUrl("Grovetender")
            .ShouldBe("https://assets.rpglogs.com/img/fellowship/abilities/boss.jpg");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unknown")]
    public void FindNpcIconUrl_IsNull_WhenNoNpcMatches(string? name)
    {
        var report = Report(Actor(1, "Grovetender", "NPC", "boss.jpg"));

        report.FindNpcIconUrl(name).ShouldBeNull();
    }

    private static ReportInfo Report(params ReportActor[] actors) =>
        new(
            Code: "abc",
            Title: "Test",
            StartTime: 0,
            EndTime: null,
            Dungeons: [],
            Actors: [.. actors]);

    private static ReportActor Actor(int id, string? name, string type, string? icon) =>
        new(id, name!, type, SubType: null, Server: null, Icon: icon);
}
