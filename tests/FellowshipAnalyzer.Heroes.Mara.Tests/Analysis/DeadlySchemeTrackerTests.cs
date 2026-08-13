using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Mara.Analysis;
using FellowshipAnalyzer.Heroes.Mara.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Mara.Spells;

namespace FellowshipAnalyzer.Heroes.Mara.Tests.Analysis;

public sealed class DeadlySchemeTrackerTests
{
    private const int PlayerId = 7;
    private const int DungeonEnd = 30000;

    [Fact]
    public async Task Analyze_StackStream_TracksThePeakAndClearsOnRemoval()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(1000, Spells.DeadlySchemeStacks),
            Stack<ApplyBuffStackEvent>(2000, Spells.DeadlySchemeStacks, 18),
            Buff<RemoveBuffEvent>(2100, Spells.DeadlySchemeStacks),
            Stack<ApplyBuffStackEvent>(3000, Spells.DeadlySchemeStacks, 0),
            Stack<ApplyBuffStackEvent>(4000, Spells.DeadlySchemeStacks, 0),
        };

        var tracker = await AnalyzeAsync(events);

        tracker.HighestStacks.ShouldBe(18);
        tracker.Activations.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_StackEventsWithoutACount_FallBackToAnIncrement()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(1000, Spells.DeadlySchemeStacks),
            Stack<ApplyBuffStackEvent>(2000, Spells.DeadlySchemeStacks, 0),
            Stack<ApplyBuffStackEvent>(3000, Spells.DeadlySchemeStacks, 0),
        };

        var tracker = await AnalyzeAsync(events);

        tracker.HighestStacks.ShouldBe(3);
    }

    [Fact]
    public async Task Analyze_ActivationWindows_AreCountedAndTimed()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(5000, Spells.DeadlySchemeActive),
            Buff<RemoveBuffEvent>(17000, Spells.DeadlySchemeActive),
            Buff<ApplyBuffEvent>(20000, Spells.DeadlySchemeActive),
            Buff<RemoveBuffEvent>(24000, Spells.DeadlySchemeActive),
        };

        var tracker = await AnalyzeAsync(events);

        tracker.Activations.ShouldBe(2);
        tracker.ActiveTimeMs.ShouldBe(16000);
    }

    [Fact]
    public async Task Analyze_ActivationWithoutRemoval_IsClosedAtDungeonEnd()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(25000, Spells.DeadlySchemeActive),
        };

        var tracker = await AnalyzeAsync(events);

        tracker.Activations.ShouldBe(1);
        tracker.ActiveTimeMs.ShouldBe(DungeonEnd - 25000);
        tracker.ActivationsUnspent.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_BenefitingCasts_MarkAnActivationSpent()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(5000, Spells.DeadlySchemeActive),
            Cast(5500, Spells.QueenFang),
            Cast(6500, Spells.ArachnidAssault),
            Cast(7500, Spells.HemorrhagingStrike),
            Buff<RemoveBuffEvent>(17000, Spells.DeadlySchemeActive),
            Cast(18000, Spells.QueenFang),
            Buff<ApplyBuffEvent>(20000, Spells.DeadlySchemeActive),
            Cast(21000, Spells.Backstab),
            Buff<RemoveBuffEvent>(24000, Spells.DeadlySchemeActive),
        };

        var tracker = await AnalyzeAsync(events);

        tracker.FinishersDuringActive.ShouldBe(2);
        tracker.HemorrhagingStrikesDuringActive.ShouldBe(1);
        tracker.ActivationsSpent.ShouldBe(1);
        tracker.ActivationsUnspent.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_HemorrhagingStrikeAlone_StillSpendsTheActivation()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(5000, Spells.DeadlySchemeActive),
            Cast(5500, Spells.HemorrhagingStrike),
            Buff<RemoveBuffEvent>(17000, Spells.DeadlySchemeActive),
        };

        var tracker = await AnalyzeAsync(events);

        tracker.FinishersDuringActive.ShouldBe(0);
        tracker.HemorrhagingStrikesDuringActive.ShouldBe(1);
        tracker.ActivationsSpent.ShouldBe(1);
        tracker.ActivationsUnspent.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_WithoutTheDeadlySchemeTalent_LeavesTheModuleUnbuilt()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(5000, Spells.DeadlySchemeActive),
            Cast(5500, Spells.QueenFang),
            Buff<RemoveBuffEvent>(17000, Spells.DeadlySchemeActive),
        };

        var (parser, result) = await AnalyzeParserAsync(events);

        parser.DeadlySchemeTracker.ShouldBeNull();
        result.Modules.OfType<DeadlySchemeTracker>().ShouldBeEmpty();
        result.Statistics.ShouldNotContain(statistic => statistic.Module is DeadlySchemeTracker);
    }

    [Fact]
    public async Task Analyze_WithActivations_SurfacesTheStatisticsCard()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(5000, Spells.DeadlySchemeActive),
            Buff<RemoveBuffEvent>(17000, Spells.DeadlySchemeActive),
        };

        var (_, result) = await AnalyzeParserAsync(events);

        var entry = result.Statistics.Single(statistic => statistic.Module is DeadlySchemeTracker);
        entry.Category.ShouldBe(StatisticCategory.Talents);
    }

    [Fact]
    public async Task Analyze_WithTheTalentButNoDeadlySchemeEvents_SurfacesNoCard()
    {
        var events = new List<Event>
        {
            Combatant(),
            Cast(5500, Spells.QueenFang),
        };

        var (parser, result) = await AnalyzeParserAsync(events);

        parser.DeadlySchemeTracker.ShouldNotBeNull();
        result.Statistics.ShouldNotContain(statistic => statistic.Module is DeadlySchemeTracker);
    }

    private static CombatantInfoEvent Combatant() => new()
    {
        SourceId = PlayerId,
        Talents = [new TalentInfo { Id = MaraTalents.DeadlyScheme }],
    };

    private static TEvent Buff<TEvent>(int timestamp, Spell effect) where TEvent : BuffEvent, new() => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = effect.FSLID },
    };

    private static TEvent Stack<TEvent>(int timestamp, Spell effect, int stack)
        where TEvent : BuffEvent, IBuffStackEvent, new()
    {
        var stackEvent = Buff<TEvent>(timestamp, effect);
        stackEvent.Stack = stack;
        return stackEvent;
    }

    private static CastEvent Cast(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = spell.Id },
    };

    private static async Task<DeadlySchemeTracker> AnalyzeAsync(List<Event> events)
    {
        var (parser, _) = await AnalyzeParserAsync(events);
        return parser.DeadlySchemeTracker.ShouldNotBeNull();
    }

    private static async Task<(MaraCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeParserAsync(
        List<Event> events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddMaraAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<MaraCombatLogParser>();
        var dungeon = new ReportDungeon(0, "", 1, null, 0, DungeonEnd, null, null, null);
        var result = await parser.Analyze(events, PlayerId, dungeon);
        return (parser, result);
    }
}
