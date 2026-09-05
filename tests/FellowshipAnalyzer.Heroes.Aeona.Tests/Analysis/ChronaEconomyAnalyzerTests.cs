using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

/// <summary>
/// Exercises the pull's Chrona and mana economy against fabricated snapshots. Aeona logs carry no
/// resource-change event, so generation and spending reach the analyzer only through the deltas
/// <c>ChronaTracker</c> reconstructs; snapshot amounts are written at the raw log scale because
/// <c>ResourceNormalizer</c> divides them by 100 before dispatch.
/// </summary>
public sealed class ChronaEconomyAnalyzerTests
{
    private const int PlayerId = 7;
    private const int TankId = 9;
    private const int EnemyId = 100;
    private const int RawChronaCap = 10_000;
    private const int RawManaCap = 165_600;
    private const int DungeonEndMs = 120_000;

    [Fact]
    public async Task Analyzer_IsConstructedForThePullAndReachableFromBothReadPaths()
    {
        var parser = await AnalyzeAsync(Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 4_000));

        var entry = parser.ChronaEconomyAnalyzers.ShouldHaveSingleItem();
        entry.Pull.ChronaEconomyAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(entry.Pull).ChronaEconomyAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    [Fact]
    public async Task GenerationAndSpending_AreReadFromTheTrackerForThePullWindow()
    {
        var analyzer = await Analyze(
            Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 4_000),
            Heal(2_000, Spells.EntropyClaim.FSLID, rawChrona: 4_600),
            Cast(3_000, Spells.Oblivion.FSLID, rawChrona: 4_600),
            Heal(4_000, Spells.Oblivion.FSLID, rawChrona: 1_600));

