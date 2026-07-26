using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Mara.Analysis;
using FellowshipAnalyzer.Heroes.Mara.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Mara.Spells;

namespace FellowshipAnalyzer.Heroes.Mara.Tests.Analysis;

public sealed class MaidenOfDeathWindowAnalyzerTests
{
    private const int PlayerId = 7;
    private const int FightEnd = 20000;

    [Fact]
    public void RechargeMs_ComesFromTheSpellRegistry()
    {
        MaidenOfDeathWindowAnalyzer.RechargeMs.ShouldBe(60_000);
    }

    [Fact]
    public async Task Analyze_MaidenBuff_OpensAndClosesOneWindow()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Cast(2000, Spells.QueenFang, comboPoints: 6, energy: 100),
            Buff<RemoveBuffEvent>(11000, Spells.MaidenOfDeathBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.OpenedAt.ShouldBe(1000);
        window.ClosedAt.ShouldBe(11000);
        window.DurationMs.ShouldBe(10000);
        window.HadMaidenOfDeath.ShouldBeTrue();
        window.HadMatriarchMacabre.ShouldBeFalse();
        window.Overlapped.ShouldBeFalse();
        window.Casts.ShouldHaveSingleItem().AbilityId.ShouldBe(Spells.QueenFang.Id);
        window.ScoredFinisherCasts.ShouldBe(1);
        window.FinisherComboPointsSpent.ShouldBe(6);
        analyzer.WindowCount.ShouldBe(1);
        analyzer.OverlappedWindows.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_BuffWithoutRemoval_CapsTheWindowAtPullEnd()
    {
        var events = new List<Event> { Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff) };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.OpenedAt.ShouldBe(1000);
        window.ClosedAt.ShouldBe(FightEnd);
        window.DurationMs.ShouldBe(FightEnd - 1000);
    }

