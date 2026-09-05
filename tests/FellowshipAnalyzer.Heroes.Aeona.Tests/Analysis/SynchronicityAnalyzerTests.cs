using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

/// <summary>
/// Exercises Synchronicity's two halves over a whole report. Snapshot amounts are written at the raw
/// log scale because <c>ResourceNormalizer</c> divides them by 100 before dispatch, so a raw cap of
/// 10,000 is 100 Chrona and the talent's threshold sits at 50.
/// </summary>
public sealed class SynchronicityAnalyzerTests
{
    private const int PlayerId = 7;
    private const int TankId = 9;
    private const int EnemyId = 100;
    private const int RawChronaCap = 10_000;
    private const int DungeonEndMs = 120_000;

    [Fact]
    public async Task WithoutTheTalent_NoAnalyzerIsConstructedAndNoCardIsCollected()
    {
        var (parser, result) = await AnalyzeAsync(
            Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 9_000),
            Damage(2_000, Spells.TimeShardDamage.FSLID, amount: 1_150));

        parser.Synchronicity.ShouldBeNull();
        result.Statistics.ShouldNotContain(entry => entry.Module is SynchronicityAnalyzer);
    }

    [Fact]
    public async Task WithTheTalent_TheCardIsCollectedUnderTalents()
    {
        var (_, result) = await AnalyzeAsync(
            CombatantInfo(AeonaTalents.Synchronicity),
            Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 9_000),
            Damage(2_000, Spells.TimeShardDamage.FSLID, amount: 1_150));

        var entry = result.Statistics
            .Where(entry => entry.Module is SynchronicityAnalyzer)
            .ShouldHaveSingleItem();
        entry.Category.ShouldBe(StatisticCategory.Talents);
    }

    [Fact]
    public async Task WithTheTalentButNothingMeasured_NoCardIsCollected()
    {
        var (parser, result) = await AnalyzeAsync(
            CombatantInfo(AeonaTalents.Synchronicity),
            Damage(2_000, Spells.TimeShardDamage.FSLID, amount: 1_150));

        parser.Synchronicity.ShouldNotBeNull().DamageAboveThreshold.ShouldBe(0);
        result.Statistics.ShouldNotContain(entry => entry.Module is SynchronicityAnalyzer);
    }

    [Fact]
    public async Task ChronaArrivingBelowTheThreshold_IsCountedAcrossTheWholeReport()
    {
        var analyzer = await Analyze(
            CombatantInfo(AeonaTalents.Synchronicity),
            Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 3_000),
            Heal(2_000, Spells.EntropyClaim.FSLID, rawChrona: 3_600),
            Heal(3_000, Spells.EntropyClaim.FSLID, rawChrona: 5_500),
            Heal(4_000, Spells.EntropyClaim.FSLID, rawChrona: 6_000));

        analyzer.Threshold.ShouldBe(50);
        analyzer.ChronaGeneratedBelowThreshold.ShouldBe(25);
        analyzer.EstimatedChrona.ShouldBe(5d, 0.0001);
    }

    [Fact]
    public async Task DamageDealtAboveTheThreshold_IsCounted()
    {
        var analyzer = await Analyze(
            CombatantInfo(AeonaTalents.Synchronicity),
            Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 6_000),
            Damage(2_000, Spells.TimeShardDamage.FSLID, amount: 1_150));

        analyzer.DamageAboveThreshold.ShouldBe(1_150);
        analyzer.EstimatedDamage.ShouldBe(150d, 0.0001);
    }

    [Fact]
    public async Task DamageDealtBelowTheThreshold_IsNotCounted()
    {
        var analyzer = await Analyze(
            CombatantInfo(AeonaTalents.Synchronicity),
            Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 4_000),
            Damage(2_000, Spells.TimeShardDamage.FSLID, amount: 1_150));

        analyzer.DamageAboveThreshold.ShouldBe(0);
        analyzer.EstimatedDamage.ShouldBe(0d);
    }

    [Fact]
    public async Task DamageDealtAtExactlyTheThreshold_IsNotCounted()
    {
        var analyzer = await Analyze(
            CombatantInfo(AeonaTalents.Synchronicity),
            Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 5_000),
            Damage(2_000, Spells.TimeShardDamage.FSLID, amount: 1_150));

        analyzer.Threshold.ShouldBe(50);
        analyzer.DamageAboveThreshold.ShouldBe(0);
    }

    [Fact]
    public async Task OblivionDamage_IsExcludedBecauseOblivionSpendsChrona()
    {
        var analyzer = await Analyze(
            CombatantInfo(AeonaTalents.Synchronicity),
            Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 9_000),
            Damage(2_000, Spells.OblivionDamage.FSLID, amount: 5_000),
            Damage(3_000, Spells.TimeShardDamage.FSLID, amount: 1_150));

        Spells.Oblivion.Cost(ResourceTypes.Primary).ShouldBe(0);
        analyzer.DamageAboveThreshold.ShouldBe(1_150);
    }

    [Fact]
    public async Task DamageDealtBeforeAnyChronaSnapshot_IsNotCounted()
    {
        var analyzer = await Analyze(
            CombatantInfo(AeonaTalents.Synchronicity),
            Damage(1_000, Spells.TimeShardDamage.FSLID, amount: 5_000),
            Heal(2_000, Spells.EntropyClaim.FSLID, rawChrona: 9_000),
            Damage(3_000, Spells.TimeShardDamage.FSLID, amount: 1_150));

        analyzer.DamageAboveThreshold.ShouldBe(1_150);
    }

    [Fact]
    public async Task AHitWithItsOwnSnapshot_IsMeasuredAgainstTheChronaHeldBeforeIt()
    {
        var analyzer = await Analyze(
            CombatantInfo(AeonaTalents.Synchronicity),
            Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 4_000),
            Damage(2_000, Spells.EntropyClaimDot.FSLID, amount: 1_150, rawChrona: 9_000),
            Damage(3_000, Spells.TimeShardDamage.FSLID, amount: 2_300));

        analyzer.DamageAboveThreshold.ShouldBe(2_300);
    }

    [Fact]
    public async Task AReportWithNoChronaSnapshots_ReportsBothQuantitiesAsZero()
    {
        var analyzer = await Analyze(
            CombatantInfo(AeonaTalents.Synchronicity),
            Damage(1_000, Spells.TimeShardDamage.FSLID, amount: 5_000));

        analyzer.ChronaGeneratedBelowThreshold.ShouldBe(0);
        analyzer.DamageAboveThreshold.ShouldBe(0);
        analyzer.EstimatedChrona.ShouldBe(0d);
        analyzer.EstimatedDamage.ShouldBe(0d);
    }

    private static ActorResources? PlayerResources(int? rawChrona)
    {
        if (rawChrona is not { } chrona) return null;

        return new ActorResources
        {
            HitPoints = 20_000,
            MaxHitPoints = 30_000,
            Resources = [new ClassResource { Type = ResourceTypes.Primary, Amount = chrona, Max = RawChronaCap }],
        };
    }

    private static HealEvent Heal(int timestamp, int abilityId, int? rawChrona = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = TankId,
        Amount = 1_000,
        Ability = new Ability { FSLID = abilityId, Name = $"Spell {abilityId}" },
        SourceResources = PlayerResources(rawChrona),
    };

    private static DamageEvent Damage(int timestamp, int abilityId, long amount, int? rawChrona = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Amount = amount,
        Ability = new Ability { FSLID = abilityId, Name = $"Spell {abilityId}" },
        AbilityGameId = abilityId,
        SourceResources = PlayerResources(rawChrona),
    };

    private static CombatantInfoEvent CombatantInfo(params int[] talentIds) => new()
    {
        SourceId = PlayerId,
        Talents = [.. talentIds.Select(id => new TalentInfo { Id = id })],
    };

    private static ReportDungeon Dungeon() =>
        new(0, "Boss", 1, true, 0, DungeonEndMs, null, null, null);

    private static async Task<SynchronicityAnalyzer> Analyze(params Event[] events)
    {
        var (parser, _) = await AnalyzeAsync(events);
        return parser.Synchronicity.ShouldNotBeNull();
    }

    private static async Task<(AeonaCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(
        params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        var result = await parser.Analyze([.. events], PlayerId, Dungeon());
        return (parser, result);
    }
}
