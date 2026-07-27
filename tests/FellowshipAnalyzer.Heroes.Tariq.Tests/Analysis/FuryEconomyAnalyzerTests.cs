using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Tariq.Analysis;
using FellowshipAnalyzer.Heroes.Tariq.Modules;

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
    public async Task Analyze_FuryEconomy_ChargesOvercapWasteInFuryPoints()
    {
        var (parser, _) = await AnalyzeAsync(BuildScenario());

        var analyzer = parser.FuryEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.GeneratorCasts.ShouldBe(5);
        analyzer.OvercapCasts.ShouldBe(3);
        analyzer.WastedFury.ShouldBe(29);
        analyzer.PotentialGeneration.ShouldBe(42);
        analyzer.WasteRate.ShouldBe(29d / 42, tolerance: 0.001);
        analyzer.ActiveSpanMs.ShouldBe(800);
        analyzer.WastedFuryPerMinute.ShouldBe(2175, tolerance: 0.001);
    }

    [Fact]
    public async Task Analyze_FuryEconomy_CountsEverySpenderBySpell()
    {
        var (parser, _) = await AnalyzeAsync(BuildScenario());

        var analyzer = parser.FuryEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.SkullCrusherCasts.ShouldBe(2);
        analyzer.HammerStormCasts.ShouldBe(1);
        analyzer.CullingStrikeCasts.ShouldBe(1);
        analyzer.SpenderCasts.ShouldBe(4);
    }

    [Fact]
    public async Task Analyze_FuryEconomy_BreaksWasteDownByAbilityHeaviestFirst()
    {
        var (parser, _) = await AnalyzeAsync(BuildScenario());

        var analyzer = parser.FuryEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.WasteByAbility.ShouldBe(
        [
            new AbilityWaste(TariqSpells.LeapSmash.FSLID, 1, 20),
            new AbilityWaste(TariqSpells.HeavyStrike.FSLID, 1, 7),
            new AbilityWaste(TariqSpells.FaceBreaker.FSLID, 1, 2),
            new AbilityWaste(TariqSpells.WildSwing.FSLID, 1, 0),
            new AbilityWaste(TariqSpells.ChainLightning.FSLID, 1, 0),
        ]);
    }

    [Fact]
    public async Task Analyze_FuryEconomy_WastesNothingMidBar()
    {
        var (parser, _) = await AnalyzeAsync(
        [
            Cast(100, TariqSpells.HeavyStrike.FSLID, rawFury: 5_000, rawMaxFury: 10_000),
        ]);

        var analyzer = parser.FuryEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.GeneratorCasts.ShouldBe(1);
        analyzer.OvercapCasts.ShouldBe(0);
        analyzer.WastedFury.ShouldBe(0);
        analyzer.PotentialGeneration.ShouldBe(FuryEconomyAnalyzer.HeavyStrikeGain);
        analyzer.WasteRate.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_FuryEconomy_WastesTheWholeGainWhenLeapSmashLandsAtCap()
    {
        var (parser, _) = await AnalyzeAsync(
        [
            Cast(100, TariqSpells.LeapSmash.FSLID, rawFury: 10_000, rawMaxFury: 10_000),
        ]);

        var analyzer = parser.FuryEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.OvercapCasts.ShouldBe(1);
        analyzer.WastedFury.ShouldBe(FuryEconomyAnalyzer.LeapSmashGain);
        analyzer.WasteRate.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_FuryEconomy_ModelsChainLightningAsWastingNothing()
    {
        var (parser, _) = await AnalyzeAsync(
        [
            Cast(100, TariqSpells.ChainLightning.FSLID, rawFury: 10_000, rawMaxFury: 10_000),
        ]);

        var analyzer = parser.FuryEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.GeneratorCasts.ShouldBe(1);
        analyzer.OvercapCasts.ShouldBe(0);
        analyzer.WastedFury.ShouldBe(0);
        analyzer.PotentialGeneration.ShouldBe(0);
        analyzer.WasteByAbility.ShouldHaveSingleItem()
            .ShouldBe(new AbilityWaste(TariqSpells.ChainLightning.FSLID, 1, 0));
    }

    [Fact]
    public async Task Analyze_FuryEconomy_CountsAGeneratorWithNoSnapshotWithoutChargingWaste()
    {
        var (parser, _) = await AnalyzeAsync(
        [
            CastWithoutResources(100, TariqSpells.HeavyStrike.FSLID),
            Cast(200, TariqSpells.HeavyStrike.FSLID, rawFury: 10_000, rawMaxFury: 10_000),
        ]);

        var analyzer = parser.FuryEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.GeneratorCasts.ShouldBe(2);
        analyzer.OvercapCasts.ShouldBe(1);
        analyzer.WastedFury.ShouldBe(FuryEconomyAnalyzer.HeavyStrikeGain);
        analyzer.PotentialGeneration.ShouldBe(FuryEconomyAnalyzer.HeavyStrikeGain * 2);
        analyzer.WasteRate.ShouldBe(0.5, tolerance: 0.001);
    }

    [Fact]
    public async Task Analyze_FuryEconomy_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeAsync(BuildScenario());

        var entry = parser.FuryEconomyAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);

        pull.FuryEconomyAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).FuryEconomyAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    [Fact]
    public async Task Analyze_FuryEconomy_LeavesTheFuryTrackerSamplesTheGuideChartsPopulated()
    {
        var (parser, _) = await AnalyzeAsync(BuildScenario());

        var tracker = parser.FuryTracker.ShouldNotBeNull();

        tracker.Samples.Count.ShouldBe(9);
        tracker.Samples[0].ShouldBe(new FurySample(100, 50));
    }

    internal static async Task<(TariqCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(List<Event> events)
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

    /// <summary>
    /// Fury values are fabricated at the raw log scale (100x the in-game value), so
    /// <c>ResourceNormalizer</c> divides them down to the percentages the analyzer reads.
    /// </summary>
    private static List<Event> BuildScenario() =>
    [
        Cast(100, TariqSpells.WildSwing.FSLID, rawFury: 5_000, rawMaxFury: 10_000),
        Cast(200, TariqSpells.HeavyStrike.FSLID, rawFury: 9_500, rawMaxFury: 10_000),
        Cast(300, TariqSpells.SkullCrusher.FSLID, rawFury: 8_000, rawMaxFury: 10_000),
        Cast(400, TariqSpells.LeapSmash.FSLID, rawFury: 10_000, rawMaxFury: 10_000),
        Cast(500, TariqSpells.ChainLightning.FSLID, rawFury: 10_000, rawMaxFury: 10_000),
        Cast(600, TariqSpells.HammerStorm.FSLID, rawFury: 9_000, rawMaxFury: 10_000),
        Cast(700, TariqSpells.FaceBreaker.FSLID, rawFury: 9_500, rawMaxFury: 10_000),
        Cast(800, TariqSpells.CullingStrike.FSLID, rawFury: 3_000, rawMaxFury: 10_000),
        Cast(900, TariqSpells.SkullCrusher.FSLID, rawFury: 4_000, rawMaxFury: 10_000),
    ];

    internal static CastEvent Cast(int timestamp, int abilityId, int rawFury, int rawMaxFury)
    {
        var cast = CastWithoutResources(timestamp, abilityId);
        cast.SourceResources = new ActorResources
        {
            Resources = [new ClassResource { Type = ResourceTypes.Primary, Amount = rawFury, Max = rawMaxFury }],
        };
        return cast;
    }

    internal static CastEvent CastWithoutResources(int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { FSLID = abilityId, Name = $"Spell {abilityId}" },
        Target = new CastTarget(),
        Channel = new EndChannelEvent(),
    };

    internal static DamageEvent CullingStrikeHit(int timestamp, int targetHp, int targetMaxHp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { FSLID = TariqSpells.CullingStrike.FSLID, Name = "Culling Strike" },
        TargetResources = new ActorResources { HitPoints = targetHp, MaxHitPoints = targetMaxHp },
    };

    internal static TEvent Buff<TEvent>(int timestamp, int abilityId) where TEvent : BuffEvent, new() => new()
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
