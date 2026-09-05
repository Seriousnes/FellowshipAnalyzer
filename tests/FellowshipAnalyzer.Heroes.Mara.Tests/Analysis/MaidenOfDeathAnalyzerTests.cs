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

public sealed class MaidenOfDeathAnalyzerTests
{
    private const int PlayerId = 7;
    private const int DungeonEnd = 20000;

    [Fact]
    public void RechargeMs_ComesFromTheSpellRegistry()
    {
        MaidenOfDeathAnalyzer.RechargeMs.ShouldBe(60_000);
    }

    [Fact]
    public async Task Analyze_MaidenBuff_OpensAndClosesOneWindow()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Cast(1000, Spells.QueenFang, comboPoints: 6, energy: 100),
            Buff<RemoveBuffEvent>(2500, Spells.MaidenOfDeathBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.OpenedAt.ShouldBe(1000);
        window.ClosedAt.ShouldBe(2500);
        window.DurationMs.ShouldBe(1500);
        window.Casts.ShouldHaveSingleItem().AbilityId.ShouldBe(Spells.QueenFang.Id);
        window.SpenderCasts.ShouldBe(1);
        window.ComboPointsSpent.ShouldBe(6);
        analyzer.WindowCount.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_MatriarchMacabre_TakesNoPartInTheWindow()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Buff<ApplyBuffEvent>(1200, Spells.MatriarchMacabreSelfBuff),
            Cast(1000, Spells.Backstab, comboPoints: 0),
            Cast(2500, Spells.QueenFang, comboPoints: 6),
            Buff<RemoveBuffEvent>(4000, Spells.MaidenOfDeathBuff),
            Buff<RemoveBuffEvent>(9000, Spells.MatriarchMacabreSelfBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.OpenedAt.ShouldBe(1000);
        window.ClosedAt.ShouldBe(4000);
        window.GeneratorCasts.ShouldBe(1);
        window.SpenderCasts.ShouldBe(1);
        window.CleanCycle.ShouldBeTrue();
    }

    [Fact]
    public async Task Analyze_BuffWithoutRemoval_CapsTheWindowAtPullEnd()
    {
        var events = new List<Event> { Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff) };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.OpenedAt.ShouldBe(1000);
        window.ClosedAt.ShouldBe(DungeonEnd);
    }

    [Fact]
    public async Task Analyze_AlternatingCycle_WastesNothing()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Cast(1000, Spells.Backstab, comboPoints: 0),
            Cast(2500, Spells.QueenFang, comboPoints: 6),
            Cast(4000, Spells.WidowBite, comboPoints: 0),
            Cast(5500, Spells.ArachnidAssault, comboPoints: 6),
            Buff<RemoveBuffEvent>(7000, Spells.MaidenOfDeathBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.GeneratorCasts.ShouldBe(2);
        window.SpenderCasts.ShouldBe(2);
        window.WastedGeneratorCasts.ShouldBe(0);
        window.ComboPointsWasted.ShouldBe(0);
        window.ComboPointsSpent.ShouldBe(12);
        window.IdleMs.ShouldBe(0);
        window.CleanCycle.ShouldBeTrue();

        analyzer.CleanWindows.ShouldBe(1);
        analyzer.WastedGeneratorCasts.ShouldBe(0);
        analyzer.AverageSpendersPerWindow.ShouldBe(2d, 0.0001);
    }

    [Fact]
    public async Task Analyze_TwoGeneratorsInARow_WastesAFullPool()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Cast(1000, Spells.Backstab, comboPoints: 0),
            Cast(2500, Spells.WidowBite, comboPoints: 6),
            Cast(4000, Spells.QueenFang, comboPoints: 6),
            Buff<RemoveBuffEvent>(5500, Spells.MaidenOfDeathBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.GeneratorCasts.ShouldBe(2);
        window.WastedGeneratorCasts.ShouldBe(1);
        window.ComboPointsWasted.ShouldBe(MaidenOfDeathAnalyzer.ComboPointsPerGenerator);
        window.CleanCycle.ShouldBeFalse();

        var doubled = window.Casts.Single(cast => cast.WastedGeneration);
        doubled.AbilityId.ShouldBe(Spells.WidowBite.Id);
        doubled.Role.ShouldBe(MaidenCastRole.Generator);

        analyzer.ComboPointsWasted.ShouldBe(MaidenOfDeathAnalyzer.ComboPointsPerGenerator);
    }

