using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.UI.Components;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Common;

[Xunit.Collection(FellowshipAnalyzer.Core.Tests.UI.CodexCollection.Name)]
public class ReportInfoTests
{
    private const string BossUrl = "https://cdn.codex.fellowshipanalyzer.com/ui/T_NPC_Boss_full.webp";

    public ReportInfoTests() => Codex.Use(CodexOptions.Default);

    [Fact]
    public void FindNpcIconUrl_ReturnsIconUrlOfMatchingNpc()
    {
        var report = Report(
            Actor(1, "Grovetender", "NPC", "custom-icon-T_NPC_Boss.jpg"),
            Actor(2, "Sapling", "NPC", "custom-icon-T_NPC_Sapling.jpg"));

        report.FindNpcIconUrl("Grovetender").ShouldBe(BossUrl);
    }

    [Fact]
    public void FindNpcIconUrl_MatchesNameCaseInsensitively()
    {
        var report = Report(Actor(1, "Grovetender", "NPC", "custom-icon-T_NPC_Boss.jpg"));

        report.FindNpcIconUrl("GROVETENDER").ShouldBe(BossUrl);
    }

    [Fact]
    public void FindNpcIconUrl_SkipsPlayersSharingTheName()
    {
        var report = Report(
            Actor(1, "Grovetender", "Player", "Aeona"),
            Actor(2, "Grovetender", "NPC", "custom-icon-T_NPC_Boss.jpg"));

        report.FindNpcIconUrl("Grovetender").ShouldBe(BossUrl);
    }

    [Fact]
    public void FindNpcIconUrl_SkipsMatchingNpcsWithoutAnIcon()
    {
        var report = Report(
            Actor(1, "Grovetender", "NPC", null),
            Actor(2, "Grovetender", "NPC", ""),
            Actor(3, "Grovetender", "NPC", "custom-icon-T_NPC_Boss.jpg"));

        report.FindNpcIconUrl("Grovetender").ShouldBe(BossUrl);
    }

    [Fact]
    public void FindNpcIconUrl_SkipsBlueprintsTheGameDataDrawsNoTextureFor()
    {
        var report = Report(
            Actor(1, "Grovetender", "NPC", "custom-icon-BP_NPC_UM_SlaveCage.jpg"),
            Actor(2, "Grovetender", "NPC", "custom-icon-T_NPC_Boss.jpg"));

        report.FindNpcIconUrl("Grovetender").ShouldBe(BossUrl);
    }

    [Fact]
    public void FindNpcIconUrl_ToleratesNpcsThatDifferOnlyByCase()
    {
        var report = Report(
            Actor(1, "grovetender", "NPC", "custom-icon-T_NPC_Lower.jpg"),
            Actor(2, "Grovetender", "NPC", "custom-icon-T_NPC_Upper.jpg"));

        report.FindNpcIconUrl("Grovetender")
            .ShouldBe("https://cdn.codex.fellowshipanalyzer.com/ui/T_NPC_Lower_full.webp");
    }

    [Fact]
    public void FindNpcIconUrl_ToleratesUnnamedActors()
    {
        var report = Report(
            Actor(1, null, "NPC", "custom-icon-T_NPC_Unnamed.jpg"),
            Actor(2, "Grovetender", "NPC", "custom-icon-T_NPC_Boss.jpg"));

        report.FindNpcIconUrl("Grovetender").ShouldBe(BossUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unknown")]
    public void FindNpcIconUrl_IsNull_WhenNoNpcMatches(string? name)
    {
        var report = Report(Actor(1, "Grovetender", "NPC", "custom-icon-T_NPC_Boss.jpg"));

        report.FindNpcIconUrl(name).ShouldBeNull();
    }

    [Theory]
    [InlineData("Aeona")]
    [InlineData("Unknown")]
    public void IconUrl_IsNull_ForAPlayersHeroName(string icon) =>
        Actor(1, "Player", "Player", icon).IconUrl.ShouldBeNull();

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
