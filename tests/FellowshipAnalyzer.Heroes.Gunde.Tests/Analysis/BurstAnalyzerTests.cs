using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Gunde.Analysis;
using FellowshipAnalyzer.Heroes.Gunde.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Gunde.Spells;

namespace FellowshipAnalyzer.Heroes.Gunde.Tests.Analysis;

public sealed class BurstAnalyzerTests
{
    private const int PlayerId = 4;
    private const int BossId = 99;
    private const int PullEnd = 200_000;
    private const int BuffDurationMs = 12_000;

    [Fact]
    public async Task Analyze_WindowHoldingAllThreeCooldowns_IsFull()
    {
        var events = new List<Event>
        {
            Cast(Spells.BloodboundSpirit.FSLID, 8_000),
            WindowOpen(10_000),
            Cast(Spells.Rupture.FSLID, 11_000),
            Cast(Spells.OwedInBlood.FSLID, 12_500),
            WindowClose(22_000),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Start.ShouldBe(10_000);
        window.End.ShouldBe(22_000);
        window.RuptureIn.ShouldBeTrue();
        window.BloodboundSpiritIn.ShouldBeTrue();
        window.OwedInBloodIn.ShouldBeTrue();
        window.PresentCount.ShouldBe(3);

        analyzer.WindowCount.ShouldBe(1);
        analyzer.FullWindows.ShouldBe(1);
        analyzer.OutOfWindowRuptures.ShouldBe(0);
        analyzer.OutOfWindowBloodboundSpirits.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_CastBeforeTheLeadIn_IsNotClaimedByTheWindow()
    {
        var events = new List<Event>
        {
            Cast(Spells.BloodboundSpirit.FSLID, 6_500),
            WindowOpen(10_000),
            WindowClose(22_000),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.BloodboundSpiritIn.ShouldBeFalse();
        analyzer.OutOfWindowBloodboundSpirits.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_RuptureCastFarOutsideTheWindow_CountsAsOutOfWindow()
    {
        var events = new List<Event>
        {
            WindowOpen(10_000),
            Cast(Spells.BloodboundSpirit.FSLID, 10_500),
            Cast(Spells.OwedInBlood.FSLID, 11_000),
            WindowClose(22_000),
            Cast(Spells.Rupture.FSLID, 60_000),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.RuptureIn.ShouldBeFalse();
        window.PresentCount.ShouldBe(2);

        analyzer.FullWindows.ShouldBe(0);
        analyzer.OutOfWindowRuptures.ShouldBe(1);
        analyzer.OutOfWindowBloodboundSpirits.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_OwedInBloodOutsideEveryWindow_IsNotAMiss()
    {
        var events = new List<Event>
        {
            WindowOpen(10_000),
            WindowClose(22_000),
            Cast(Spells.OwedInBlood.FSLID, 60_000),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Windows.ShouldHaveSingleItem().OwedInBloodIn.ShouldBeFalse();
        analyzer.OutOfWindowRuptures.ShouldBe(0);
        analyzer.OutOfWindowBloodboundSpirits.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_BuffWithNoRemoval_ClosesAtTheBuffDuration()
    {
        var events = new List<Event>
        {
            WindowOpen(10_000),
            Cast(Spells.Rupture.FSLID, 11_000),
        };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Start.ShouldBe(10_000);
        window.End.ShouldBe(10_000 + BuffDurationMs);
        window.RuptureIn.ShouldBeTrue();
    }

    [Fact]
    public async Task Analyze_BuffNearPullEnd_WithNoRemoval_ClampsToPullEnd()
    {
        var events = new List<Event> { WindowOpen(PullEnd - 4_000) };

        var analyzer = await AnalyzeAsync(events);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.Start.ShouldBe(PullEnd - 4_000);
        window.End.ShouldBe(PullEnd);
    }

    [Fact]
    public async Task Analyze_OneRupture_IsClaimedByASingleWindow()
    {
        var events = new List<Event>
        {
            WindowOpen(10_000),
            Cast(Spells.Rupture.FSLID, 11_000),
            WindowClose(22_000),
            WindowOpen(100_000),
            WindowClose(112_000),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Windows.Count.ShouldBe(2);
        analyzer.Windows[0].RuptureIn.ShouldBeTrue();
        analyzer.Windows[1].RuptureIn.ShouldBeFalse();
        analyzer.OutOfWindowRuptures.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ARuptureInEachWindow_FillsBothWindows()
    {
        var events = new List<Event>
        {
            WindowOpen(10_000),
            Cast(Spells.Rupture.FSLID, 11_000),
            WindowClose(22_000),
            WindowOpen(100_000),
            Cast(Spells.Rupture.FSLID, 101_000),
            WindowClose(112_000),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Windows.Count.ShouldBe(2);
        analyzer.Windows[0].RuptureIn.ShouldBeTrue();
        analyzer.Windows[1].RuptureIn.ShouldBeTrue();
        analyzer.OutOfWindowRuptures.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_CastInsideAWindowAndTheNextWindowsLeadIn_GoesToTheEarlierWindow()
    {
        var events = new List<Event>
        {
            WindowOpen(10_000),
            Cast(Spells.BloodboundSpirit.FSLID, 21_000),
            WindowClose(22_000),
            WindowOpen(23_000),
            WindowClose(35_000),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Windows.Count.ShouldBe(2);
        analyzer.Windows[0].BloodboundSpiritIn.ShouldBeTrue();
        analyzer.Windows[1].BloodboundSpiritIn.ShouldBeFalse();
        analyzer.OutOfWindowBloodboundSpirits.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_TwoBloodboundSpiritsInsideOneWindow_CountsNeitherOutOfWindow()
    {
        var events = new List<Event>
        {
            WindowOpen(10_000),
            Cast(Spells.BloodboundSpirit.FSLID, 10_500),
            Cast(Spells.BloodboundSpirit.FSLID, 15_000),
            WindowClose(22_000),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Windows.ShouldHaveSingleItem().BloodboundSpiritIn.ShouldBeTrue();
        analyzer.OutOfWindowBloodboundSpirits.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ASecondRuptureInsideASatisfiedWindow_LeavesTheNextWindowUnsatisfied()
    {
        var events = new List<Event>
        {
            WindowOpen(10_000),
            Cast(Spells.Rupture.FSLID, 11_000),
            Cast(Spells.Rupture.FSLID, 15_000),
            WindowClose(22_000),
            WindowOpen(100_000),
            WindowClose(112_000),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Windows[0].RuptureIn.ShouldBeTrue();
        analyzer.Windows[1].RuptureIn.ShouldBeFalse();
        analyzer.OutOfWindowRuptures.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_NoWindows_ReportsEveryRuptureAndSpiritOutOfWindow()
    {
        var events = new List<Event>
        {
            Cast(Spells.Rupture.FSLID, 10_000),
            Cast(Spells.Rupture.FSLID, 70_000),
            Cast(Spells.BloodboundSpirit.FSLID, 30_000),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Windows.ShouldBeEmpty();
        analyzer.WindowCount.ShouldBe(0);
        analyzer.FullWindows.ShouldBe(0);
        analyzer.OutOfWindowRuptures.ShouldBe(2);
        analyzer.OutOfWindowBloodboundSpirits.ShouldBe(1);
    }

    [Fact]
    public void ReignInBlood_IsCuratedAsANinetySecondCooldown()
    {
        Spells.ReignInBlood.Cooldown.ShouldBe(90d);
    }

    [Fact]
    public async Task Analyze_TrashPull_IsAlsoEvaluated()
    {
        var events = new List<Event>
        {
            WindowOpen(10_000),
            WindowClose(22_000),
        };

        var (parser, _) = await RunAsync(events, TrashDungeon(PullEnd));

        parser.BurstAnalyzers.ShouldHaveSingleItem()
            .Analyzer.Windows.ShouldHaveSingleItem().Start.ShouldBe(10_000);
    }

    [Fact]
    public async Task Analyze_DungeonRun_ScoresEachPullSeparately()
    {
        var events = new List<Event>
        {
            WindowOpen(210_000),
            Cast(Spells.Rupture.FSLID, 211_000),
            WindowClose(222_000),
        };

        var (parser, _) = await RunAsync(events, DungeonRun());

        var entries = parser.BurstAnalyzers;
        entries.Count.ShouldBe(3);

        var firstTrash = entries[0];
        firstTrash.Pull.IsBoss.ShouldBeFalse();
        firstTrash.Analyzer.WindowCount.ShouldBe(0);

        var secondTrash = entries[1];
        secondTrash.Pull.IsBoss.ShouldBeFalse();
        secondTrash.Analyzer.WindowCount.ShouldBe(0);

        var boss = entries[2];
        boss.Pull.IsBoss.ShouldBeTrue();
        boss.Analyzer.WindowCount.ShouldBe(1);
        boss.Analyzer.Windows.ShouldHaveSingleItem().RuptureIn.ShouldBeTrue();
    }

    [Fact]
    public async Task Analyze_RetainsTheAnalyzerOnEveryPullReadPath()
    {
        var (parser, _) = await RunAsync([WindowOpen(10_000)], BossDungeon(PullEnd));

        var entry = parser.BurstAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;

        pull.BurstAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).BurstAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static ApplyBuffEvent WindowOpen(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.ReignInBloodSelfBuff.FSLID },
    };

    private static RemoveBuffEvent WindowClose(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.ReignInBloodSelfBuff.FSLID },
    };

    private static CastEvent Cast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = BossId,
        Ability = new Ability { Id = abilityId },
        Target = new CastTarget(),
    };

    private static ReportDungeon BossDungeon(int endTime) =>
        new(0, "Boss", 1, null, 0, endTime, null, null, null);

    private static ReportDungeon TrashDungeon(int endTime) =>
        new(0, "Trash", 0, null, 0, endTime, null, null, null, EnemyNpcs: [new DungeonNpc(1, 100, 4, 1, null)]);

    private static ReportDungeon DungeonRun() =>
        new(0, "Dungeon", 0, null, 0, 400_000, null, null, null, DungeonPulls:
        [
            new DungeonPull(1, 0, true, 0, 20_000, "Trash", null),
            new DungeonPull(2, 0, true, 40_000, 55_000, "Trash", null),
            new DungeonPull(3, 5, true, 200_000, 400_000, "Boss", null),
        ]);

    private static async Task<BurstAnalyzer> AnalyzeAsync(List<Event> events)
    {
        var (parser, _) = await RunAsync(events, BossDungeon(PullEnd));
        return parser.BurstAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<(GundeCombatLogParser Parser, HeroAnalysisResult Result)> RunAsync(
        List<Event> events, ReportDungeon dungeon)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddGundeAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<GundeCombatLogParser>();
        var result = await parser.Analyze(events, PlayerId, dungeon);
        return (parser, result);
    }

    private sealed class CastTarget : ICastTarget
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public int Guid { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}
