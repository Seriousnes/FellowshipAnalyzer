using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Tariq.Modules;

using Shouldly;

using Xunit;

using TariqSpells = FellowshipAnalyzer.Core.Common.Spells.Tariq.Spells;

namespace FellowshipAnalyzer.Heroes.Tariq.Tests.Analysis;

public sealed class ExecutionersGrinTrackerTests
{
    [Fact]
    public async Task ProcWithAboveExecuteCullingStrike_CountsAsUsed()
    {
        var (parser, _) = await FuryEconomyAnalyzerTests.AnalyzeAsync(
        [
            FuryEconomyAnalyzerTests.Buff<ApplyBuffEvent>(100, TariqSpells.ExecutionersGrin.FSLID),
            FuryEconomyAnalyzerTests.Cast(150, TariqSpells.CullingStrike.FSLID, fury: 30, maxFury: 100),
            FuryEconomyAnalyzerTests.CullingStrikeHit(200, targetHp: 60, targetMaxHp: 100),
            FuryEconomyAnalyzerTests.Buff<RemoveBuffEvent>(300, TariqSpells.ExecutionersGrin.FSLID),
        ]);

        var tracker = parser.GetModule<ExecutionersGrinTracker>().ShouldNotBeNull();
        tracker.Procs.ShouldBe(1);
        tracker.UsedProcs.ShouldBe(1);
        tracker.WastedProcs.ShouldBe(0);
        tracker.AboveExecuteCullingStrikes.ShouldBe(1);
    }

    [Fact]
    public async Task ProcExpiringUnused_CountsAsWastedOpportunity()
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
        tracker.UsedProcs.ShouldBe(0);
        tracker.WastedProcs.ShouldBe(2);
        tracker.CullingStrikeHits.ShouldBe(1);
        tracker.AboveExecuteCullingStrikes.ShouldBe(0);
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
        tracker.WastedProcs.ShouldBe(0);
        tracker.CullingStrikeHits.ShouldBe(2);
        tracker.AboveExecuteCullingStrikes.ShouldBe(1);
    }
}
