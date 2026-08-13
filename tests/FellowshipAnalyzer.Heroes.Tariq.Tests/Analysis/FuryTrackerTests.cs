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

public sealed class FuryTrackerTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 11;

    [Fact]
    public async Task Analyze_FuryTracker_NormalizesToPercentUnderAGrowingRawMax()
    {
        var (parser, _) = await AnalyzeAsync(
        [
            Cast(100, TariqSpells.WildSwing.FSLID, rawFury: 30_000, rawMaxFury: 60_000),
            Cast(200, TariqSpells.HeavyStrike.FSLID, rawFury: 45_000, rawMaxFury: 90_000),
        ]);

        var tracker = parser.FuryTracker.ShouldNotBeNull();

        tracker.Samples.Select(sample => sample.Fury).ShouldBe([50, 50]);
        tracker.Current.ShouldBe(50);
    }

    [Fact]
    public async Task Analyze_FuryTracker_HoldsTheLatestReadingAsCurrent()
    {
        var (parser, _) = await AnalyzeAsync(BuildScenario());

        var tracker = parser.FuryTracker.ShouldNotBeNull();

        tracker.Current.ShouldBe(10);
    }

    [Fact]
    public async Task Analyze_FuryTracker_RecordsEveryObservationIncludingTheSeed()
    {
        var (parser, _) = await AnalyzeAsync(BuildScenario());

        var tracker = parser.FuryTracker.ShouldNotBeNull();

        tracker.Samples.Count.ShouldBe(5);
        tracker.Samples.ShouldBe(
        [
            new FurySample(100, 50),
            new FurySample(200, 80),
            new FurySample(300, 20),
            new FurySample(400, 70),
            new FurySample(500, 10),
        ]);
    }

    [Fact]
    public async Task Analyze_FuryTracker_IgnoresCastsWithoutAUsableFurySnapshot()
    {
        var (parser, _) = await AnalyzeAsync(
        [
            CastWithoutResources(100, TariqSpells.WildSwing.FSLID),
            Cast(200, TariqSpells.HeavyStrike.FSLID, rawFury: 30_000, rawMaxFury: 0),
            Cast(300, TariqSpells.ChainLightning.FSLID, rawFury: 20_000, rawMaxFury: 50_000),
        ]);

        var tracker = parser.FuryTracker.ShouldNotBeNull();

        tracker.Samples.ShouldBe([new FurySample(300, 40)]);
        tracker.Current.ShouldBe(40);
    }

    [Fact]
    public async Task Analyze_FuryTracker_ResolvesFromTheParserAndLabelsThePrimaryResource()
    {
        var (parser, result) = await AnalyzeAsync(BuildScenario());

        var tracker = parser.FuryTracker.ShouldNotBeNull();

        result.Modules.OfType<FuryTracker>().ShouldHaveSingleItem().ShouldBeSameAs(tracker);
        tracker.MaxFury.ShouldBe(100);
        tracker.GetDisplayName(ResourceTypes.Primary).ShouldBe("Fury");
    }

    private static List<Event> BuildScenario() =>
    [
        Cast(100, TariqSpells.WildSwing.FSLID, rawFury: 30_000, rawMaxFury: 60_000),
        Cast(200, TariqSpells.HeavyStrike.FSLID, rawFury: 48_000, rawMaxFury: 60_000),
        Cast(300, TariqSpells.SkullCrusher.FSLID, rawFury: 18_000, rawMaxFury: 90_000),
        Cast(400, TariqSpells.ChainLightning.FSLID, rawFury: 63_000, rawMaxFury: 90_000),
        Cast(500, TariqSpells.HammerStorm.FSLID, rawFury: 12_000, rawMaxFury: 120_000),
    ];

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
        var dungeon = new ReportDungeon(0, "Boss", 1, true, 0, 2_000, null, null, null);
        var result = await parser.Analyze(events, playerId: PlayerId, dungeon: dungeon);
        return (parser, result);
    }

    private static CastEvent Cast(int timestamp, int abilityId, int rawFury, int rawMaxFury)
    {
        var cast = CastWithoutResources(timestamp, abilityId);
        cast.SourceResources = new ActorResources
        {
            Resources = [new ClassResource { Type = ResourceTypes.Primary, Amount = rawFury, Max = rawMaxFury }],
        };
        return cast;
    }

    private static CastEvent CastWithoutResources(int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { FSLID = abilityId, Name = $"Spell {abilityId}" },
        Target = new CastTarget(),
        Channel = new EndChannelEvent(),
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
