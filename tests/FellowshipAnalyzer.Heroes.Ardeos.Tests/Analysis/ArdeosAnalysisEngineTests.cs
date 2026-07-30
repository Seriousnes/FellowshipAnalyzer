using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Core;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Ardeos.Tests.Analysis;

public sealed class ArdeosAnalysisEngineTests
{
    private const int PlayerId = 7;
    private const int TargetId = 27;
    private const int TargetInstance = 2;
    private const int OtherId = 28;
    private const int OtherInstance = 1;

    [Fact]
    public async Task Analyze_ShouldProvideGuideComponentType()
    {
        var (_, result) = await AnalyzeAsync([], new ReportFight(0, "", 0, null, 0, 0, null, null, null));

        result.GuideComponentType.ShouldNotBeNull();
    }

    [Fact]
    public async Task CleanWindow_FourDotsTwoEngulfingAndDetonateSpam_IsSuccessful()
    {
        const int anchor = 10000;
        var events = new List<Event>();
        events.AddRange(FullSetup(anchor, TargetId, TargetInstance));
        events.Add(ApplyBuff(anchor, Spells.WildfireDotBonusBuff.FSLID));
        events.Add(WildfireCast(anchor, TargetId, TargetInstance));
        events.Add(Cast(Spells.Detonate.FSLID, anchor + 500));
        events.Add(Cast(Spells.Detonate.FSLID, anchor + 1000));
        events.Add(Cast(Spells.Detonate.FSLID, anchor + 1500));
        events.Add(RemoveBuff(anchor + 9000, Spells.WildfireDotBonusBuff.FSLID));

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 25000));

        var entry = parser.WildfireComboAnalyzers.ShouldHaveSingleItem();
        var analyzer = entry.Analyzer;
        analyzer.EvaluatedWindows.ShouldBe(1);
        analyzer.SuccessfulWindows.ShouldBe(1);
        analyzer.PartialWindows.ShouldBe(0);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.TargetId.ShouldBe(TargetId);
        window.TargetInstance.ShouldBe(TargetInstance);
        window.DistinctDots.ShouldBe(4);
        window.EngulfingInstances.ShouldBe(2);
        window.DetonateCasts.ShouldBe(3);
        window.SetupSuccessful.ShouldBeTrue();
        window.Successful.ShouldBeTrue();

        var pull = entry.Pull;
        pull.WildfireComboAnalyzer.ShouldBeSameAs(analyzer);
        parser.For(pull).WildfireComboAnalyzer.ShouldBeSameAs(analyzer);
    }

    [Fact]
    public async Task EngulfingFlames_AppliedTwiceConcurrently_CountsAsTwoInstances()
    {
        const int anchor = 10000;
        var events = new List<Event>
        {
            ApplyDebuff(anchor - 2000, TargetId, TargetInstance, Spells.EngulfingFlamesDot.FSLID),
            ApplyDebuff(anchor - 1900, TargetId, TargetInstance, Spells.EngulfingFlamesDot.FSLID),
            WildfireCast(anchor, TargetId, TargetInstance),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 20000));

        var window = parser.WildfireComboAnalyzers.ShouldHaveSingleItem().Analyzer.Windows.ShouldHaveSingleItem();
        window.EngulfingInstances.ShouldBe(2);
        window.DistinctDots.ShouldBe(1);
        window.ActiveDots.ShouldContain(ArdeosDots.EngulfingFlames);
    }

    [Fact]
    public async Task IncompleteSetup_WithDetonateSpam_IsPartial()
    {
        const int anchor = 10000;
        var events = new List<Event>
        {
            ApplyDebuff(anchor - 3000, TargetId, TargetInstance, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(anchor - 3000, TargetId, TargetInstance, Spells.FireBallDot.FSLID),
            ApplyBuff(anchor, Spells.WildfireDotBonusBuff.FSLID),
            WildfireCast(anchor, TargetId, TargetInstance),
            Cast(Spells.Detonate.FSLID, anchor + 500),
            Cast(Spells.Detonate.FSLID, anchor + 1000),
            Cast(Spells.Detonate.FSLID, anchor + 1500),
            RemoveBuff(anchor + 9000, Spells.WildfireDotBonusBuff.FSLID),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 25000));

        var analyzer = parser.WildfireComboAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.SuccessfulWindows.ShouldBe(0);
        analyzer.PartialWindows.ShouldBe(1);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.DistinctDots.ShouldBe(2);
        window.DetonateCasts.ShouldBe(3);
        window.SetupSuccessful.ShouldBeFalse();
        window.Partial.ShouldBeTrue();
        window.Successful.ShouldBeFalse();
    }

    [Fact]
    public async Task IncompleteSetup_WithoutDetonateSpam_IsFail()
    {
        const int anchor = 10000;
        var events = new List<Event>
        {
            ApplyDebuff(anchor - 3000, TargetId, TargetInstance, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(anchor - 3000, TargetId, TargetInstance, Spells.FireBallDot.FSLID),
            ApplyBuff(anchor, Spells.WildfireDotBonusBuff.FSLID),
            WildfireCast(anchor, TargetId, TargetInstance),
            Cast(Spells.Detonate.FSLID, anchor + 500),
            RemoveBuff(anchor + 9000, Spells.WildfireDotBonusBuff.FSLID),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 25000));

        var analyzer = parser.WildfireComboAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.SuccessfulWindows.ShouldBe(0);
        analyzer.PartialWindows.ShouldBe(0);

        var window = analyzer.Windows.ShouldHaveSingleItem();
        window.DistinctDots.ShouldBe(2);
        window.DetonateCasts.ShouldBe(1);
        window.Partial.ShouldBeFalse();
        window.Successful.ShouldBeFalse();
    }

    [Fact]
    public async Task ExpiredDot_RemovedBeforeCast_DoesNotCount()
    {
        const int anchor = 10000;
        var events = new List<Event>
        {
            ApplyDebuff(anchor - 3000, TargetId, TargetInstance, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(anchor - 3000, TargetId, TargetInstance, Spells.FireBallDot.FSLID),
            ApplyDebuff(anchor - 3000, TargetId, TargetInstance, Spells.ApocalypseDot.FSLID),
            ApplyDebuff(anchor - 3000, TargetId, TargetInstance, Spells.FireFrogsDot.FSLID),
            RemoveDebuff(anchor - 1000, TargetId, TargetInstance, Spells.SearingBlazeDot.FSLID),
            WildfireCast(anchor, TargetId, TargetInstance),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 20000));

        var window = parser.WildfireComboAnalyzers.ShouldHaveSingleItem().Analyzer.Windows.ShouldHaveSingleItem();
        window.DistinctDots.ShouldBe(3);
        window.ActiveDots.ShouldNotContain(ArdeosDots.SearingBlaze);
    }

    [Fact]
    public async Task DotOnDifferentEnemy_DoesNotCount()
    {
        const int anchor = 10000;
        var events = new List<Event>
        {
            ApplyDebuff(anchor - 3000, OtherId, OtherInstance, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(anchor - 3000, OtherId, OtherInstance, Spells.FireBallDot.FSLID),
            ApplyDebuff(anchor - 3000, OtherId, OtherInstance, Spells.ApocalypseDot.FSLID),
            ApplyDebuff(anchor - 3000, OtherId, OtherInstance, Spells.FireFrogsDot.FSLID),
            WildfireCast(anchor, TargetId, TargetInstance),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 20000));

        var window = parser.WildfireComboAnalyzers.ShouldHaveSingleItem().Analyzer.Windows.ShouldHaveSingleItem();
        window.DistinctDots.ShouldBe(0);
        window.EngulfingInstances.ShouldBe(0);
    }

    [Fact]
    public async Task DeadEnemyBeforeCast_DotsDoNotCount_ForCastAimedElsewhere()
    {
        const int anchor = 10000;
        var events = new List<Event>
        {
            ApplyDebuff(anchor - 4000, OtherId, OtherInstance, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(anchor - 4000, OtherId, OtherInstance, Spells.FireBallDot.FSLID),
            ApplyDebuff(anchor - 4000, OtherId, OtherInstance, Spells.ApocalypseDot.FSLID),
            ApplyDebuff(anchor - 4000, OtherId, OtherInstance, Spells.FireFrogsDot.FSLID),
            Death(anchor - 2000, OtherId, OtherInstance),
            ApplyDebuff(anchor - 3000, TargetId, TargetInstance, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(anchor - 3000, TargetId, TargetInstance, Spells.FireBallDot.FSLID),
            WildfireCast(anchor, TargetId, TargetInstance),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 20000));

        var window = parser.WildfireComboAnalyzers.ShouldHaveSingleItem().Analyzer.Windows.ShouldHaveSingleItem();
        window.TargetId.ShouldBe(TargetId);
        window.DistinctDots.ShouldBe(2);
    }

    [Fact]
    public async Task NoUsableTarget_FallsBackToEnemyCarryingMostDots()
    {
        const int anchor = 10000;
        var events = new List<Event>
        {
            ApplyDebuff(anchor - 3000, OtherId, OtherInstance, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(anchor - 3000, OtherId, OtherInstance, Spells.FireBallDot.FSLID),
        };
        events.AddRange(FullSetup(anchor, TargetId, TargetInstance));
        events.Add(WildfireCast(anchor, targetId: -1, targetInstance: null));

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 20000));

        var window = parser.WildfireComboAnalyzers.ShouldHaveSingleItem().Analyzer.Windows.ShouldHaveSingleItem();
        window.TargetId.ShouldBe(TargetId);
        window.TargetInstance.ShouldBe(TargetInstance);
        window.DistinctDots.ShouldBe(4);
        window.EngulfingInstances.ShouldBe(2);
    }

    [Fact]
    public async Task NoUsableTarget_DeadEnemyIsNotChosen_OverLiveEnemy()
    {
        const int anchor = 10000;
        var events = new List<Event>
        {
            ApplyDebuff(anchor - 4000, OtherId, OtherInstance, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(anchor - 4000, OtherId, OtherInstance, Spells.FireBallDot.FSLID),
            ApplyDebuff(anchor - 4000, OtherId, OtherInstance, Spells.ApocalypseDot.FSLID),
            ApplyDebuff(anchor - 4000, OtherId, OtherInstance, Spells.FireFrogsDot.FSLID),
            Death(anchor - 2000, OtherId, OtherInstance),
            ApplyDebuff(anchor - 3000, TargetId, TargetInstance, Spells.SearingBlazeDot.FSLID),
            ApplyDebuff(anchor - 3000, TargetId, TargetInstance, Spells.FireBallDot.FSLID),
            WildfireCast(anchor, targetId: -1, targetInstance: null),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 20000));

        var window = parser.WildfireComboAnalyzers.ShouldHaveSingleItem().Analyzer.Windows.ShouldHaveSingleItem();
        window.TargetId.ShouldBe(TargetId);
        window.DistinctDots.ShouldBe(2);
    }

    [Fact]
    public async Task DetonatesOutsideBuffWindow_DoNotCountTowardSpam()
    {
        const int anchor = 10000;
        var events = new List<Event>();
        events.AddRange(FullSetup(anchor, TargetId, TargetInstance));
        events.Add(ApplyBuff(anchor, Spells.WildfireDotBonusBuff.FSLID));
        events.Add(WildfireCast(anchor, TargetId, TargetInstance));
        events.Add(Cast(Spells.Detonate.FSLID, anchor + 500));
        events.Add(Cast(Spells.Detonate.FSLID, anchor + 1000));
        events.Add(Cast(Spells.Detonate.FSLID, anchor + 1500));
        events.Add(RemoveBuff(anchor + 3000, Spells.WildfireDotBonusBuff.FSLID));
        events.Add(Cast(Spells.Detonate.FSLID, anchor + 5000));
        events.Add(Cast(Spells.Detonate.FSLID, anchor + 8000));

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 25000));

        var window = parser.WildfireComboAnalyzers.ShouldHaveSingleItem().Analyzer.Windows.ShouldHaveSingleItem();
        window.BuffWindowEnd.ShouldBe(anchor + 3000);
        window.DetonateCasts.ShouldBe(3);
        window.Successful.ShouldBeTrue();
    }

    [Fact]
    public async Task MultipleWildfireAnchors_AreEvaluatedIndependently()
    {
        var events = new List<Event>();
        events.AddRange(FullSetup(10000, TargetId, TargetInstance));
        events.Add(ApplyBuff(10000, Spells.WildfireDotBonusBuff.FSLID));
        events.Add(WildfireCast(10000, TargetId, TargetInstance));
        events.Add(Cast(Spells.Detonate.FSLID, 10500));
        events.Add(Cast(Spells.Detonate.FSLID, 11000));
        events.Add(Cast(Spells.Detonate.FSLID, 11500));
        events.Add(RemoveBuff(19000, Spells.WildfireDotBonusBuff.FSLID));

        events.Add(ApplyDebuff(59000, OtherId, OtherInstance, Spells.SearingBlazeDot.FSLID));
        events.Add(WildfireCast(60000, OtherId, OtherInstance));

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 80000));

        var analyzer = parser.WildfireComboAnalyzers.ShouldHaveSingleItem().Analyzer;
        analyzer.EvaluatedWindows.ShouldBe(2);
        analyzer.SuccessfulWindows.ShouldBe(1);
        analyzer.Windows.Count.ShouldBe(2);
        analyzer.Windows[0].Successful.ShouldBeTrue();
        analyzer.Windows[1].DistinctDots.ShouldBe(1);
        analyzer.Windows[1].Successful.ShouldBeFalse();
    }

    [Fact]
    public async Task Coverage_MarksEveryDotActiveOnTheTarget()
    {
        const int anchor = 10000;
        var events = new List<Event>();
        events.AddRange(FullSetup(anchor, TargetId, TargetInstance));
        events.Add(WildfireCast(anchor, TargetId, TargetInstance));

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 20000));

        var window = parser.WildfireComboAnalyzers.ShouldHaveSingleItem().Analyzer.Windows.ShouldHaveSingleItem();
        window.Coverage.Count.ShouldBe(ArdeosDots.Count);
        window.Coverage.Select(entry => entry.Dot).ShouldBe(ArdeosDots.All);

        var engulfing = Coverage(window, ArdeosDots.EngulfingFlames);
        engulfing.Active.ShouldBeTrue();
        engulfing.Instances.ShouldBe(2);
        engulfing.Magnitude.ShouldBe(2);

        var searing = Coverage(window, ArdeosDots.SearingBlaze);
        searing.Active.ShouldBeTrue();
        searing.Magnitude.ShouldBeNull();

        Coverage(window, ArdeosDots.FireFrogs).Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Coverage_IncinerateStacks_ReadStackCountAtEachCast()
    {
        var events = new List<Event>
        {
            ApplyDebuff(1000, TargetId, TargetInstance, Spells.IncinerateDot.FSLID),
            ApplyDebuffStack(1500, TargetId, TargetInstance, Spells.IncinerateDot.FSLID, stack: 4),
            WildfireCast(2000, TargetId, TargetInstance),
            ApplyDebuffStack(50000, TargetId, TargetInstance, Spells.IncinerateDot.FSLID, stack: 11),
            WildfireCast(51000, TargetId, TargetInstance),
        };

        var (parser, _) = await AnalyzeAsync(events, SpanningFight(0, 70000));

        var windows = parser.WildfireComboAnalyzers.ShouldHaveSingleItem().Analyzer.Windows;
        windows.Count.ShouldBe(2);
        Coverage(windows[0], ArdeosDots.Incinerate).Magnitude.ShouldBe(4);
        Coverage(windows[1], ArdeosDots.Incinerate).Magnitude.ShouldBe(11);
    }

    private static DotCoverage Coverage(
        WildfireComboAnalyzer.WildfireWindowEvaluation window,
        Dot dot) =>
        window.Coverage.Single(entry => entry.Dot == dot);

    private static IEnumerable<Event> FullSetup(int anchor, int targetId, int targetInstance) =>
    [
        ApplyDebuff(anchor - 3000, targetId, targetInstance, Spells.SearingBlazeDot.FSLID),
        ApplyDebuff(anchor - 3000, targetId, targetInstance, Spells.FireBallDot.FSLID),
        ApplyDebuff(anchor - 3000, targetId, targetInstance, Spells.ApocalypseDot.FSLID),
        ApplyDebuff(anchor - 2000, targetId, targetInstance, Spells.EngulfingFlamesDot.FSLID),
        ApplyDebuff(anchor - 1900, targetId, targetInstance, Spells.EngulfingFlamesDot.FSLID),
    ];

    private static CastEvent Cast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = abilityId },
    };

    private static CastEvent WildfireCast(int timestamp, int targetId, int? targetInstance) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { Id = Spells.Wildfire.FSLID },
    };

    private static ApplyDebuffEvent ApplyDebuff(int timestamp, int targetId, int? targetInstance, int effectId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static ApplyDebuffStackEvent ApplyDebuffStack(int timestamp, int targetId, int? targetInstance, int effectId, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Stack = stack,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static RemoveDebuffEvent RemoveDebuff(int timestamp, int targetId, int? targetInstance, int effectId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static ApplyBuffEvent ApplyBuff(int timestamp, int effectId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static RemoveBuffEvent RemoveBuff(int timestamp, int effectId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = effectId, Name = $"Effect {effectId}" },
    };

    private static DeathEvent Death(int timestamp, int targetId, int? targetInstance) => new()
    {
        Timestamp = timestamp,
        TargetId = targetId,
        TargetInstance = targetInstance,
    };

    private static ReportFight SpanningFight(double startTime, double endTime) =>
        new(0, "", 0, null, startTime, endTime, null, null, null);

    private static async Task<(ArdeosCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(List<Event> events, ReportFight fight)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddArdeosAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ArdeosCombatLogParser>();
        var result = await parser.Analyze(events, PlayerId, fight);
        return (parser, result);
    }
}
