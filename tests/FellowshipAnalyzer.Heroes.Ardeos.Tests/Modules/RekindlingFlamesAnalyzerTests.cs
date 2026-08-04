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

namespace FellowshipAnalyzer.Heroes.Ardeos.Tests.Modules;

public sealed class RekindlingFlamesAnalyzerTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 100;
    private const int EnemyInstance = 1;

    private static readonly ReportFight Fight =
        new(Id: 0, Name: "", EncounterId: 0, Kill: null,
            StartTime: 0, EndTime: 60_000, Difficulty: null,
            FriendlyPlayers: null, FightPercentage: null);

    [Fact]
    public async Task DeathWithoutEngulfingFlamesWindows_IsIgnored()
    {
        var analyzer = await Analyze(
            Combatant(),
            ApplyDebuff(1000, EnemyId, EnemyInstance, Spells.SearingBlazeDot.FSLID),
            Death(2000, EnemyId, EnemyInstance));

        analyzer.QualifyingDeaths.ShouldBe(0);
        analyzer.CooldownReduction.Total.ShouldBe(0);
        analyzer.CooldownReduction.Effective.ShouldBe(0);
        analyzer.CooldownReduction.Wasted.ShouldBe(0);
    }

    [Fact]
    public async Task SingleWindowDeath_ReducesRunningCooldownFully()
    {
        var analyzer = await Analyze(
            Combatant(),
            Cast(Spells.EngulfingFlames.FSLID, 1000),
            ApplyDebuff(1500, EnemyId, EnemyInstance, Spells.EngulfingFlamesDot.FSLID),
            Death(3000, EnemyId, EnemyInstance));

        analyzer.QualifyingDeaths.ShouldBe(1);
        analyzer.CooldownReduction.Total.ShouldBe(10_000);
        analyzer.CooldownReduction.Effective.ShouldBe(10_000);
        analyzer.CooldownReduction.Wasted.ShouldBe(0);
    }

    [Fact]
    public async Task ManyWindowsDeath_OverflowsChargeCap_IsWasted()
    {
        var analyzer = await Analyze(
            Combatant(),
            Cast(Spells.EngulfingFlames.FSLID, 1000),
            Cast(Spells.EngulfingFlames.FSLID, 1000),
            ApplyDebuff(2000, EnemyId, EnemyInstance, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(2000, EnemyId, EnemyInstance, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(2000, EnemyId, EnemyInstance, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(2000, EnemyId, EnemyInstance, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(2000, EnemyId, EnemyInstance, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(2000, EnemyId, EnemyInstance, Spells.EngulfingFlamesDot.FSLID),
            Death(11_000, EnemyId, EnemyInstance));

        analyzer.QualifyingDeaths.ShouldBe(1);
        analyzer.CooldownReduction.Total.ShouldBe(60_000);
        analyzer.CooldownReduction.Effective.ShouldBe(30_000);
        analyzer.CooldownReduction.Wasted.ShouldBe(30_000);
    }

    private static CombatantInfoEvent Combatant() => new() { SourceId = PlayerId };

    private static CastEvent Cast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = abilityId },
    };

    private static ApplyDebuffEvent ApplyDebuff(int timestamp, int targetId, int? targetInstance, int effectId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static DeathEvent Death(int timestamp, int targetId, int? targetInstance) => new()
    {
        Timestamp = timestamp,
        TargetId = targetId,
        TargetInstance = targetInstance,
    };

    private static async Task<RekindlingFlamesAnalyzer> Analyze(params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddArdeosAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ArdeosCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, Fight);
        return parser.GetModule<RekindlingFlamesAnalyzer>()!;
    }
}
