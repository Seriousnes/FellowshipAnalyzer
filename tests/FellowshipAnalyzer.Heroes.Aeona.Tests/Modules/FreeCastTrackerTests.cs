using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Modules;

/// <summary>
/// Tests for <see cref="FreeCastTracker"/>: which window paid for each free cast, and how many chances to
/// spend one a stretch of the report offered.
/// </summary>
public sealed class FreeCastTrackerTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 40;
    private const int DungeonEndTime = 30_000;

    private static readonly ReportDungeon Dungeon =
        new(Id: 0, Name: "Boss", EncounterId: 1, Kill: true,
            StartTime: 0, EndTime: DungeonEndTime, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    [Fact]
    public async Task WithoutAnyTalent_TheModuleStillTracks()
    {
        var tracker = await Track(FreeCast(Spells.Oblivion.FSLID, 2_000));

        tracker.FreeCasts.ShouldHaveSingleItem().Source.ShouldBe(FreeCastSource.Other);
        tracker.FreeCastsObserved.ShouldBeTrue();
    }

    [Fact]
    public async Task AFreeCastInsideAUchroniaWindow_IsAttributedToUchronia()
    {
        var tracker = await Track(
            Talented(AeonaTalents.Uchronia),
            UchroniaApply(1_000),
            FreeCast(Spells.Oblivion.FSLID, 2_000),
            UchroniaRemove(2_000));

        tracker.FreeCasts.ShouldHaveSingleItem().Source.ShouldBe(FreeCastSource.Uchronia);
    }

    [Fact]
    public async Task AFreeCastInsideAnEpochBreakWindow_IsAttributedToEpochBreak()
    {
        var tracker = await Track(
            EpochBreakApply(1_000),
            FreeCast(Spells.AmendFate.FSLID, 2_000),
            EpochBreakRemove(5_000));

        tracker.FreeCasts.ShouldHaveSingleItem().Source.ShouldBe(FreeCastSource.EpochBreak);
        tracker.EpochBreakActive(1_000).ShouldBeTrue();
        tracker.EpochBreakActive(5_000).ShouldBeTrue();
        tracker.EpochBreakActive(5_001).ShouldBeFalse();
    }

    [Fact]
    public async Task EpochBreakTakesPrecedenceOverAnOverlappingUchroniaWindow()
    {
        var tracker = await Track(
            Talented(AeonaTalents.Uchronia),
            EpochBreakApply(1_000),
            UchroniaApply(1_500),
            FreeCast(Spells.Oblivion.FSLID, 2_000),
            UchroniaRemove(2_000),
            EpochBreakRemove(5_000));

        tracker.FreeCasts.ShouldHaveSingleItem().Source.ShouldBe(FreeCastSource.EpochBreak);
    }

    [Fact]
    public async Task Opportunities_CountBothWindowsThatOpenedInTheRange()
    {
        var tracker = await Track(
            Talented(AeonaTalents.Uchronia),
            UchroniaApply(1_000),
            UchroniaRemove(2_000),
            EpochBreakApply(3_000),
            EpochBreakRemove(6_000),
            UchroniaApply(9_000),
            UchroniaRemove(9_500));

        tracker.OpportunitiesBetween(0, 7_000).ShouldBe(2);
        tracker.OpportunitiesBetween(0, 10_000).ShouldBe(3);
        tracker.OpportunitiesBetween(4_000, 10_000).ShouldBe(1);
    }

    [Fact]
    public async Task Opportunities_CountEpochBreakAloneWithoutTheUchroniaTalent()
    {
        var tracker = await Track(
            EpochBreakApply(3_000),
            EpochBreakRemove(6_000));

        tracker.OpportunitiesBetween(0, 10_000).ShouldBe(1);
    }

    [Fact]
    public async Task FreeCastAt_MatchesTheAbilityWithinTolerance()
    {
        var tracker = await Track(FreeCast(Spells.Oblivion.FSLID, 2_000));

        tracker.FreeCastAt(2_000, Spells.Oblivion.FSLID).ShouldNotBeNull();
        tracker.FreeCastAt(2_000 + FreeCastTracker.CastMatchToleranceMs, Spells.Oblivion.FSLID).ShouldNotBeNull();
        tracker.FreeCastAt(2_000 + FreeCastTracker.CastMatchToleranceMs + 1, Spells.Oblivion.FSLID).ShouldBeNull();
        tracker.FreeCastAt(2_000, Spells.AmendFate.FSLID).ShouldBeNull();
    }

    [Fact]
    public async Task FreeCastByAnotherPlayer_IsNotRecorded()
    {
        var otherPlayerFreeCast = FreeCast(Spells.Oblivion.FSLID, 2_000);
        otherPlayerFreeCast.SourceId = PlayerId + 1;

        var tracker = await Track(otherPlayerFreeCast);

        tracker.FreeCasts.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnEpochBreakWindowStillOpenAtTheEnd_ClosesAtTheDungeonEnd()
    {
        var tracker = await Track(EpochBreakApply(1_000));

        var window = tracker.EpochBreakWindows.ShouldHaveSingleItem();
        window.Start.ShouldBe(1_000);
        window.End.ShouldBe(DungeonEndTime);
    }

    private static ApplyBuffEvent UchroniaApply(int timestamp) => Apply(timestamp, Spells.Uchronia.FSLID);

    private static RemoveBuffEvent UchroniaRemove(int timestamp) => Remove(timestamp, Spells.Uchronia.FSLID);

    private static ApplyBuffEvent EpochBreakApply(int timestamp) => Apply(timestamp, Spells.EpochBreakSelfBuff.FSLID);

    private static RemoveBuffEvent EpochBreakRemove(int timestamp) => Remove(timestamp, Spells.EpochBreakSelfBuff.FSLID);

    private static ApplyBuffEvent Apply(int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = abilityId },
    };

    private static RemoveBuffEvent Remove(int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = abilityId },
    };

    private static FreeCastEvent FreeCast(int abilityId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        AbilityGameId = abilityId,
        Ability = new Ability { Id = abilityId },
    };

    private static CombatantInfoEvent Talented(int talentId) => new()
    {
        SourceId = PlayerId,
        Talents = [new TalentInfo { Id = talentId }],
    };

    private static async Task<FreeCastTracker> Track(params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, Dungeon);

        return parser.FreeCastTracker.ShouldNotBeNull();
    }
}
