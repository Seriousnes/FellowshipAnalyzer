using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Tariq.Modules;

using Shouldly;

using Xunit;

using TariqSpells = FellowshipAnalyzer.Core.Common.Spells.Tariq.Spells;

namespace FellowshipAnalyzer.Heroes.Tariq.Tests.Analysis;

public sealed class ExecutionersGrinTrackerTests
{
    [Fact]
    public async Task ProcSpentAboveTheThreshold_IsAnExtraCast()
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
    /// A proc spent under the threshold is not the same as a proc expiring with no cast at all, and
    /// counting the two together overstates the avoidable loss - report <c>a:NcqHDKzamL7n6YFv</c> has
    /// 28 of the first and 8 of the second.
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
    public async Task ProcReappliedOnOneAlreadyActive_CountsAsAReapplicationRatherThanANewProc()
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
    /// Executioner's Grin holds up to two stacks, and a second proc arrives as a stack application.
    /// Each Culling Strike spends one stack.
    /// </summary>
    [Fact]
    public async Task TwoStacks_AreTwoProcsSpentByTwoCullingStrikes()
    {
        var (parser, _) = await FuryEconomyAnalyzerTests.AnalyzeAsync(
        [
            FuryEconomyAnalyzerTests.Buff<ApplyBuffEvent>(100, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.Buff<ApplyBuffStackEvent>(150, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.CullingStrikeHit(200, targetHp: 60, targetMaxHp: 100),
            FuryEconomyAnalyzerTests.Buff<RemoveBuffStackEvent>(200, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.Buff<RemoveBuffEvent>(400, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.CullingStrikeHit(400, targetHp: 10, targetMaxHp: 100),
        ]);

        var tracker = parser.GetModule<ExecutionersGrinTracker>().ShouldNotBeNull();
        tracker.Procs.ShouldBe(2);
        tracker.SpentAboveThreshold.ShouldBe(1);
        tracker.SpentBelowThreshold.ShouldBe(1);
        tracker.ExpiredUnspent.ShouldBe(0);
    }

    /// <summary>
    /// Every stack still held when the buff is removed expired unspent.
    /// </summary>
    [Fact]
    public async Task TwoStacks_OneSpent_TheOtherExpiresUnspent()
    {
        var (parser, _) = await FuryEconomyAnalyzerTests.AnalyzeAsync(
        [
            FuryEconomyAnalyzerTests.Buff<ApplyBuffEvent>(100, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.Buff<ApplyBuffStackEvent>(150, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.Buff<RemoveBuffStackEvent>(200, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.CullingStrikeHit(200, targetHp: 60, targetMaxHp: 100),
            FuryEconomyAnalyzerTests.Buff<RemoveBuffEvent>(900, TariqSpells.ExecutionersGrin.FSLID),
        ]);

        var tracker = parser.GetModule<ExecutionersGrinTracker>().ShouldNotBeNull();
        tracker.Procs.ShouldBe(2);
        tracker.SpentAboveThreshold.ShouldBe(1);
        tracker.ExpiredUnspent.ShouldBe(1);
    }

    /// <summary>
    /// Two stacks held when the buff is removed are two procs expired unspent.
    /// </summary>
    [Fact]
    public async Task TwoStacks_NeitherSpent_BothExpireUnspent()
    {
        var (parser, _) = await FuryEconomyAnalyzerTests.AnalyzeAsync(
        [
            FuryEconomyAnalyzerTests.Buff<ApplyBuffEvent>(100, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.Buff<ApplyBuffStackEvent>(150, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.Buff<RemoveBuffEvent>(900, TariqSpells.ExecutionersGrin.FSLID),
        ]);

        var tracker = parser.GetModule<ExecutionersGrinTracker>().ShouldNotBeNull();
        tracker.Procs.ShouldBe(2);
        tracker.ExpiredUnspent.ShouldBe(2);
    }

    /// <summary>
    /// The statistics card and the guide's proc subsection both depend on the Executioner's Grin
    /// legendary being equipped, so a parse without it must not offer either.
    /// </summary>
    [Fact]
    public async Task WithoutTheLegendaryEquipped_NoStatisticsCardIsOffered()
    {
        var (parser, _) = await FuryEconomyAnalyzerTests.AnalyzeAsync(
        [
            FuryEconomyAnalyzerTests.Buff<ApplyBuffEvent>(100, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.CullingStrikeHit(200, targetHp: 60, targetMaxHp: 100),
        ]);

        var tracker = parser.GetModule<ExecutionersGrinTracker>().ShouldNotBeNull();
        tracker.Equipped.ShouldBeFalse();
        tracker.Statistic.ShouldBeNull();
    }

    /// <summary>
    /// Wearing the Executioner's Grin legendary offers the statistics card once a proc lands.
    /// </summary>
    [Fact]
    public async Task WithTheLegendaryEquipped_TheStatisticsCardIsOffered()
    {
        var (parser, _) = await FuryEconomyAnalyzerTests.AnalyzeAsync(
        [
            new CombatantInfoEvent
            {
                SourceId = FuryEconomyAnalyzerTests.PlayerId,
                Gear = [new Item { Id = Legendaries.ExecutionersGrin.Id, Quality = 6 }],
            },
            FuryEconomyAnalyzerTests.Buff<ApplyBuffEvent>(100, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.CullingStrikeHit(200, targetHp: 60, targetMaxHp: 100),
        ]);

        var tracker = parser.GetModule<ExecutionersGrinTracker>().ShouldNotBeNull();
        tracker.Equipped.ShouldBeTrue();
        tracker.Statistic.ShouldNotBeNull();
    }
}
