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

public sealed class MatriarchMacabreAnalyzerTests
{
    private const int PlayerId = 7;
    private const int DungeonEnd = 60000;

    [Fact]
    public async Task Analyze_MatriarchBuff_OpensAndClosesOneWindow()
    {
        var events = new List<Event>
        {
            Cast(1000, Spells.MatriarchMacabre),
            Buff<ApplyBuffEvent>(1000, Spells.MatriarchMacabreSelfBuff),
            Buff<RemoveBuffEvent>(21000, Spells.MatriarchMacabreSelfBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.OpenedAt.ShouldBe(1000);
        window.ClosedAt.ShouldBe(21000);
        window.DurationMs.ShouldBe(MatriarchMacabreAnalyzer.WindowMs);
        analyzer.MatriarchMacabreCasts.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_CountsOnlyTheTwoImitatedFinishers()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MatriarchMacabreSelfBuff),
            Cast(2000, Spells.QueenFang),
            Cast(4000, Spells.Backstab),
            Cast(6000, Spells.ArachnidAssault),
            Cast(8000, Spells.HemorrhagingStrike),
            Cast(10000, Spells.QueenFang),
            Buff<RemoveBuffEvent>(21000, Spells.MatriarchMacabreSelfBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.QueensFangCasts.ShouldBe(2);
        window.ArachnidAssaultCasts.ShouldBe(1);
        window.ImitatedCasts.ShouldBe(3);
        window.Converted.ShouldBeTrue();

        analyzer.ImitatedCasts.ShouldBe(3);
        analyzer.QueensFangCasts.ShouldBe(2);
        analyzer.ArachnidAssaultCasts.ShouldBe(1);
        analyzer.ConvertedWindows.ShouldBe(1);
        analyzer.AverageImitatedCasts.ShouldBe(3d, 0.0001);
    }

    [Fact]
    public async Task Analyze_FinishersOutsideTheWindow_AreNotCounted()
    {
        var events = new List<Event>
        {
            Cast(500, Spells.QueenFang),
            Buff<ApplyBuffEvent>(1000, Spells.MatriarchMacabreSelfBuff),
            Cast(2000, Spells.QueenFang),
            Buff<RemoveBuffEvent>(21000, Spells.MatriarchMacabreSelfBuff),
            Cast(25000, Spells.ArachnidAssault),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Windows.ShouldHaveSingleItem().ImitatedCasts.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_EmptyWindow_IsNotConverted()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MatriarchMacabreSelfBuff),
            Cast(2000, Spells.Backstab),
            Buff<RemoveBuffEvent>(21000, Spells.MatriarchMacabreSelfBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.ImitatedCasts.ShouldBe(0);
        window.Converted.ShouldBeFalse();
        analyzer.ConvertedWindows.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_MaidenOfDeath_TakesNoPartInTheMeasurement()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MatriarchMacabreSelfBuff),
            Buff<ApplyBuffEvent>(2000, Spells.MaidenOfDeathBuff),
            Cast(3000, Spells.QueenFang),
            Buff<RemoveBuffEvent>(12000, Spells.MaidenOfDeathBuff),
            Buff<RemoveBuffEvent>(21000, Spells.MatriarchMacabreSelfBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.OpenedAt.ShouldBe(1000);
        window.ClosedAt.ShouldBe(21000);
        window.ImitatedCasts.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_BuffWithoutRemoval_CapsTheWindowAtPullEnd()
    {
        var events = new List<Event> { Buff<ApplyBuffEvent>(1000, Spells.MatriarchMacabreSelfBuff) };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Windows.ShouldHaveSingleItem().ClosedAt.ShouldBe(DungeonEnd);
    }

    [Fact]
    public async Task Analyze_ExposesPerPullReadPaths()
    {
        var events = new List<Event> { Buff<ApplyBuffEvent>(1000, Spells.MatriarchMacabreSelfBuff) };

        var parser = await AnalyzeParserAsync(events, BossDungeon());

        var entry = parser.MatriarchMacabreAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;

        pull.MatriarchMacabreAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).MatriarchMacabreAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static TEvent Buff<TEvent>(int timestamp, Spell effect) where TEvent : BuffEvent, new() => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
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

    private static async Task<MatriarchMacabreAnalyzer> AnalyzeAsync(List<Event> events, ReportDungeon? dungeon = null)
    {
        var parser = await AnalyzeParserAsync(events, dungeon ?? BossDungeon());
        return parser.MatriarchMacabreAnalyzers.ShouldHaveSingleItem().Analyzer;
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
