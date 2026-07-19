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
        // The enemy carries a non-Engulfing-Flames debuff, so the effect-id filter must reject the death.
        var analyzer = await Analyze(
            Combatant(),
            ApplyDebuff(1000, EnemyId, EnemyInstance, Spells.SearingBlazeDot.FSLID),
            Death(2000, EnemyId, EnemyInstance));

        analyzer.QualifyingDeaths.ShouldBe(0);
        analyzer.TotalRequestedReductionMs.ShouldBe(0);
        analyzer.EffectiveReductionMs.ShouldBe(0);
        analyzer.WastedReductionMs.ShouldBe(0);
    }

    [Fact]
    public async Task SingleWindowDeath_ReducesRunningCooldownFully()
    {
        // Engulfing Flames on cooldown with one charge in flight; one open DoT window requests 10s and
        // the running cooldown has more than that remaining, so all of it lands.
        var analyzer = await Analyze(
            Combatant(),
            Cast(Spells.EngulfingFlames.FSLID, 1000),
            ApplyDebuff(1500, EnemyId, EnemyInstance, Spells.EngulfingFlamesDot.FSLID),
            Death(3000, EnemyId, EnemyInstance));

        analyzer.QualifyingDeaths.ShouldBe(1);
        analyzer.TotalRequestedReductionMs.ShouldBe(10_000);
        analyzer.EffectiveReductionMs.ShouldBe(10_000);
        analyzer.WastedReductionMs.ShouldBe(0);
    }

    [Fact]
    public async Task ManyWindowsDeath_OverflowsChargeCap_IsWasted()
    {
        // Both charges spent, so 40s of cooldown is reducible at most. Six open windows request 60s at a
        // death 10s after the casts (10s remains on the first charge, a full 20s on the second), so 30s
        // lands and 30s overflows.
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
        analyzer.TotalRequestedReductionMs.ShouldBe(60_000);
        analyzer.EffectiveReductionMs.ShouldBe(30_000);
        analyzer.WastedReductionMs.ShouldBe(30_000);
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
