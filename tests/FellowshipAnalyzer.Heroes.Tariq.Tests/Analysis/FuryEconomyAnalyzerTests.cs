using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Tariq.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using TariqSpells = FellowshipAnalyzer.Core.Common.Spells.Tariq.Spells;

namespace FellowshipAnalyzer.Heroes.Tariq.Tests.Analysis;

public sealed class FuryEconomyAnalyzerTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 11;

    [Fact]
    public async Task Analyze_FuryEconomy_MeasuresOvercapSpendersAndExecute()
    {
        var (parser, _) = await AnalyzeAsync(BuildScenario());

        var entry = parser.FuryEconomyReports.ShouldHaveSingleItem();
        var report = entry.Result;

        report.GeneratorCasts.ShouldBe(3);
        report.OvercapCasts.ShouldBe(1);
        report.SpenderCasts.ShouldBe(4);
        report.SkullCrusherCasts.ShouldBe(2);
        report.HammerStormCasts.ShouldBe(1);
        report.CullingStrikeCasts.ShouldBe(1);
        report.EmpoweredSpenderCasts.ShouldBe(3);
        report.ThunderCallWindows.ShouldBe(1);
        report.ExecuteCullingStrikes.ShouldBe(1);
        report.ScoreCard.Score.ShouldBe(67);
        report.Findings.ShouldContain(finding => finding.Severity == "warning");
    }

    [Fact]
    public async Task Analyze_FuryEconomy_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeAsync(BuildScenario());

        var entry = parser.FuryEconomyReports.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);

        pull.FuryEconomyReport.ShouldBe(entry.Result);
        parser.For(pull).FuryEconomyReport.ShouldBe(entry.Result);
    }

    private static async Task<(TariqCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(List<Event> events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddTariqAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<TariqCombatLogParser>();
        var fight = new ReportFight(0, "Boss", 1, true, 0, 2_000, null, null, null);
        var result = await parser.Analyze(events, playerId: PlayerId, fight: fight);
        return (parser, result);
    }

    private static List<Event> BuildScenario() =>
    [
        Buff<ApplyBuffEvent>(100, TariqSpells.ThunderCallBuff.FSLID),
        Cast(200, TariqSpells.WildSwing.FSLID, fury: 50, maxFury: 100),
        Cast(300, TariqSpells.HeavyStrike.FSLID, fury: 100, maxFury: 100),
        Cast(400, TariqSpells.SkullCrusher.FSLID, fury: 60, maxFury: 100),
        Cast(500, TariqSpells.HammerStorm.FSLID, fury: 40, maxFury: 100),
        Cast(600, TariqSpells.CullingStrike.FSLID, fury: 30, maxFury: 100),
        CullingStrikeHit(650, targetHp: 10, targetMaxHp: 100),
        Buff<RemoveBuffEvent>(800, TariqSpells.ThunderCallBuff.FSLID),
        Cast(900, TariqSpells.SkullCrusher.FSLID, fury: 55, maxFury: 100),
        Cast(1000, TariqSpells.ChainLightning.FSLID, fury: 30, maxFury: 100),
    ];

    private static CastEvent Cast(int timestamp, int abilityId, int fury, int maxFury) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { FSLID = abilityId, Name = $"Spell {abilityId}" },
        Target = new CastTarget(),
        Channel = new EndChannelEvent(),
        SourceResources = new ActorResources
        {
            Resources = [new ClassResource { Type = ResourceTypes.Primary, Amount = fury, Max = maxFury }],
        },
    };

    private static DamageEvent CullingStrikeHit(int timestamp, int targetHp, int targetMaxHp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { FSLID = TariqSpells.CullingStrike.FSLID, Name = "Culling Strike" },
        TargetResources = new ActorResources { HitPoints = targetHp, MaxHitPoints = targetMaxHp },
    };

    private static TEvent Buff<TEvent>(int timestamp, int abilityId) where TEvent : BuffEvent, new() => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = abilityId, Name = $"Buff {abilityId}" },
    };

    private sealed class CastTarget : ICastTarget
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public int Guid { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}