    [Fact]
    public async Task Analyze_MatriarchDuringMaiden_ProducesOneOverlappedWindow()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Buff<ApplyBuffEvent>(4000, Spells.MatriarchMacabreSelfBuff),
            Buff<RemoveBuffEvent>(11000, Spells.MaidenOfDeathBuff),
            Buff<RemoveBuffEvent>(14000, Spells.MatriarchMacabreSelfBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.OpenedAt.ShouldBe(1000);
        window.ClosedAt.ShouldBe(14000);
        window.HadMaidenOfDeath.ShouldBeTrue();
        window.HadMatriarchMacabre.ShouldBeTrue();
        window.Overlapped.ShouldBeTrue();
        analyzer.OverlappedWindows.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_SeparatedBuffs_ProduceOneWindowEach()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Buff<RemoveBuffEvent>(5000, Spells.MaidenOfDeathBuff),
            Buff<ApplyBuffEvent>(8000, Spells.MatriarchMacabreSelfBuff),
            Buff<RemoveBuffEvent>(12000, Spells.MatriarchMacabreSelfBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.WindowCount.ShouldBe(2);
        analyzer.OverlappedWindows.ShouldBe(0);

        analyzer.Windows[0].HadMaidenOfDeath.ShouldBeTrue();
        analyzer.Windows[0].HadMatriarchMacabre.ShouldBeFalse();
        analyzer.Windows[0].ClosedAt.ShouldBe(5000);

        analyzer.Windows[1].HadMaidenOfDeath.ShouldBeFalse();
        analyzer.Windows[1].HadMatriarchMacabre.ShouldBeTrue();
        analyzer.Windows[1].OpenedAt.ShouldBe(8000);
    }

    [Fact]
    public async Task Analyze_Finishers_AreCountedOnlyInsideTheWindow()
    {
        var events = new List<Event>
        {
            Cast(500, Spells.QueenFang, comboPoints: 5, energy: 200),
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Cast(2000, Spells.QueenFang, comboPoints: 6, energy: 150),
            Cast(3000, Spells.Backstab, comboPoints: 2, energy: 120),
            Cast(4000, Spells.ArachnidAssault, comboPoints: 4, energy: 90),
            Buff<RemoveBuffEvent>(5000, Spells.MaidenOfDeathBuff),
            Cast(6000, Spells.QueenFang, comboPoints: 6, energy: 60),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Casts.Count.ShouldBe(3);
        window.ScoredFinisherCasts.ShouldBe(2);
        window.FinisherComboPointsSpent.ShouldBe(10);
        window.GeneratorCasts.ShouldBe(1);

        analyzer.ScoredFinishersInWindows.ShouldBe(2);
        analyzer.ComboPointsSpentInWindows.ShouldBe(10);
        analyzer.AverageFinishersPerWindow.ShouldBe(2d, 0.0001);
    }

    [Fact]
    public async Task Analyze_EnergyAtClose_IsTheLastSnapshotInsideTheWindow()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Cast(2000, Spells.QueenFang, comboPoints: 6, energy: 150),
            Cast(4000, Spells.ArachnidAssault, comboPoints: 4, energy: 90),
            Buff<RemoveBuffEvent>(5000, Spells.MaidenOfDeathBuff),
            Cast(6000, Spells.QueenFang, comboPoints: 6, energy: 60),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Windows.ShouldHaveSingleItem().EnergyAtClose.ShouldBe(90);
    }

    [Fact]
    public async Task Analyze_WindowWithoutResourceSnapshots_ReportsNoEnergyAtClose()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Buff<RemoveBuffEvent>(5000, Spells.MaidenOfDeathBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Casts.ShouldBeEmpty();
        window.EnergyAtClose.ShouldBeNull();
    }

    [Fact]
    public async Task Analyze_SpacedMaidenCasts_MeasureHeldTimeBeyondTheRecharge()
    {
        var events = new List<Event>
        {
            Cast(1000, Spells.MaidenOfDeath),
            Cast(65000, Spells.MaidenOfDeath),
            Cast(130000, Spells.MaidenOfDeath),
        };

        var analyzer = await AnalyzeAsync(events, BossFight(200000));

        analyzer.MaidenOfDeathCasts.ShouldBe(3);
        analyzer.MaidenOfDeathRecasts.Count.ShouldBe(2);

        analyzer.MaidenOfDeathRecasts[0].Timestamp.ShouldBe(65000);
        analyzer.MaidenOfDeathRecasts[0].GapMs.ShouldBe(64000);
        analyzer.MaidenOfDeathRecasts[0].HeldMs.ShouldBe(4000);

        analyzer.MaidenOfDeathRecasts[1].GapMs.ShouldBe(65000);
        analyzer.MaidenOfDeathRecasts[1].HeldMs.ShouldBe(5000);

        analyzer.TotalHeldMs.ShouldBe(9000);
        analyzer.AverageHeldMs.ShouldBe(4500d, 0.0001);
    }

    [Fact]
    public async Task Analyze_MaidenRecastOnRecharge_ReportsNoHeldTime()
    {
        var events = new List<Event>
        {
            Cast(1000, Spells.MaidenOfDeath),
            Cast(58000, Spells.MaidenOfDeath),
        };

        var analyzer = await AnalyzeAsync(events, BossFight(200000));

        analyzer.MaidenOfDeathRecasts.ShouldHaveSingleItem().HeldMs.ShouldBe(0);
        analyzer.TotalHeldMs.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ResetCasts_AreCountedInsideAndOutsideWindows()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Cast(2000, Spells.FinalStratagem),
            Buff<RemoveBuffEvent>(5000, Spells.MaidenOfDeathBuff),
            Cast(8000, Spells.MacabreStratagem),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.ResetCast.ShouldBeTrue();
        window.ResetCasts.ShouldBe(1);

        analyzer.ResetCasts.ShouldBe(2);
        analyzer.ResetCastsInWindows.ShouldBe(1);
        analyzer.WindowsWithReset.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_MatriarchMacabreCasts_AreCounted()
    {
        var events = new List<Event>
        {
            Cast(1000, Spells.MatriarchMacabre),
            Cast(9000, Spells.MatriarchMacabre),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.MatriarchMacabreCasts.ShouldBe(2);
        analyzer.MaidenOfDeathCasts.ShouldBe(0);
        analyzer.Windows.ShouldBeEmpty();
        analyzer.AverageFinishersPerWindow.ShouldBe(0d, 0.0001);
    }

    [Fact]
    public async Task Analyze_ExposesPerPullReadPaths()
    {
        var events = new List<Event> { Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff) };

        var parser = await AnalyzeParserAsync(events, BossFight());

        var entry = parser.MaidenOfDeathWindowAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;

        pull.MaidenOfDeathWindowAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).MaidenOfDeathWindowAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static TEvent Buff<TEvent>(int timestamp, Spell effect) where TEvent : BuffEvent, new() => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = effect.FSLID },
    };

    /// <summary>
    /// Raw Fellowship log resource values are scaled x100; the ResourceNormalizer divides by 100 during
    /// Analyze, so the fixture stores in-game intent (0-6 combo points, 0-200 Energy) x100. A cast with
    /// neither resource carries no snapshot at all, as an ability that spends nothing does.
    /// </summary>
    private static CastEvent Cast(int timestamp, Spell spell, int? comboPoints = null, int? energy = null)
    {
        var resources = new List<ClassResource>();
        if (energy is not null)
            resources.Add(new() { Type = ResourceTypes.Primary, Amount = energy.Value * 100, Max = 20000 });
        if (comboPoints is not null)
            resources.Add(new() { Type = ResourceTypes.Secondary, Amount = comboPoints.Value * 100, Max = 600 });

        return new CastEvent
        {
            Timestamp = timestamp,
            SourceId = PlayerId,
            Ability = new Ability { Id = spell.Id },
            SourceResources = resources.Count == 0 ? null : new ActorResources { Resources = resources },
        };
    }

    private static ReportFight BossFight(int endTime = FightEnd) =>
        new(0, "", 1, null, 0, endTime, null, null, null);

    private static async Task<MaidenOfDeathWindowAnalyzer> AnalyzeAsync(List<Event> events, ReportFight? fight = null)
    {
        var parser = await AnalyzeParserAsync(events, fight ?? BossFight());
        return parser.MaidenOfDeathWindowAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<MaraCombatLogParser> AnalyzeParserAsync(List<Event> events, ReportFight fight)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddMaraAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<MaraCombatLogParser>();
        await parser.Analyze(events, PlayerId, fight);
        return parser;
    }
}