        analyzer.ChronaGenerated.ShouldBe(6);
        analyzer.ChronaSpent.ShouldBe(30);
        analyzer.ChronaMaximum.ShouldBe(100);
        analyzer.ObservedChronaSpends.ShouldBe(1);
    }

    [Fact]
    public async Task ChronaHeldAtTheMaximum_IsMeasuredFromTheFillingGainToTheNextChange()
    {
        var analyzer = await Analyze(
            Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 9_000),
            Heal(2_000, Spells.EntropyClaim.FSLID, rawChrona: RawChronaCap),
            Cast(5_000, Spells.Oblivion.FSLID, rawChrona: RawChronaCap),
            Heal(6_000, Spells.Oblivion.FSLID, rawChrona: 7_000));

        analyzer.ChronaGainsAtMaximum.ShouldBe(1);
        analyzer.ChronaTimeAtMaximumMs.ShouldBe(4_000);
        analyzer.ChronaTimeAtMaximumShare.ShouldBe(4_000d / DungeonEndMs, 0.0001);
    }

    [Fact]
    public async Task ChronaStillAtTheMaximumWhenThePullEnds_IsMeasuredToThePullEnd()
    {
        var analyzer = await Analyze(
            Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 9_000),
            Heal(2_000, Spells.EntropyClaim.FSLID, rawChrona: RawChronaCap));

        analyzer.ChronaGainsAtMaximum.ShouldBe(1);
        analyzer.ChronaTimeAtMaximumMs.ShouldBe(DungeonEndMs - 2_000);
    }

    [Fact]
    public async Task ChronaNeverReachingTheMaximum_ReportsNoTimeThere()
    {
        var analyzer = await Analyze(
            Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 4_000),
            Heal(2_000, Spells.EntropyClaim.FSLID, rawChrona: 9_900));

        analyzer.ChronaGainsAtMaximum.ShouldBe(0);
        analyzer.ChronaTimeAtMaximumMs.ShouldBe(0);
        analyzer.ChronaTimeAtMaximumShare.ShouldBe(0);
    }

    [Fact]
    public async Task ManaHeldAtTheMaximum_IsMeasuredTheSameWay()
    {
        var analyzer = await Analyze(
            Heal(1_000, Spells.EntropyClaim.FSLID, rawMana: 160_000),
            Heal(2_000, Spells.EntropyClaim.FSLID, rawMana: RawManaCap),
            Cast(9_000, Spells.Oblivion.FSLID, rawMana: RawManaCap),
            Heal(10_000, Spells.Oblivion.FSLID, rawMana: 100_000));

        analyzer.ManaMaximum.ShouldBe(1_656);
        analyzer.ManaGainsAtMaximum.ShouldBe(1);
        analyzer.ManaTimeAtMaximumMs.ShouldBe(8_000);
        analyzer.ManaTimeAtMaximumShare.ShouldBe(8_000d / DungeonEndMs, 0.0001);
    }

    [Fact]
    public async Task GenerationBelowHalfTheMaximum_IsCountedFromTheAmountEachGainLandedOn()
    {
        var analyzer = await Analyze(
            Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 3_000),
            Heal(2_000, Spells.EntropyClaim.FSLID, rawChrona: 3_600),
            Heal(3_000, Spells.EntropyClaim.FSLID, rawChrona: 5_500),
            Heal(4_000, Spells.EntropyClaim.FSLID, rawChrona: 6_000));

        analyzer.SynchronicityThreshold.ShouldBe(50);
        analyzer.ChronaGeneratedBelowSynchronicityThreshold.ShouldBe(25);
    }

    [Fact]
    public async Task GenerationBelowTheThreshold_RecoversThePreGainAmountAcrossTheCapAsWell()
    {
        var analyzer = await Analyze(
            Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 4_000),
            ResourceChange(2_000, Spells.EntropyClaim.FSLID, ResourceTypes.Primary, change: 80, waste: 0));

        analyzer.ChronaGeneratedBelowSynchronicityThreshold.ShouldBe(60);
        analyzer.ChronaGainsAtMaximum.ShouldBe(1);
    }

    [Fact]
    public async Task WithoutSynchronicity_TheEstimateIsAbsentRatherThanZero()
    {
        var analyzer = await Analyze(
            Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 3_000),
            Heal(2_000, Spells.EntropyClaim.FSLID, rawChrona: 3_600));

        analyzer.SynchronicityTalented.ShouldBeFalse();
        analyzer.ChronaGeneratedBelowSynchronicityThreshold.ShouldBe(6);
        analyzer.EstimatedSynchronicityChrona.ShouldBeNull();
    }

    [Fact]
    public async Task WithSynchronicity_TheEstimateIsTheIncreasesShareOfTheObservedGeneration()
    {
        var analyzer = await Analyze(
            CombatantInfo(AeonaTalents.Synchronicity),
            Heal(1_000, Spells.EntropyClaim.FSLID, rawChrona: 3_000),
            Heal(2_000, Spells.EntropyClaim.FSLID, rawChrona: 5_500));

        analyzer.SynchronicityTalented.ShouldBeTrue();
        analyzer.ChronaGeneratedBelowSynchronicityThreshold.ShouldBe(25);
        analyzer.EstimatedSynchronicityChrona.ShouldNotBeNull().ShouldBe(5d, 0.0001);
    }

    [Fact]
    public async Task ACleanseCast_ReportsTheStaggerItClearedAndTheManaThatFollowed()
    {
        var analyzer = await Analyze(
            Heal(1_000, Spells.EntropyClaim.FSLID, rawMana: 100_000, targetRawStagger: 200_000),
            CleanseCast(5_000, Spells.AmendFate.FSLID),
            Heal(5_001, Spells.AmendFate.FSLID, rawMana: 112_000, targetRawStagger: 50_000));

        var cleanse = analyzer.CleanseReturns.ShouldHaveSingleItem();
        cleanse.Timestamp.ShouldBe(5_000);
        cleanse.Ability.ShouldBe(Spells.AmendFate.FSLID);
        cleanse.StaggerCleared.ShouldBe(1_500);
        cleanse.ManaRestored.ShouldBe(120);
        cleanse.HasInterveningEvent.ShouldBeFalse();

        analyzer.CleanseCasts.ShouldBe(1);
        analyzer.StaggerClearedByCleansing.ShouldBe(1_500);
        analyzer.EstimatedManaFromCleansing.ShouldBe(120);
        analyzer.CleansesWithoutManaReading.ShouldBe(0);
        analyzer.CleansesWithoutStaggerReading.ShouldBe(0);
    }

    [Fact]
    public async Task ACleanseCastWithNoManaSnapshotInsideItsWindow_ReportsNoReadingRatherThanZero()
    {
        var analyzer = await Analyze(
            Heal(1_000, Spells.EntropyClaim.FSLID, rawMana: 100_000, targetRawStagger: 200_000),
            CleanseCast(5_000, Spells.AmendFate.FSLID),
            Heal(5_001, Spells.AmendFate.FSLID, targetRawStagger: 50_000));

        var cleanse = analyzer.CleanseReturns.ShouldHaveSingleItem();
        cleanse.StaggerCleared.ShouldBe(1_500);
        cleanse.ManaRestored.ShouldBeNull();

        analyzer.EstimatedManaFromCleansing.ShouldBe(0);
        analyzer.CleansesWithoutManaReading.ShouldBe(1);
    }

    [Fact]
    public async Task ACleanseCastWithNoStaggerReadingBeforeIt_ReportsNoClearedAmount()
    {
        var analyzer = await Analyze(
            CleanseCast(5_000, Spells.AmendFate.FSLID),
            Heal(5_001, Spells.AmendFate.FSLID, rawMana: 112_000, targetRawStagger: 50_000));

        var cleanse = analyzer.CleanseReturns.ShouldHaveSingleItem();
        cleanse.StaggerCleared.ShouldBeNull();

        analyzer.StaggerClearedByCleansing.ShouldBe(0);
        analyzer.CleansesWithoutStaggerReading.ShouldBe(1);
    }

    [Fact]
    public async Task ManaArrivingAfterTheNextCleanseCast_IsAttributedToThatCastInstead()
    {
        var analyzer = await Analyze(
            Heal(1_000, Spells.EntropyClaim.FSLID, rawMana: 100_000, targetRawStagger: 200_000),
            CleanseCast(5_000, Spells.AmendFate.FSLID),
            Heal(5_001, Spells.AmendFate.FSLID, rawMana: 101_000, targetRawStagger: 150_000),
            CleanseCast(5_500, Spells.RestoreContinuity.FSLID),
            Heal(5_600, Spells.RestoreContinuity.FSLID, rawMana: 120_000, targetRawStagger: 50_000));

        analyzer.CleanseReturns.Count.ShouldBe(2);
        analyzer.CleanseReturns[0].ManaRestored.ShouldBe(10);
        analyzer.CleanseReturns[1].ManaRestored.ShouldBe(190);
    }

    [Fact]
    public async Task ASecondCleanseInsideTheMeasuredBracket_IsReportedAsAnInterveningEvent()
    {
        var analyzer = await Analyze(
            Heal(1_000, Spells.EntropyClaim.FSLID, rawMana: 100_000, targetRawStagger: 200_000),
            CleanseCast(5_000, Spells.AmendFate.FSLID),
            CleanseCast(5_200, Spells.RestoreContinuity.FSLID),
            Heal(5_300, Spells.AmendFate.FSLID, rawMana: 112_000, targetRawStagger: 50_000));

        analyzer.CleanseReturns[0].HasInterveningEvent.ShouldBeTrue();
    }

    [Fact]
    public async Task ChronaTapWindows_RecordTheStacksHeldWhenEachOneEnded()
    {
        var analyzer = await Analyze(
            CombatantInfo(AeonaTalents.ChronaTap),
            Heal(500, Spells.EntropyClaim.FSLID, rawMana: RawManaCap),
            ChronaTapApply(1_000),
            ChronaTapStack(2_000, stack: 2),
            ChronaTapStack(3_000, stack: 3),
            ChronaTapRemove(10_000),
            ChronaTapApply(20_000));

        analyzer.ChronaTapTalented.ShouldBeTrue();
        analyzer.ChronaTapWindows.ShouldBe(
        [
            new ChronaTapWindow(1_000, 10_000, 3, Expired: true),
            new ChronaTapWindow(20_000, DungeonEndMs, 1, Expired: false),
        ]);

        analyzer.ChronaTapStacksGained.ShouldBe(4);
    }

    [Fact]
    public async Task ChronaTapMana_CountsOnlyTheWindowsThatExpired()
    {
        var analyzer = await Analyze(
            CombatantInfo(AeonaTalents.ChronaTap),
            Heal(500, Spells.EntropyClaim.FSLID, rawMana: RawManaCap),
            ChronaTapApply(1_000),
            ChronaTapStack(2_000, stack: 2),
            ChronaTapStack(3_000, stack: 3),
            ChronaTapRemove(10_000),
            ChronaTapApply(20_000));

        analyzer.ManaMaximum.ShouldBe(1_656);
        analyzer.EstimatedChronaTapMana.ShouldNotBeNull()
            .ShouldBe(3 * ChronaEconomyAnalyzer.ChronaTapManaSharePerStack * 1_656, 0.0001);
    }

    [Fact]
    public async Task ChronaTapStacksPerObservedSpend_DividesStacksByTheSpendsTheSnapshotsRevealed()
    {
        var analyzer = await Analyze(
            CombatantInfo(AeonaTalents.ChronaTap),
            Heal(500, Spells.EntropyClaim.FSLID, rawChrona: RawChronaCap, rawMana: RawManaCap),
            ChronaTapApply(1_000),
            Cast(2_000, Spells.Oblivion.FSLID, rawChrona: RawChronaCap),
            Heal(2_500, Spells.Oblivion.FSLID, rawChrona: 7_000),
            ChronaTapStack(2_600, stack: 2),
            ChronaTapRemove(11_600));

        analyzer.ObservedChronaSpends.ShouldBe(1);
        analyzer.ChronaTapStacksGained.ShouldBe(2);
        analyzer.ChronaTapStacksPerObservedSpend.ShouldNotBeNull().ShouldBe(2d, 0.0001);
    }

    [Fact]
    public async Task WithoutChronaTap_ItsReturnAndRatioAreAbsentRatherThanZero()
    {
        var analyzer = await Analyze(
            Heal(500, Spells.EntropyClaim.FSLID, rawChrona: RawChronaCap, rawMana: RawManaCap),
            Cast(2_000, Spells.Oblivion.FSLID, rawChrona: RawChronaCap),
            Heal(2_500, Spells.Oblivion.FSLID, rawChrona: 7_000));

        analyzer.ChronaTapTalented.ShouldBeFalse();
        analyzer.ObservedChronaSpends.ShouldBe(1);
        analyzer.EstimatedChronaTapMana.ShouldBeNull();
        analyzer.ChronaTapStacksPerObservedSpend.ShouldBeNull();
    }

    [Fact]
    public async Task ChronaTapRefreshedWithNoApplicationBeforeIt_OpensAWindowFromTheRefresh()
    {
        var analyzer = await Analyze(
            CombatantInfo(AeonaTalents.ChronaTap),
            Heal(500, Spells.EntropyClaim.FSLID, rawMana: RawManaCap),
            ChronaTapRefresh(1_000),
            ChronaTapStack(2_000, stack: 4),
            ChronaTapRemove(9_000));

        analyzer.ChronaTapWindows.ShouldBe([new ChronaTapWindow(1_000, 9_000, 4, Expired: true)]);
        analyzer.ChronaTapStacksGained.ShouldBe(4);
    }

    [Fact]
    public async Task ChronaTapRefreshedInsideAnOpenWindow_LeavesTheWindowAndItsStacksAlone()
    {
        var analyzer = await Analyze(
            CombatantInfo(AeonaTalents.ChronaTap),
            Heal(500, Spells.EntropyClaim.FSLID, rawMana: RawManaCap),
            ChronaTapApply(1_000),
            ChronaTapStack(2_000, stack: 3),
            ChronaTapRefresh(3_000),
            ChronaTapRemove(9_000));

        analyzer.ChronaTapWindows.ShouldBe([new ChronaTapWindow(1_000, 9_000, 3, Expired: true)]);
    }

    [Fact]
    public async Task ChronaTapStackedWithNoApplicationBeforeIt_OpensAWindowFromTheStack()
    {
        var analyzer = await Analyze(
            CombatantInfo(AeonaTalents.ChronaTap),
            Heal(500, Spells.EntropyClaim.FSLID, rawMana: RawManaCap),
            ChronaTapStack(2_000, stack: 5),
            ChronaTapRemove(9_000));

        analyzer.ChronaTapWindows.ShouldBe([new ChronaTapWindow(2_000, 9_000, 5, Expired: true)]);
    }

    [Fact]
    public async Task ChronaTapWithNoObservedSpend_ReportsNoRatio()
    {
        var analyzer = await Analyze(
            CombatantInfo(AeonaTalents.ChronaTap),
            Heal(500, Spells.EntropyClaim.FSLID, rawMana: RawManaCap),
            ChronaTapApply(1_000),
            ChronaTapRemove(10_000));

        analyzer.ObservedChronaSpends.ShouldBe(0);
        analyzer.ChronaTapStacksPerObservedSpend.ShouldBeNull();
    }

    [Fact]
    public async Task AReportWithNoResourceSnapshots_ReportsEveryQuantityAsZero()
    {
        var analyzer = await Analyze(Cast(1_000, Spells.Oblivion.FSLID));

        analyzer.ChronaGenerated.ShouldBe(0);
        analyzer.ChronaSpent.ShouldBe(0);
        analyzer.ChronaTimeAtMaximumMs.ShouldBe(0);
        analyzer.ManaMaximum.ShouldBe(0);
        analyzer.CleanseReturns.ShouldBeEmpty();
        analyzer.ChronaTapWindows.ShouldBeEmpty();
        analyzer.EstimatedChronaTapMana.ShouldBeNull();
    }

    private static ActorResources? PlayerResources(int? rawChrona, int? rawMana)
    {
        if (rawChrona is null && rawMana is null) return null;

        var resources = new List<ClassResource>();
        if (rawMana is { } mana)
            resources.Add(new ClassResource { Type = ResourceTypes.Mana, Amount = mana, Max = RawManaCap });
        if (rawChrona is { } chrona)
            resources.Add(new ClassResource { Type = ResourceTypes.Primary, Amount = chrona, Max = RawChronaCap });

        return new ActorResources { HitPoints = 20_000, MaxHitPoints = 30_000, Resources = resources };
    }

    private static ActorResources StaggerResources(int rawStagger) => new()
    {
        HitPoints = 25_000,
        MaxHitPoints = 40_000,
        Resources = [new ClassResource { Type = ResourceTypes.Stagger, Amount = rawStagger, Max = -100 }],
    };

    private static HealEvent Heal(
        int timestamp,
        int abilityId,
        int? rawChrona = null,
        int? rawMana = null,
        int? targetRawStagger = null) => new()
        {
            Timestamp = timestamp,
            SourceId = PlayerId,
            TargetId = TankId,
            Amount = 1_000,
            Ability = new Ability { FSLID = abilityId, Name = $"Spell {abilityId}" },
            SourceResources = PlayerResources(rawChrona, rawMana),
            TargetResources = targetRawStagger is { } stagger ? StaggerResources(stagger) : null,
        };

    private static CastEvent Cast(int timestamp, int abilityId, int? rawChrona = null, int? rawMana = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { FSLID = abilityId, Name = $"Spell {abilityId}" },
        Target = new StubCastTarget(),
        Channel = new EndChannelEvent(),
        SourceResources = PlayerResources(rawChrona, rawMana),
    };

    private static CastEvent CleanseCast(int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = -1,
        Ability = new Ability { FSLID = abilityId, Name = $"Spell {abilityId}" },
        Target = new StubCastTarget(),
        Channel = new EndChannelEvent(),
    };

    private static ResourceChangeEvent ResourceChange(
        int timestamp,
        int abilityId,
        ResourceTypes type,
        double change,
        double waste) => new()
        {
            Timestamp = timestamp,
            SourceId = PlayerId,
            TargetId = PlayerId,
            Ability = new Ability { FSLID = abilityId, Name = $"Spell {abilityId}" },
            ResourceChangeType = type,
            ResourceChange = change,
            Waste = waste,
        };

    private static ApplyBuffEvent ChronaTapApply(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.ChronaTap.FSLID },
    };

    private static ApplyBuffStackEvent ChronaTapStack(int timestamp, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = new Ability { Id = Spells.ChronaTap.FSLID },
    };

    private static RefreshBuffEvent ChronaTapRefresh(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.ChronaTap.FSLID },
    };

    private static RemoveBuffEvent ChronaTapRemove(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.ChronaTap.FSLID },
    };

    private static CombatantInfoEvent CombatantInfo(params int[] talentIds) => new()
    {
        SourceId = PlayerId,
        Talents = [.. talentIds.Select(id => new TalentInfo { Id = id })],
    };

    private static ReportDungeon Dungeon() =>
        new(0, "Boss", 1, true, 0, DungeonEndMs, null, null, null);

    private static async Task<ChronaEconomyAnalyzer> Analyze(params Event[] events)
    {
        var parser = await AnalyzeAsync(events);
        return parser.ChronaEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<AeonaCombatLogParser> AnalyzeAsync(params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, Dungeon());
        return parser;
    }

    private sealed class StubCastTarget : ICastTarget
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public int Guid { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}
