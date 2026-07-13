using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Elarion.Analysis;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using SpellAbility = FellowshipAnalyzer.Core.Events.Ability;

namespace FellowshipAnalyzer.Heroes.Elarion.Tests.Analysis;

public sealed class ElarionAnalysisEngineTests
{
    private const int PlayerId = 1;

    [Fact]
    public async Task Analyze_ShouldProvideGuideComponentType()
    {
        var (_, result) = await AnalyzeAsync([], new ReportFight(0, "", 0, null, 0, 0, null, null, null));

        result.GuideComponentType.ShouldNotBeNull();
    }

    [Fact]
    public async Task Analyze_SingleTargetPull_ExposesPerPullReadPaths()
    {
        var events = new List<Event>
        {
            Cast(Spells.LunarlightMark.Id, 1_000),
            Cast(Spells.HighwindArrow.Id, 2_000),
            Cast(Spells.HighwindArrow.Id, 3_000),
            Cast(Spells.StarfallVolley.Id, 13_000),
        };
        var fight = new ReportFight(0, "Boss", 31, true, 0, 20_000, null, null, null);

        var (parser, _) = await AnalyzeAsync(events, fight);

        var entry = parser.StarfallVolleyDesyncAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);
        pull.Targets.ShouldBe(PullKind.Single);
        entry.Analyzer.VolleyCount.ShouldBe(1);
        entry.Analyzer.CloseGapCount.ShouldBe(0);

        pull.StarfallVolleyDesyncAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).StarfallVolleyDesyncAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    [Fact]
    public async Task Analyze_SingleTargetPull_RunsSingleTargetLeafOnly()
    {
        var events = new List<Event>
        {
            Cast(Spells.HighwindArrow.Id, 2_000),
            Cast(Spells.HighwindArrow.Id, 3_000),
            Cast(Spells.Multishot.Id, 4_000),
        };
        var fight = new ReportFight(0, "Boss", 31, true, 0, 20_000, null, null, null);

        var (parser, _) = await AnalyzeAsync(events, fight);

        parser.HighwindArrowCapAnalyzers.ShouldHaveSingleItem().Analyzer.TotalCasts.ShouldBe(2);
        parser.EmpoweredMultishotWasteAnalyzers.ShouldBeEmpty();
    }

    [Fact]
    public async Task Analyze_MultiTargetPull_RunsAoeLeafOnly()
    {
        var events = new List<Event>
        {
            Cast(Spells.Multishot.Id, 1_000),
            Cast(Spells.Multishot.Id, 2_000),
            Cast(Spells.HighwindArrow.Id, 3_000),
        };
        var fight = new ReportFight(0, "Trash", 0, false, 0, 20_000, null, null, null);

        var (parser, _) = await AnalyzeAsync(events, fight);

        var entry = parser.EmpoweredMultishotWasteAnalyzers.ShouldHaveSingleItem();
        entry.Pull.Targets.ShouldBe(PullKind.Multi);
        entry.Analyzer.RegularCasts.ShouldBe(2);

        parser.HighwindArrowCapAnalyzers.ShouldBeEmpty();
        parser.StarfallVolleyDesyncAnalyzers.ShouldHaveSingleItem();
    }

    private static CastEvent Cast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new SpellAbility { Id = abilityId },
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
