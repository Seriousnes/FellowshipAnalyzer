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

public sealed class OwedInBloodEconomyTests
{
    private const int PlayerId = 4;
    private const int BossId = 99;
    private const int PullEnd = 200_000;

    [Fact]
    public async Task Analyze_BankBuiltThenCashedIn_RecordsTheConversion()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 10),
            StackApplied(3_000, 40),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_100),
        };

        var analyzer = await AnalyzeAsync(events);

        var conversion = analyzer.Conversions.ShouldHaveSingleItem();
        conversion.Timestamp.ShouldBe(10_000);
        conversion.StacksConverted.ShouldBe(40);

        analyzer.TotalStacksConverted.ShouldBe(40);
        analyzer.AverageConversion.ShouldBe(40d);
        analyzer.DecayedStacks.ShouldBe(0);
        analyzer.CappedMs.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ApplyWithoutAStackEvent_CountsASingleFeather()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            Cast(Spells.OwedInBlood.FSLID, 5_000),
            BuffRemoved(5_100),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.ShouldHaveSingleItem().StacksConverted.ShouldBe(1);
        analyzer.TotalStacksConverted.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_TwoConversionsOfDifferentSizes_ReportsTheTotalAndAverage()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 20),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_100),
            BuffApplied(20_000),
            StackApplied(25_000, 60),
            Cast(Spells.OwedInBlood.FSLID, 30_000),
            BuffRemoved(30_100),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.Count.ShouldBe(2);
        analyzer.Conversions[0].StacksConverted.ShouldBe(20);
        analyzer.Conversions[1].StacksConverted.ShouldBe(60);

        analyzer.TotalStacksConverted.ShouldBe(80);
        analyzer.AverageConversion.ShouldBe(40d);
        analyzer.DecayedStacks.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_BuffFallingOffWithNoCast_CountsEveryHeldStackAsDecayed()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            BuffRemoved(50_000),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.DecayedStacks.ShouldBe(25);
        analyzer.Conversions.ShouldBeEmpty();
        analyzer.TotalStacksConverted.ShouldBe(0);
        analyzer.AverageConversion.ShouldBe(0d);
    }

    [Fact]
    public async Task Analyze_BuffRemovedRightAfterACast_IsTheConversionNotDecay()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_500),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.DecayedStacks.ShouldBe(0);
        analyzer.Conversions.ShouldHaveSingleItem().StacksConverted.ShouldBe(25);
    }

    [Fact]
    public async Task Analyze_BuffRemovedLongAfterACast_IsDecay()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            StackApplied(11_000, 25),
            BuffRemoved(40_000),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.ShouldHaveSingleItem().StacksConverted.ShouldBe(25);
        analyzer.DecayedStacks.ShouldBe(25);
    }

    [Fact]
    public async Task Analyze_PartialStackLossWithNoCast_CountsOnlyTheStacksLost()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            StackRemoved(30_000, 10),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.DecayedStacks.ShouldBe(15);
        analyzer.Conversions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Analyze_BankPinnedAtTheCap_MeasuresTheCappedSpanFromTheFirstArrival()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(20_000, OwedInBloodEconomyAnalyzer.MaxStacks),
            StackApplied(25_000, OwedInBloodEconomyAnalyzer.MaxStacks),
            Cast(Spells.OwedInBlood.FSLID, 40_000),
            BuffRemoved(40_200),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.CappedMs.ShouldBe(20_200);
        analyzer.TotalStacksConverted.ShouldBe(OwedInBloodEconomyAnalyzer.MaxStacks);
        analyzer.DecayedStacks.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_StillCappedWhenThePullEnds_ClosesTheSpanAtThePullBoundary()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(150_000, OwedInBloodEconomyAnalyzer.MaxStacks),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.CappedMs.ShouldBe(PullEnd - 150_000);
        analyzer.DecayedStacks.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_PullEndingWhileHoldingStacks_CountsThemAsNeitherConvertedNorDecayed()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 30),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.ShouldBeEmpty();
        analyzer.TotalStacksConverted.ShouldBe(0);
        analyzer.DecayedStacks.ShouldBe(0);
        analyzer.CappedMs.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_CastWithNoBuffEventsAtAll_RecordsAnUnobservedConversion()
    {
        var analyzer = await AnalyzeAsync([Cast(Spells.OwedInBlood.FSLID, 10_000)]);

        var conversion = analyzer.Conversions.ShouldHaveSingleItem();
        conversion.StacksConverted.ShouldBe(0);
        conversion.BankObserved.ShouldBeFalse();

        analyzer.TotalStacksConverted.ShouldBe(0);
        analyzer.DecayedStacks.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_CastAfterAnInPullStackEvent_RecordsAnObservedConversion()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 18),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_100),
        };

        var analyzer = await AnalyzeAsync(events);

        var conversion = analyzer.Conversions.ShouldHaveSingleItem();
        conversion.StacksConverted.ShouldBe(18);
        conversion.BankObserved.ShouldBeTrue();
    }

    [Fact]
    public async Task Analyze_EmptyCastAfterTheBankWasSeen_StaysAProvenEmptyCast()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 12),
            Cast(Spells.OwedInBlood.FSLID, 5_000),
            BuffRemoved(5_100),
            Cast(Spells.OwedInBlood.FSLID, 20_000),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.Count.ShouldBe(2);
        analyzer.Conversions[1].StacksConverted.ShouldBe(0);
        analyzer.Conversions[1].BankObserved.ShouldBeTrue();
    }

    [Fact]
    public async Task Analyze_PullOpeningWithAPrepullBank_FlagsTheConversionUnobserved()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 30),
            Cast(Spells.OwedInBlood.FSLID, 35_000),
            BuffRemoved(35_100),
        };

        var (parser, _) = await RunAsync(events, Dungeon());

        parser.OwedInBloodEconomyAnalyzers.Count.ShouldBe(2);
        var second = parser.OwedInBloodEconomyAnalyzers[1].Analyzer;

        var conversion = second.Conversions.ShouldHaveSingleItem();
        conversion.StacksConverted.ShouldBe(0);
        conversion.BankObserved.ShouldBeFalse();
    }

    [Fact]
    public async Task Analyze_PullThatBuildsItsOwnBank_FlagsTheConversionObserved()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 30),
            StackApplied(31_000, 45),
            Cast(Spells.OwedInBlood.FSLID, 35_000),
            BuffRemoved(35_100),
        };

        var (parser, _) = await RunAsync(events, Dungeon());

        var second = parser.OwedInBloodEconomyAnalyzers[1].Analyzer;

        var conversion = second.Conversions.ShouldHaveSingleItem();
        conversion.StacksConverted.ShouldBe(45);
        conversion.BankObserved.ShouldBeTrue();
    }

    [Fact]
    public async Task Analyze_SecondCastAfterAReclassifiedDrop_DoesNotClaimTheSameStacksAgain()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            BuffRemoved(10_000),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            Cast(Spells.OwedInBlood.FSLID, 10_500),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.Count.ShouldBe(2);
        analyzer.Conversions[0].StacksConverted.ShouldBe(25);
        analyzer.Conversions[1].StacksConverted.ShouldBe(0);
        analyzer.TotalStacksConverted.ShouldBe(25);
        analyzer.DecayedStacks.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_TrackerSecondCastAfterAReclassifiedDrop_DoesNotDoubleCount()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            BuffRemoved(10_000),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            Cast(Spells.OwedInBlood.FSLID, 10_500),
        };

        var tracker = await TrackAsync(events);

        tracker.Generated.ShouldBe(25);
        tracker.Spent.ShouldBe(25);
        tracker.Decayed.ShouldBe(0);
        tracker.Current.ShouldBe(0);
        tracker.Generated.ShouldBe(tracker.Spent + tracker.Decayed + tracker.Current);
    }

    [Fact]
    public async Task Analyze_RemovalOrderedBeforeItsCastAtTheSameMillisecond_IsTheConversionNotDecay()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            BuffRemoved(10_000),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
        };

        var analyzer = await AnalyzeAsync(events);

        var conversion = analyzer.Conversions.ShouldHaveSingleItem();
        conversion.StacksConverted.ShouldBe(25);
        conversion.BankObserved.ShouldBeTrue();
        analyzer.DecayedStacks.ShouldBe(0);
        analyzer.TotalStacksConverted.ShouldBe(25);
    }

    [Fact]
    public async Task Analyze_RemovalExactlyAtTheGraceBoundaryBeforeTheCast_IsTheConversion()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            BuffRemoved(10_000),
            Cast(Spells.OwedInBlood.FSLID, 10_000 + OwedInBloodEconomyAnalyzer.ConversionGraceMs),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.ShouldHaveSingleItem().StacksConverted.ShouldBe(25);
        analyzer.DecayedStacks.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_RemovalOneMillisecondPastTheGraceBoundary_StaysDecay()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            BuffRemoved(10_000),
            Cast(Spells.OwedInBlood.FSLID, 10_001 + OwedInBloodEconomyAnalyzer.ConversionGraceMs),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.ShouldHaveSingleItem().StacksConverted.ShouldBe(0);
        analyzer.DecayedStacks.ShouldBe(25);
    }

    [Fact]
    public async Task Analyze_PartialDropBeforeACast_IsNotReclassified()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            StackRemoved(10_000, 10),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_100),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.ShouldHaveSingleItem().StacksConverted.ShouldBe(10);
        analyzer.DecayedStacks.ShouldBe(15);
    }

    [Fact]
    public async Task Analyze_TrashPull_IsAlsoEvaluated()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 12),
            Cast(Spells.OwedInBlood.FSLID, 5_000),
        };

        var (parser, _) = await RunAsync(events, TrashDungeon(PullEnd));

        parser.OwedInBloodEconomyAnalyzers.ShouldHaveSingleItem()
            .Analyzer.Conversions.ShouldHaveSingleItem().StacksConverted.ShouldBe(12);
    }

    [Fact]
    public async Task Analyze_RetainsTheAnalyzerOnEveryPullReadPath()
    {
        var (parser, _) = await RunAsync([BuffApplied(1_000)], BossDungeon(PullEnd));

        var entry = parser.OwedInBloodEconomyAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;

        pull.OwedInBloodEconomyAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).OwedInBloodEconomyAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    [Fact]
    public async Task Analyze_TrackerBankBuiltThenCashedIn_ReconstructsTheDungeonLifetimeTotals()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 10),
            StackApplied(3_000, 40),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_100),
        };

        var tracker = await TrackAsync(events);

        tracker.Generated.ShouldBe(40);
        tracker.Spent.ShouldBe(40);
        tracker.Decayed.ShouldBe(0);
        tracker.Current.ShouldBe(0);
        tracker.CappedMs.ShouldBe(0);
        tracker.Generated.ShouldBe(tracker.Spent + tracker.Decayed + tracker.Current);
    }

    [Fact]
    public async Task Analyze_TrackerBuffExpiringWithNoCast_BooksTheBankAsDecayed()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            BuffRemoved(50_000),
        };

        var tracker = await TrackAsync(events);

        tracker.Generated.ShouldBe(25);
        tracker.Spent.ShouldBe(0);
        tracker.Decayed.ShouldBe(25);
        tracker.Current.ShouldBe(0);
        tracker.Generated.ShouldBe(tracker.Spent + tracker.Decayed + tracker.Current);
    }

    [Fact]
    public async Task Analyze_TrackerHoldingABankAtTheEnd_LeavesItAsHeld()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 30),
        };

        var tracker = await TrackAsync(events);

        tracker.Generated.ShouldBe(30);
        tracker.Spent.ShouldBe(0);
        tracker.Decayed.ShouldBe(0);
        tracker.Current.ShouldBe(30);
        tracker.Generated.ShouldBe(tracker.Spent + tracker.Decayed + tracker.Current);
    }

    [Fact]
    public async Task Analyze_TrackerBankPinnedAtTheCap_LatchesTheSpanAndClosesItAtTheDungeonEnd()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(120_000, BloodFeatherTracker.MaxBloodFeathers),
            StackApplied(130_000, BloodFeatherTracker.MaxBloodFeathers),
        };

        var tracker = await TrackAsync(events);

        tracker.CappedMs.ShouldBe(PullEnd - 120_000);
        tracker.Current.ShouldBe(BloodFeatherTracker.MaxBloodFeathers);
    }

    [Fact]
    public async Task Analyze_TrackerRemovalOrderedBeforeItsCast_CreditsTheConversion()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            BuffRemoved(10_000),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
        };

        var tracker = await TrackAsync(events);

        tracker.Generated.ShouldBe(25);
        tracker.Spent.ShouldBe(25);
        tracker.Decayed.ShouldBe(0);
        tracker.Current.ShouldBe(0);
        tracker.Generated.ShouldBe(tracker.Spent + tracker.Decayed + tracker.Current);
    }

    [Fact]
    public async Task Analyze_TrackerRemovalExactlyAtTheGraceBoundaryBeforeTheCast_CreditsTheConversion()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            BuffRemoved(10_000),
            Cast(Spells.OwedInBlood.FSLID, 10_000 + BloodFeatherTracker.ConversionGraceMs),
        };

        var tracker = await TrackAsync(events);

        tracker.Spent.ShouldBe(25);
        tracker.Decayed.ShouldBe(0);
        tracker.Generated.ShouldBe(tracker.Spent + tracker.Decayed + tracker.Current);
    }

    [Fact]
    public async Task Analyze_TrackerRemovalOneMillisecondPastTheGraceBoundary_StaysDecay()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(5_000, 25),
            BuffRemoved(10_000),
            Cast(Spells.OwedInBlood.FSLID, 10_001 + BloodFeatherTracker.ConversionGraceMs),
        };

        var tracker = await TrackAsync(events);

        tracker.Spent.ShouldBe(0);
        tracker.Decayed.ShouldBe(25);
        tracker.Generated.ShouldBe(tracker.Spent + tracker.Decayed + tracker.Current);
    }

    [Fact]
    public async Task Analyze_TrackerSpansEveryPullOfTheDungeon()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 30),
            Cast(Spells.OwedInBlood.FSLID, 5_000),
            BuffRemoved(5_100),
            BuffApplied(31_000),
            StackApplied(32_000, 20),
            Cast(Spells.OwedInBlood.FSLID, 35_000),
            BuffRemoved(35_100),
        };

        var (parser, _) = await RunAsync(events, Dungeon());

        var tracker = parser.BloodFeatherTracker.ShouldNotBeNull();
        tracker.Generated.ShouldBe(50);
        tracker.Spent.ShouldBe(50);
        tracker.Decayed.ShouldBe(0);
        tracker.Current.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_WithFeatherActivity_ContributesTheStatisticsCard()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 8),
            Cast(Spells.OwedInBlood.FSLID, 5_000),
            BuffRemoved(5_100),
        };

        var (parser, result) = await RunAsync(events, BossDungeon(PullEnd));

        var tracker = parser.BloodFeatherTracker.ShouldNotBeNull();
        tracker.Statistic.ShouldNotBeNull();
        result.Statistics.ShouldContain(entry => entry.Module is BloodFeatherTracker);
    }

    [Fact]
    public async Task Analyze_WithNoFeatherActivity_ContributesNoStatisticsCard()
    {
        var (parser, result) = await RunAsync([Cast(Spells.HeartSplitter.FSLID, 1_000)], BossDungeon(PullEnd));

        var tracker = parser.BloodFeatherTracker.ShouldNotBeNull();
        tracker.Generated.ShouldBe(0);
        tracker.Spent.ShouldBe(0);
        tracker.Statistic.ShouldBeNull();
        result.Statistics.ShouldNotContain(entry => entry.Module is BloodFeatherTracker);
    }

    [Fact]
    public async Task Analyze_ConversionFollowedByASlaughterInsideOpenWounds_IsTheFullChain()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 120),
            OpenWoundsApplied(9_000),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_100),
            Cast(Spells.Slaughter.FSLID, 11_000),
        };

        var analyzer = await AnalyzeAsync(events);

        var conversion = analyzer.Conversions.ShouldHaveSingleItem();
        conversion.FollowedBySlaughter.ShouldBeTrue();
        conversion.PairedWithRupture.ShouldBeTrue();
        conversion.SpiritActive.ShouldBeFalse();
        conversion.ShareOfCap.ShouldBe(120d / OwedInBloodEconomyAnalyzer.MaxStacks);

        analyzer.FollowedBySlaughter.ShouldBe(1);
        analyzer.PairedWithRupture.ShouldBe(1);
        analyzer.OverlappedSpirit.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ConversionCashedOutsideOpenWounds_IsCashedButNotPaired()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 60),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_100),
            Cast(Spells.Slaughter.FSLID, 11_000),
        };

        var analyzer = await AnalyzeAsync(events);

        var conversion = analyzer.Conversions.ShouldHaveSingleItem();
        conversion.FollowedBySlaughter.ShouldBeTrue();
        conversion.PairedWithRupture.ShouldBeFalse();

        analyzer.FollowedBySlaughter.ShouldBe(1);
        analyzer.PairedWithRupture.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_ConversionWithNoSlaughterFollowing_IsNotCashed()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 60),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_100),
        };

        var analyzer = await AnalyzeAsync(events);

        var conversion = analyzer.Conversions.ShouldHaveSingleItem();
        conversion.FollowedBySlaughter.ShouldBeFalse();
        conversion.PairedWithRupture.ShouldBeFalse();
    }

    [Fact]
    public async Task Analyze_SlaughterBeyondTheCashWindow_DoesNotCountAsCashingTheConversion()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 60),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_100),
            Cast(Spells.Slaughter.FSLID, 10_001 + OwedInBloodEconomyAnalyzer.CashWindowMs),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.ShouldHaveSingleItem().FollowedBySlaughter.ShouldBeFalse();
    }

    [Fact]
    public async Task Analyze_SlaughterPrecedingTheConversion_DoesNotCashIt()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 60),
            Cast(Spells.Slaughter.FSLID, 9_000),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_100),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.ShouldHaveSingleItem().FollowedBySlaughter.ShouldBeFalse();
    }

    [Fact]
    public async Task Analyze_ExpiredOpenWoundsWindow_DoesNotPairTheSlaughterWithRupture()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 60),
            OpenWoundsApplied(5_000),
            Cast(Spells.OwedInBlood.FSLID, 5_001 + OwedInBloodEconomyAnalyzer.OpenWoundsDurationMs),
            BuffRemoved(5_100 + OwedInBloodEconomyAnalyzer.OpenWoundsDurationMs),
            Cast(Spells.Slaughter.FSLID, 6_000 + OwedInBloodEconomyAnalyzer.OpenWoundsDurationMs),
        };

        var analyzer = await AnalyzeAsync(events);

        var conversion = analyzer.Conversions.ShouldHaveSingleItem();
        conversion.FollowedBySlaughter.ShouldBeTrue();
        conversion.PairedWithRupture.ShouldBeFalse();
    }

    [Fact]
    public async Task Analyze_OpenWoundsRemovedBeforeTheSlaughter_DoesNotPairIt()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 60),
            OpenWoundsApplied(9_000),
            OpenWoundsRemoved(9_500),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_100),
            Cast(Spells.Slaughter.FSLID, 11_000),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.ShouldHaveSingleItem().PairedWithRupture.ShouldBeFalse();
    }

    [Fact]
    public async Task Analyze_ConversionInsideTheSpiritBuff_RecordsTheOverlap()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, OwedInBloodEconomyAnalyzer.MaxStacks),
            SpiritApplied(8_000),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_100),
            SpiritRemoved(28_000),
        };

        var analyzer = await AnalyzeAsync(events);

        var conversion = analyzer.Conversions.ShouldHaveSingleItem();
        conversion.SpiritActive.ShouldBeTrue();
        conversion.ShareOfCap.ShouldBe(1d);

        analyzer.OverlappedSpirit.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_ConversionAfterTheSpiritBuffEnded_RecordsNoOverlap()
    {
        var events = new List<Event>
        {
            BuffApplied(1_000),
            StackApplied(2_000, 40),
            SpiritApplied(3_000),
            SpiritRemoved(9_000),
            Cast(Spells.OwedInBlood.FSLID, 10_000),
            BuffRemoved(10_100),
        };

        var analyzer = await AnalyzeAsync(events);

        analyzer.Conversions.ShouldHaveSingleItem().SpiritActive.ShouldBeFalse();
        analyzer.OverlappedSpirit.ShouldBe(0);
    }

    private static ApplyDebuffEvent OpenWoundsApplied(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = BossId,
        Ability = new Ability { Id = Spells.OpenWounds.FSLID },
    };

    private static RemoveDebuffEvent OpenWoundsRemoved(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = BossId,
        Ability = new Ability { Id = Spells.OpenWounds.FSLID },
    };

    private static ApplyBuffEvent SpiritApplied(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.BloodboundSpiritSelfBuff.FSLID },
    };

    private static RemoveBuffEvent SpiritRemoved(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.BloodboundSpiritSelfBuff.FSLID },
    };

    private static ApplyBuffEvent BuffApplied(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.OwedInBloodSelfBuff.FSLID },
    };

    private static ApplyBuffStackEvent StackApplied(int timestamp, int stacks) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stacks,
        Ability = new Ability { Id = Spells.OwedInBloodSelfBuff.FSLID },
    };

    private static RemoveBuffStackEvent StackRemoved(int timestamp, int stacks) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stacks,
        Ability = new Ability { Id = Spells.OwedInBloodSelfBuff.FSLID },
    };

    private static RemoveBuffEvent BuffRemoved(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = Spells.OwedInBloodSelfBuff.FSLID },
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

    private static ReportDungeon Dungeon() =>
        new(0, "Dungeon", 0, true, 0, PullEnd, null, null, null, false,
            [
                new DungeonPull(1, 0, null, 0, 20_000, "Trash", null),
                new DungeonPull(2, 42, true, 30_000, PullEnd, "Boss", null),
            ]);

    private static async Task<OwedInBloodEconomyAnalyzer> AnalyzeAsync(List<Event> events, ReportDungeon? dungeon = null)
    {
        var (parser, _) = await RunAsync(events, dungeon ?? BossDungeon(PullEnd));
        return parser.OwedInBloodEconomyAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<BloodFeatherTracker> TrackAsync(List<Event> events)
    {
        var (parser, _) = await RunAsync(events, BossDungeon(PullEnd));
        return parser.BloodFeatherTracker.ShouldNotBeNull();
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
