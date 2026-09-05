using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Mara.Analysis;
using FellowshipAnalyzer.Heroes.Mara.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Mara.Spells;

namespace FellowshipAnalyzer.Heroes.Mara.Tests.Analysis;

public sealed class GuileAnalyzerTests
{
    private const int PlayerId = 7;
    private const int DungeonEnd = 20000;

    [Fact]
    public async Task Analyze_GuileWindowWithSpenders_IsConverted()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(1000, Spells.AssassinsGuileBuff),
            Cast(1200, Spells.QueenFang),
            Cast(2400, Spells.ArachnidAssault),
            Buff<RemoveBuffEvent>(6000, Spells.AssassinsGuileBuff),
            Cast(6500, Spells.QueenFang),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.OpenedAt.ShouldBe(1000);
        window.ClosedAt.ShouldBe(6000);
        window.DurationMs.ShouldBe(5000);
        window.SpenderCasts.ShouldBe(2);
        window.Converted.ShouldBeTrue();

        analyzer.WindowCount.ShouldBe(1);
        analyzer.ConvertedWindows.ShouldBe(1);
        analyzer.SpendersInWindows.ShouldBe(2);
    }

    [Fact]
    public async Task Analyze_GuileWindowWithoutASpender_IsNotConverted()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(1000, Spells.AssassinsGuileBuff),
            Cast(1200, Spells.WidowBite),
            Buff<RemoveBuffEvent>(6000, Spells.AssassinsGuileBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.SpenderCasts.ShouldBe(0);
        window.Converted.ShouldBeFalse();
        analyzer.ConvertedWindows.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_RefreshWhileTheBuffIsActive_ExtendsTheSameWindow()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(1000, Spells.AssassinsGuileBuff),
            Cast(1200, Spells.QueenFang),
            Buff<RefreshBuffEvent>(3000, Spells.AssassinsGuileBuff),
            Cast(5000, Spells.ArachnidAssault),
            Buff<RemoveBuffEvent>(8000, Spells.AssassinsGuileBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.OpenedAt.ShouldBe(1000);
        window.ClosedAt.ShouldBe(8000);
        window.SpenderCasts.ShouldBe(2);
    }

    [Fact]
    public async Task Analyze_GuileBuffWithoutRemoval_CapsTheWindowAtPullEnd()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(1000, Spells.AssassinsGuileBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.ClosedAt.ShouldBe(DungeonEnd);
        window.DurationMs.ShouldBe(DungeonEnd - 1000);
    }

    [Fact]
    public async Task Analyze_SeparatedGuileBuffs_ProduceOneWindowEach()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(1000, Spells.AssassinsGuileBuff),
            Cast(1200, Spells.QueenFang),
            Buff<RemoveBuffEvent>(6000, Spells.AssassinsGuileBuff),
            Buff<ApplyBuffEvent>(9000, Spells.AssassinsGuileBuff),
            Buff<RemoveBuffEvent>(14000, Spells.AssassinsGuileBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.WindowCount.ShouldBe(2);
        analyzer.ConvertedWindows.ShouldBe(1);
        analyzer.Windows[1].OpenedAt.ShouldBe(9000);
        analyzer.Windows[1].Converted.ShouldBeFalse();
    }

    [Fact]
    public async Task Analyze_MalevolenceStacksAtOpen_FollowTheBuffStream()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(500, Spells.MalevolenceQueensFang),
            Buff<ApplyBuffEvent>(600, Spells.MalevolenceArachnidAssault),
            Buff<ApplyBuffEvent>(1000, Spells.AssassinsGuileBuff),
            Cast(1200, Spells.QueenFang),
            Buff<RemoveBuffEvent>(6000, Spells.AssassinsGuileBuff),
            Stack<ApplyBuffStackEvent>(7000, Spells.MalevolenceQueensFang, 2),
            Buff<RemoveBuffEvent>(7100, Spells.MalevolenceArachnidAssault),
            Buff<ApplyBuffEvent>(8000, Spells.AssassinsGuileBuff),
            Cast(8200, Spells.ArachnidAssault),
            Buff<RemoveBuffEvent>(12000, Spells.AssassinsGuileBuff),
            Buff<RemoveBuffEvent>(13000, Spells.MalevolenceQueensFang),
            Stack<RemoveBuffStackEvent>(13100, Spells.MalevolenceArachnidAssault, 1),
            Buff<ApplyBuffEvent>(14000, Spells.AssassinsGuileBuff),
            Buff<RemoveBuffEvent>(18000, Spells.AssassinsGuileBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.HasMalevolence.ShouldBeTrue();
        analyzer.WindowCount.ShouldBe(3);

        analyzer.Windows[0].QueensFangStacksAtOpen.ShouldBe(1);
        analyzer.Windows[0].ArachnidAssaultStacksAtOpen.ShouldBe(1);

        analyzer.Windows[1].QueensFangStacksAtOpen.ShouldBe(2);
        analyzer.Windows[1].ArachnidAssaultStacksAtOpen.ShouldBe(0);

        analyzer.Windows[2].QueensFangStacksAtOpen.ShouldBe(0);
        analyzer.Windows[2].ArachnidAssaultStacksAtOpen.ShouldBe(1);

        analyzer.WindowsAtMaxStacks.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_OneFinisherAtMaxStacks_CountsTheWindowAtMaxStacks()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(500, Spells.MalevolenceQueensFang),
            Stack<ApplyBuffStackEvent>(600, Spells.MalevolenceQueensFang, 2),
            Buff<ApplyBuffEvent>(1000, Spells.AssassinsGuileBuff),
            Cast(1200, Spells.QueenFang),
            Buff<RemoveBuffEvent>(6000, Spells.AssassinsGuileBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.QueensFangStacksAtOpen.ShouldBe(2);
        window.ArachnidAssaultStacksAtOpen.ShouldBe(0);
        analyzer.WindowsAtMaxStacks.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_StackEventsWithoutACount_FallBackToIncrementAndDecrement()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(400, Spells.MalevolenceQueensFang),
            Stack<ApplyBuffStackEvent>(500, Spells.MalevolenceQueensFang, 0),
            Buff<ApplyBuffEvent>(600, Spells.MalevolenceArachnidAssault),
            Stack<ApplyBuffStackEvent>(700, Spells.MalevolenceArachnidAssault, 0),
            Stack<RemoveBuffStackEvent>(800, Spells.MalevolenceArachnidAssault, 0),
            Buff<ApplyBuffEvent>(1000, Spells.AssassinsGuileBuff),
            Buff<RemoveBuffEvent>(6000, Spells.AssassinsGuileBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.QueensFangStacksAtOpen.ShouldBe(2);
        window.ArachnidAssaultStacksAtOpen.ShouldBe(1);
        analyzer.HasMalevolence.ShouldBeTrue();
        analyzer.WindowsAtMaxStacks.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_WithoutMalevolence_ReportsNoStacks()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(1000, Spells.AssassinsGuileBuff),
            Cast(1200, Spells.QueenFang),
            Buff<RemoveBuffEvent>(6000, Spells.AssassinsGuileBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.HasMalevolence.ShouldBeFalse();
        analyzer.WindowsAtMaxStacks.ShouldBe(0);
        analyzer.Windows.ShouldHaveSingleItem().QueensFangStacksAtOpen.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_WithoutTheAssassinsGuileTalent_LeavesTheSurfaceEmpty()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.AssassinsGuileBuff),
            Cast(1200, Spells.QueenFang),
            Buff<RemoveBuffEvent>(6000, Spells.AssassinsGuileBuff),
        };

        var parser = await AnalyzeParserAsync(events, BossDungeon());

        parser.GuileAnalyzers.ShouldBeEmpty();
        parser.StealthAnalyzers.ShouldNotBeEmpty();
        parser.Pulls.ShouldHaveSingleItem().GuileAnalyzer.ShouldBeNull();
    }

    [Fact]
    public async Task Analyze_ExposesPerPullReadPaths()
    {
        var events = new List<Event>
        {
            Combatant(),
            Buff<ApplyBuffEvent>(1000, Spells.AssassinsGuileBuff),
        };

        var parser = await AnalyzeParserAsync(events, BossDungeon());

        var entry = parser.GuileAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;

        pull.GuileAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).GuileAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static CombatantInfoEvent Combatant() => new()
    {
        SourceId = PlayerId,
        Talents = [new TalentInfo { Id = MaraTalents.AssassinsGuile }],
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

    private static ReportDungeon BossDungeon(int endTime = DungeonEnd) =>
        new(0, "", 1, null, 0, endTime, null, null, null);

    private static async Task<GuileAnalyzer> AnalyzeAsync(List<Event> events, ReportDungeon? dungeon = null)
    {
        var parser = await AnalyzeParserAsync(events, dungeon ?? BossDungeon());
        return parser.GuileAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<MaraCombatLogParser> AnalyzeParserAsync(List<Event> events, ReportDungeon dungeon)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddMaraAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<MaraCombatLogParser>();
        await parser.Analyze(events, PlayerId, dungeon);
        return parser;
    }
}
