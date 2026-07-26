using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Elarion.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using ElarionTalents = FellowshipAnalyzer.Core.Common.Spells.ElarionTalents;
using SpellAbility = FellowshipAnalyzer.Core.Events.Ability;

namespace FellowshipAnalyzer.Heroes.Elarion.Tests.Analysis;

public sealed class ElarionAnalysisEngineTests
{
    private const int PlayerId = 1;

    private const int LogResourceScale = 100;

    [Fact]
    public async Task Analyze_ShouldProvideGuideComponentType()
    {
        var (_, result) = await AnalyzeAsync([], new ReportFight(0, "", 0, null, 0, 0, null, null, null));

        result.GuideComponentType.ShouldNotBeNull();
    }

    [Fact]
    public void GeneratedTalentConstants_CoverTheSeason3Roster()
    {
        ElarionTalents.DeadlyFocus.ShouldBe(679);
        ElarionTalents.StrikersAim.ShouldBe(680);
        ElarionTalents.RisingMoon.ShouldBe(681);
        ElarionTalents.SwiftReload.ShouldBe(682);
    }

    [Fact]
    public void Registry_CarriesSeason3AbilityFacts()
    {
        Spells.CelestialVeil.FSLID.Value.ShouldBe(1302);
        Spells.GrapplingArrow.Charges.ShouldBe(2);
        Spells.HighwindArrow.FocusCost.ShouldBe(30);
        Spells.EventHorizon.SpiritCost.ShouldBe(100);
    }

    [Fact]
    public async Task FocusTracker_SpendsTheRegistryFocusCost()
    {
        var events = new List<Event>
        {
            Cast(Spells.HighwindArrow.Id, 1_000, focus: 100),
        };

        var (parser, _) = await AnalyzeAsync(events, new ReportFight(0, "Boss", 31, true, 0, 20_000, null, null, null));

        var tracker = parser.FocusTracker.ShouldNotBeNull();
        tracker.GetSpent(ResourceTypes.Primary).ShouldBe(30);
        tracker.GetCurrent(ResourceTypes.Primary).ShouldBe(70);
    }

    private static CastEvent Cast(int abilityId, int timestamp, int? focus = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new SpellAbility { Id = abilityId },
        SourceResources = focus is null
            ? null
            : new ActorResources
            {
                Resources =
                [
                    new ClassResource
                    {
                        Type = ResourceTypes.Primary,
                        Amount = focus.Value * LogResourceScale,
                        Max = 100 * LogResourceScale,
                    },
                ],
            },
    };

    private static async Task<(ElarionCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(
        IReadOnlyList<Event> events, ReportFight fight)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddElarionAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ElarionCombatLogParser>();
        var result = await parser.Analyze(events, PlayerId, fight);
        return (parser, result);
    }
}
