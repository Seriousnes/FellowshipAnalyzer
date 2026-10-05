using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using ArdeosTalents = FellowshipAnalyzer.Core.Common.Spells.ArdeosTalents;

namespace FellowshipAnalyzer.Heroes.Ardeos.Tests.Modules;

public sealed class CrashAndBurnAnalyzerTests
{
    private const int PlayerId = 7;

    [Fact]
    public async Task CrashAndBurn_WithFireBallReady_WastesEveryTick()
    {
        var events = new List<Event>
        {
            CombatantWithCrashAndBurn(),
            SearingBlazeTick(1000),
            SearingBlazeTick(2000),
            SearingBlazeTick(3000),
        };

        var analyzer = await AnalyzeAndGetAnalyzer(events);

        analyzer.ShouldNotBeNull();
        analyzer.CooldownReduction.Total.ShouldBe(150);
        analyzer.CooldownReduction.Effective.ShouldBe(0);
        analyzer.CooldownReduction.Wasted.ShouldBe(150);
    }

    [Fact]
    public async Task CrashAndBurn_WithFireBallRecharging_ReducesFireBallCooldown()
    {
        var events = new List<Event>
        {
            CombatantWithCrashAndBurn(),
            Cast(Spells.FireBall.FSLID, 1000),
            SearingBlazeTick(2000),
            SearingBlazeTick(3000),
        };

        var analyzer = await AnalyzeAndGetAnalyzer(events);

        analyzer.ShouldNotBeNull();
        analyzer.CooldownReduction.Total.ShouldBe(100);
        analyzer.CooldownReduction.Effective.ShouldBe(100);
        analyzer.CooldownReduction.Wasted.ShouldBe(0);
    }

    [Fact]
    public async Task CrashAndBurn_WithoutTalent_IsInactive()
    {
        var events = new List<Event> { SearingBlazeTick(1000) };

        var analyzer = await AnalyzeAndGetAnalyzer(events);

        analyzer.ShouldBeNull();
    }

    private static CombatantInfoEvent CombatantWithCrashAndBurn() => new()
    {
        SourceId = PlayerId,
        Talents = [new TalentInfo { Id = ArdeosTalents.CrashAndBurn }],
    };

    private static DamageEvent SearingBlazeTick(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = Spells.SearingBlazeDot.FSLID },
    };

    private static CastEvent Cast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = abilityId },
    };

    private static async Task<CrashAndBurnAnalyzer?> AnalyzeAndGetAnalyzer(List<Event> events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddArdeosAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ArdeosCombatLogParser>();
        await parser.Analyze(events, PlayerId, new ReportDungeon(0, "", 0, null, 0, 10000, null, null, null));
        return parser.GetModule<CrashAndBurnAnalyzer>();
    }
}