    [Fact]
    public async Task Analyze_WindowOpeningAtFourComboPoints_FailsAnOpeningGenerator()
    {
        var events = new List<Event>
        {
            Cast(500, Spells.Backstab, comboPoints: 0),
            Generate(600, comboPoints: MaidenOfDeathAnalyzer.OpeningComboPointLimit),
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Cast(1000, Spells.WidowBite, comboPoints: 4),
            Cast(2500, Spells.QueenFang, comboPoints: 6),
            Buff<RemoveBuffEvent>(4000, Spells.MaidenOfDeathBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.ComboPointsAtOpen.ShouldBe(MaidenOfDeathAnalyzer.OpeningComboPointLimit);
        window.WastedGeneratorCasts.ShouldBe(1);
        window.Casts[0].WastedGeneration.ShouldBeTrue();
        window.CleanCycle.ShouldBeFalse();
    }

    [Fact]
    public async Task Analyze_WindowOpeningBelowFourComboPoints_AllowsAnOpeningGenerator()
    {
        var events = new List<Event>
        {
            Cast(500, Spells.Backstab, comboPoints: 0),
            Generate(600, comboPoints: MaidenOfDeathAnalyzer.OpeningComboPointLimit - 1),
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Cast(1000, Spells.WidowBite, comboPoints: 3),
            Cast(2500, Spells.QueenFang, comboPoints: 6),
            Buff<RemoveBuffEvent>(4000, Spells.MaidenOfDeathBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.ComboPointsAtOpen.ShouldBe(MaidenOfDeathAnalyzer.OpeningComboPointLimit - 1);
        window.WastedGeneratorCasts.ShouldBe(0);
        window.CleanCycle.ShouldBeTrue();
    }

    [Fact]
    public async Task Analyze_WindowOpeningAtFourComboPoints_AllowsAnOpeningSpender()
    {
        var events = new List<Event>
        {
            Cast(500, Spells.Backstab, comboPoints: 0),
            Generate(600, comboPoints: MaidenOfDeathAnalyzer.OpeningComboPointLimit),
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Cast(1000, Spells.QueenFang, comboPoints: 4),
            Cast(2500, Spells.WidowBite, comboPoints: 0),
            Buff<RemoveBuffEvent>(4000, Spells.MaidenOfDeathBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.WastedGeneratorCasts.ShouldBe(0);
        window.CleanCycle.ShouldBeTrue();
    }

    [Fact]
    public async Task Analyze_HemorrhagingStrike_CountsAsASpender()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Cast(1000, Spells.Backstab, comboPoints: 0),
            Cast(2500, Spells.HemorrhagingStrike, comboPoints: 6),
            Cast(4000, Spells.WidowBite, comboPoints: 0),
            Buff<RemoveBuffEvent>(5500, Spells.MaidenOfDeathBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.SpenderCasts.ShouldBe(1);
        window.WastedGeneratorCasts.ShouldBe(0);
        window.Casts[1].Role.ShouldBe(MaidenCastRole.Spender);
    }

    [Fact]
    public async Task Analyze_IdleStretch_IsCountedAsLostCasts()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Cast(1000, Spells.Backstab, comboPoints: 0),
            Cast(7000, Spells.QueenFang, comboPoints: 6),
            Buff<RemoveBuffEvent>(8500, Spells.MaidenOfDeathBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        var gap = window.Gaps.ShouldHaveSingleItem();
        gap.StartTimestamp.ShouldBe(2500);
        gap.DurationMs.ShouldBe(4500);
        window.IdleMs.ShouldBe(4500);
        window.LongestGapMs.ShouldBe(4500);
        window.LostCasts.ShouldBe(3);
        window.CleanCycle.ShouldBeFalse();

        analyzer.IdleMs.ShouldBe(4500);
        analyzer.GapCount.ShouldBe(1);
        analyzer.LostCasts.ShouldBe(3);
    }

    [Fact]
    public async Task Analyze_ShortGap_IsLatencyRatherThanAMistake()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Cast(1000, Spells.Backstab, comboPoints: 0),
            Cast(2800, Spells.QueenFang, comboPoints: 6),
            Buff<RemoveBuffEvent>(4300, Spells.MaidenOfDeathBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Gaps.ShouldBeEmpty();
        window.IdleMs.ShouldBe(0);
        window.CleanCycle.ShouldBeTrue();
    }

    [Fact]
    public async Task Analyze_TrailingIdleTime_IsCounted()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Cast(1000, Spells.Backstab, comboPoints: 0),
            Buff<RemoveBuffEvent>(6000, Spells.MaidenOfDeathBuff),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        var gap = window.Gaps.ShouldHaveSingleItem();
        gap.StartTimestamp.ShouldBe(2500);
        gap.DurationMs.ShouldBe(3500);
        window.SpenderCasts.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_CastsOutsideTheWindow_AreNotCounted()
    {
        var events = new List<Event>
        {
            Cast(500, Spells.QueenFang, comboPoints: 5),
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Cast(1000, Spells.Backstab, comboPoints: 0),
            Cast(2500, Spells.QueenFang, comboPoints: 6),
            Buff<RemoveBuffEvent>(4000, Spells.MaidenOfDeathBuff),
            Cast(6000, Spells.QueenFang, comboPoints: 6),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Casts.Count.ShouldBe(2);
        window.SpenderCasts.ShouldBe(1);
        window.ComboPointsSpent.ShouldBe(6);
    }

    [Fact]
    public async Task Analyze_EnergyAtClose_IsTheLastSnapshotInsideTheWindow()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Cast(1000, Spells.QueenFang, comboPoints: 6, energy: 150),
            Cast(2500, Spells.ArachnidAssault, comboPoints: 4, energy: 90),
            Buff<RemoveBuffEvent>(4000, Spells.MaidenOfDeathBuff),
            Cast(6000, Spells.QueenFang, comboPoints: 6, energy: 60),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Windows.ShouldHaveSingleItem().EnergyAtClose.ShouldBe(90);
    }

    [Fact]
    public async Task Analyze_WindowWithoutCasts_ReportsNoEnergyAtClose()
    {
        var events = new List<Event>
        {
            Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff),
            Buff<RemoveBuffEvent>(1200, Spells.MaidenOfDeathBuff),
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

        var analyzer = await AnalyzeAsync(events, BossDungeon(200000));

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

        var analyzer = await AnalyzeAsync(events, BossDungeon(200000));

        analyzer.MaidenOfDeathRecasts.ShouldHaveSingleItem().HeldMs.ShouldBe(0);
        analyzer.TotalHeldMs.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ExposesPerPullReadPaths()
    {
        var events = new List<Event> { Buff<ApplyBuffEvent>(1000, Spells.MaidenOfDeathBuff) };

        var parser = await AnalyzeParserAsync(events, BossDungeon());

        var entry = parser.MaidenOfDeathAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;

        pull.MaidenOfDeathAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).MaidenOfDeathAnalyzer.ShouldBeSameAs(entry.Analyzer);
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
    /// Analyze, so the fixture stores in-game intent (0-6 combo points, 0-200 Energy) x100. A cast given
    /// neither resource is written with no SourceResources. No GlobalCooldown is attached, so each cast
    /// occupies <see cref="MaidenOfDeathAnalyzer.StandardGcdMs"/>.
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

    /// <summary>
    /// Sets the combo point tracker to <paramref name="comboPoints"/> from <paramref name="timestamp"/>
    /// onward. The tracker records a gain whenever a cast reports more than the pool currently has, so a
    /// builder cast with the wanted amount sets the pool without spending anything: combo points have no
    /// cost in these fixtures.
    /// </summary>
    private static CastEvent Generate(int timestamp, int comboPoints) =>
        Cast(timestamp, Spells.Backstab, comboPoints: comboPoints);

    private static ReportDungeon BossDungeon(int endTime = DungeonEnd) =>
        new(0, "", 1, null, 0, endTime, null, null, null);

    private static async Task<MaidenOfDeathAnalyzer> AnalyzeAsync(List<Event> events, ReportDungeon? dungeon = null)
    {
        var parser = await AnalyzeParserAsync(events, dungeon ?? BossDungeon());
        return parser.MaidenOfDeathAnalyzers.ShouldHaveSingleItem().Analyzer;
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
