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

public sealed class StealthAnalyzerTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 42;
    private const int DungeonEnd = 20000;

    [Fact]
    public async Task Analyze_StealthWidowBite_ConvertsTheWindowAndLinksSeethingPoison()
    {
        var events = new List<Event>
        {
            Cast(900, Spells.BroodingShadows),
            Buff<ApplyBuffEvent>(1000, Spells.BroodingShadowsBuff),
            Cast(1500, Spells.WidowBite),
            Debuff<ApplyDebuffEvent>(1600, Spells.WidowBitePoison),
            Buff<RemoveBuffEvent>(3000, Spells.BroodingShadowsBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.OpenedAt.ShouldBe(1000);
        window.ClosedAt.ShouldBe(3000);
        window.DurationMs.ShouldBe(2000);
        window.Converted.ShouldBeTrue();
        window.ConvertingAbilityId.ShouldBe(Spells.WidowBite.Id);
        window.ConvertingCastAt.ShouldBe(1500);
        window.PoisonEffectId.ShouldBe(Spells.WidowBitePoison.FSLID);

        analyzer.WindowCount.ShouldBe(1);
        analyzer.ConvertedWindows.ShouldBe(1);
        analyzer.WindowsWithPoison.ShouldBe(1);
        analyzer.BroodingShadowsCasts.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_StealthSkitteringBlades_LinksVolatilePoison()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.BroodingShadowsBuff),
            Cast(1200, Spells.SkitteringBlades),
            Debuff<RefreshDebuffEvent>(1250, Spells.SkitteringBladesPoison),
            Buff<RemoveBuffEvent>(3000, Spells.BroodingShadowsBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.ConvertingAbilityId.ShouldBe(Spells.SkitteringBlades.Id);
        window.PoisonEffectId.ShouldBe(Spells.SkitteringBladesPoison.FSLID);
    }

    [Fact]
    public async Task Analyze_StealthWindowWithoutABuilder_IsUnconverted()
    {
        var events = new List<Event>
        {
            Cast(900, Spells.BroodingShadows),
            Buff<ApplyBuffEvent>(1000, Spells.BroodingShadowsBuff),
            Buff<RemoveBuffEvent>(3000, Spells.BroodingShadowsBuff),
            Cast(3500, Spells.WidowBite),
            Debuff<ApplyDebuffEvent>(3600, Spells.WidowBitePoison),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Converted.ShouldBeFalse();
        window.ConvertingAbilityId.ShouldBeNull();
        window.ConvertingCastAt.ShouldBeNull();
        window.PoisonEffectId.ShouldBeNull();

        analyzer.ConvertedWindows.ShouldBe(0);
        analyzer.BroodingShadowsCasts.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_StealthBuffWithoutRemoval_CapsTheWindowAtPullEnd()
    {
        var events = new List<Event> { Buff<ApplyBuffEvent>(1000, Spells.BroodingShadowsBuff) };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.OpenedAt.ShouldBe(1000);
        window.ClosedAt.ShouldBe(DungeonEnd);
        window.DurationMs.ShouldBe(DungeonEnd - 1000);
    }

    [Fact]
    public async Task Analyze_PoisonBeyondTheLinkHorizon_IsNotCreditedToTheWindow()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.BroodingShadowsBuff),
            Cast(1200, Spells.WidowBite),
            Buff<RemoveBuffEvent>(3000, Spells.BroodingShadowsBuff),
            Debuff<ApplyDebuffEvent>(1200 + StealthAnalyzer.PoisonLinkWindowMs + 1, Spells.WidowBitePoison),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Converted.ShouldBeTrue();
        window.PoisonEffectId.ShouldBeNull();
        analyzer.WindowsWithPoison.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_PoisonAfterTheStealthBuffFalls_StillLinksToItsCast()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.BroodingShadowsBuff),
            Cast(1200, Spells.WidowBite),
            Buff<RemoveBuffEvent>(1400, Spells.BroodingShadowsBuff),
            Debuff<ApplyDebuffEvent>(1900, Spells.WidowBitePoison),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Windows.ShouldHaveSingleItem().PoisonEffectId.ShouldBe(Spells.WidowBitePoison.FSLID);
    }

    [Fact]
    public async Task Analyze_StealthBackstab_NeverTakesCreditForAnotherBuildersPoison()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.BroodingShadowsBuff),
            Cast(1200, Spells.Backstab),
            Debuff<ApplyDebuffEvent>(1300, Spells.WidowBitePoison),
            Buff<RemoveBuffEvent>(3000, Spells.BroodingShadowsBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.ConvertingAbilityId.ShouldBe(Spells.Backstab.Id);
        window.PoisonEffectId.ShouldBeNull();
        analyzer.WindowsWithPoison.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_PoisonFromADifferentBuilder_IsNotCreditedToTheWindow()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.BroodingShadowsBuff),
            Cast(1200, Spells.WidowBite),
            Debuff<ApplyDebuffEvent>(1300, Spells.SkitteringBladesPoison),
            Buff<RemoveBuffEvent>(3000, Spells.BroodingShadowsBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Windows.ShouldHaveSingleItem().PoisonEffectId.ShouldBeNull();
    }

    [Fact]
    public async Task Analyze_OnlyTheFirstBuilderInAWindow_ConvertsIt()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.BroodingShadowsBuff),
            Cast(1100, Spells.Backstab),
            Cast(1300, Spells.WidowBite),
            Buff<RemoveBuffEvent>(3000, Spells.BroodingShadowsBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Windows.ShouldHaveSingleItem().ConvertingAbilityId.ShouldBe(Spells.Backstab.Id);
    }

    [Fact]
    public async Task Analyze_TwoStealthWindows_AreScoredIndependently()
    {
        var events = new List<Event>
        {
            Cast(900, Spells.BroodingShadows),
            Buff<ApplyBuffEvent>(1000, Spells.BroodingShadowsBuff),
            Cast(1200, Spells.WidowBite),
            Debuff<ApplyDebuffEvent>(1300, Spells.WidowBitePoison),
            Buff<RemoveBuffEvent>(3000, Spells.BroodingShadowsBuff),
            Cast(7900, Spells.BroodingShadows),
            Buff<ApplyBuffEvent>(8000, Spells.BroodingShadowsBuff),
            Buff<RemoveBuffEvent>(10000, Spells.BroodingShadowsBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.WindowCount.ShouldBe(2);
        analyzer.ConvertedWindows.ShouldBe(1);
        analyzer.BroodingShadowsCasts.ShouldBe(2);

        analyzer.Windows[0].Converted.ShouldBeTrue();
        analyzer.Windows[0].PoisonEffectId.ShouldBe(Spells.WidowBitePoison.FSLID);
        analyzer.Windows[1].Converted.ShouldBeFalse();
        analyzer.Windows[1].OpenedAt.ShouldBe(8000);
        analyzer.Windows[1].ClosedAt.ShouldBe(10000);
    }

    [Fact]
    public async Task Analyze_BroodingShadowsCastWithoutABuff_IsStillCounted()
    {
        var events = new List<Event> { Cast(900, Spells.BroodingShadows) };

        var analyzer = await AnalyzeAsync(events);

        analyzer.BroodingShadowsCasts.ShouldBe(1);
        analyzer.Windows.ShouldBeEmpty();
        analyzer.ConvertedWindows.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ExposesPerPullReadPaths()
    {
        var events = new List<Event> { Buff<ApplyBuffEvent>(1000, Spells.BroodingShadowsBuff) };

        var parser = await AnalyzeParserAsync(events, BossDungeon());

        var entry = parser.StealthAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;

        pull.StealthAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).StealthAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static TEvent Buff<TEvent>(int timestamp, Spell effect) where TEvent : BuffEvent, new() => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = effect.FSLID },
    };

    private static TEvent Debuff<TEvent>(int timestamp, Spell effect) where TEvent : BuffEvent, new() => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { Id = effect.FSLID },
    };

    private static CastEvent Cast(int timestamp, Spell spell) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = spell.Id },
    };

    private static ReportDungeon BossDungeon(int endTime = DungeonEnd) =>
        new(0, "", 1, null, 0, endTime, null, null, null);

    private static async Task<StealthAnalyzer> AnalyzeAsync(List<Event> events, ReportDungeon? dungeon = null)
    {
        var parser = await AnalyzeParserAsync(events, dungeon ?? BossDungeon());
        return parser.StealthAnalyzers.ShouldHaveSingleItem().Analyzer;
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
