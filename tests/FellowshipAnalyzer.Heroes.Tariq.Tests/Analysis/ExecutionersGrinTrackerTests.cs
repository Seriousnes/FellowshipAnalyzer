using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Tariq.Modules;

using Shouldly;

using Xunit;

using TariqSpells = FellowshipAnalyzer.Core.Common.Spells.Tariq.Spells;

namespace FellowshipAnalyzer.Heroes.Tariq.Tests.Analysis;

public sealed class ExecutionersGrinTrackerTests
{
    [Fact]
    public async Task ProcSpentAboveTheThreshold_IsTheExtraCastTheItemBought()
    {
        var (parser, _) = await FuryEconomyAnalyzerTests.AnalyzeAsync(
        [
            FuryEconomyAnalyzerTests.Buff<ApplyBuffEvent>(100, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.Cast(150, TariqSpells.CullingStrike.FSLID, rawFury: 3_000, rawMaxFury: 10_000),
            FuryEconomyAnalyzerTests.CullingStrikeHit(200, targetHp: 60, targetMaxHp: 100),
            FuryEconomyAnalyzerTests.Buff<RemoveBuffEvent>(300, TariqSpells.ExecutionersGrin.FSLID),
        ]);

        var tracker = parser.GetModule<ExecutionersGrinTracker>().ShouldNotBeNull();
        tracker.Procs.ShouldBe(1);
        tracker.SpentAboveThreshold.ShouldBe(1);
        tracker.SpentBelowThreshold.ShouldBe(0);
        tracker.ExpiredUnspent.ShouldBe(0);
        tracker.AboveExecuteCullingStrikes.ShouldBe(1);
    }

    /// <summary>
    /// The game spends the proc on any Culling Strike, including one already legal under the threshold.
    /// That is not the same as a proc expiring with no cast at all, and counting the two together
    /// overstates the avoidable loss - report <c>a:NcqHDKzamL7n6YFv</c> has 28 of the first and 8 of
    /// the second.
    /// </summary>
    [Fact]
    public async Task ProcSpentBelowTheThreshold_IsSeparatedFromOneThatExpiredUnspent()
    {
        var (parser, _) = await FuryEconomyAnalyzerTests.AnalyzeAsync(
        [
            FuryEconomyAnalyzerTests.Buff<ApplyBuffEvent>(100, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.CullingStrikeHit(200, targetHp: 10, targetMaxHp: 100),
            FuryEconomyAnalyzerTests.Buff<RemoveBuffEvent>(300, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.Buff<ApplyBuffEvent>(400, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.Buff<RemoveBuffEvent>(600, TariqSpells.ExecutionersGrin.FSLID),
        ]);

        var tracker = parser.GetModule<ExecutionersGrinTracker>().ShouldNotBeNull();
        tracker.Procs.ShouldBe(2);
        tracker.SpentAboveThreshold.ShouldBe(0);
        tracker.SpentBelowThreshold.ShouldBe(1);
        tracker.ExpiredUnspent.ShouldBe(1);
        tracker.CullingStrikeHits.ShouldBe(1);
        tracker.AboveExecuteCullingStrikes.ShouldBe(0);
    }

    /// <summary>
    /// The removal and the Culling Strike that caused it share a timestamp in practice, so a removal seen
    /// first must not be booked as an expiry the cast then cannot reclaim.
    /// </summary>
    [Fact]
    public async Task RemovalSharingItsTimestampWithTheCast_IsReclaimedAsASpentProc()
    {
        var (parser, _) = await FuryEconomyAnalyzerTests.AnalyzeAsync(
        [
            FuryEconomyAnalyzerTests.Buff<ApplyBuffEvent>(100, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.Buff<RemoveBuffEvent>(200, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.CullingStrikeHit(200, targetHp: 60, targetMaxHp: 100),
        ]);

        var tracker = parser.GetModule<ExecutionersGrinTracker>().ShouldNotBeNull();
        tracker.Procs.ShouldBe(1);
        tracker.SpentAboveThreshold.ShouldBe(1);
        tracker.ExpiredUnspent.ShouldBe(0);
    }

    [Fact]
    public async Task ProcLandingOnOneAlreadyHeld_CountsAsAReapplicationThatAddsNothing()
    {
        var (parser, _) = await FuryEconomyAnalyzerTests.AnalyzeAsync(
        [
            FuryEconomyAnalyzerTests.Buff<ApplyBuffEvent>(100, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.Buff<RefreshBuffEvent>(200, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.CullingStrikeHit(300, targetHp: 60, targetMaxHp: 100),
        ]);

        var tracker = parser.GetModule<ExecutionersGrinTracker>().ShouldNotBeNull();
        tracker.Procs.ShouldBe(1);
        tracker.Reapplications.ShouldBe(1);
        tracker.SpentAboveThreshold.ShouldBe(1);
    }

    [Fact]
    public async Task AboveExecuteHitWithoutBuffEvents_StillEvidencesItemUsage()
    {
        var (parser, _) = await FuryEconomyAnalyzerTests.AnalyzeAsync(
        [
            FuryEconomyAnalyzerTests.CullingStrikeHit(200, targetHp: 80, targetMaxHp: 100),
            FuryEconomyAnalyzerTests.CullingStrikeHit(400, targetHp: 10, targetMaxHp: 100),
        ]);

        var tracker = parser.GetModule<ExecutionersGrinTracker>().ShouldNotBeNull();
        tracker.Procs.ShouldBe(0);
        tracker.ExpiredUnspent.ShouldBe(0);
        tracker.CullingStrikeHits.ShouldBe(2);
        tracker.AboveExecuteCullingStrikes.ShouldBe(1);
    }

    /// <summary>
    /// The statistics card and the guide's proc subsection both hang off the bands being worn, so a
    /// parse without them must not offer either.
    /// </summary>
    [Fact]
    public async Task WithoutTheBandsEquipped_NoStatisticsCardIsOffered()
    {
        var (parser, _) = await FuryEconomyAnalyzerTests.AnalyzeAsync(
        [
            FuryEconomyAnalyzerTests.Buff<ApplyBuffEvent>(100, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.CullingStrikeHit(200, targetHp: 60, targetMaxHp: 100),
        ]);

        var tracker = parser.GetModule<ExecutionersGrinTracker>().ShouldNotBeNull();
        tracker.Equipped.ShouldBeFalse();
        tracker.StatisticsComponentType.ShouldBeNull();
    }
}
