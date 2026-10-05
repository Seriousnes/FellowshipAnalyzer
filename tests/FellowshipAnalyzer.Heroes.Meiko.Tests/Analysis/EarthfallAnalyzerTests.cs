using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Meiko.Spells;

using static FellowshipAnalyzer.Heroes.Meiko.Tests.Analysis.MeikoAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Meiko.Tests.Analysis;

public sealed class EarthfallAnalyzerTests
{
    [Fact]
    public async Task SpendingBothStacksBeforeTheNextRisingEarth_WastesNothing()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.RisingEarth),
            ApplyBuff(PullStart + 1_000, Spells.Earthfall),
            ApplyBuffStack(PullStart + 1_000, Spells.Earthfall, 2),
            Cast(PullStart + 3_000, Spells.EarthfistBarrage),
            RemoveBuffStack(PullStart + 3_000, Spells.Earthfall, 1),
            Cast(PullStart + 6_000, Spells.EarthfistBarrage),
            RemoveBuff(PullStart + 6_000, Spells.Earthfall),
            Cast(PullStart + 9_000, Spells.RisingEarth));

        var analyzer = parser.EarthfallAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.EarthfallGranted.ShouldBe(2);
        analyzer.EarthfallOverwritten.ShouldBe(0);
        analyzer.RisingEarthOverwrites.ShouldBe(0);
        analyzer.EmpoweredBarrages.ShouldBe(2);
        analyzer.UnempoweredBarrages.ShouldBe(0);
    }

    [Fact]
    public async Task RisingEarthWithStacksHeld_WastesThem_AndABarrageWithoutEarthfallIsUnempowered()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.EarthfistBarrage),
            Cast(PullStart + 2_000, Spells.RisingEarth),
            ApplyBuff(PullStart + 2_000, Spells.Earthfall),
            ApplyBuffStack(PullStart + 2_000, Spells.Earthfall, 2),
            Cast(PullStart + 4_000, Spells.EarthfistBarrage),
            RemoveBuffStack(PullStart + 4_000, Spells.Earthfall, 1),
            Cast(PullStart + 6_000, Spells.RisingEarth),
            RefreshBuff(PullStart + 6_000, Spells.Earthfall),
            ApplyBuffStack(PullStart + 6_000, Spells.Earthfall, 2));

        var analyzer = parser.EarthfallAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.RisingEarthOverwrites.ShouldBe(1);
        analyzer.EarthfallOverwritten.ShouldBe(1);
        analyzer.EarthfallGranted.ShouldBe(4);
        analyzer.OverwriteShare.ShouldNotBeNull().ShouldBe(0.5, 0.001);
        analyzer.EmpoweredBarrages.ShouldBe(1);
        analyzer.UnempoweredBarrages.ShouldBe(1);
        analyzer.EmpoweredShare.ShouldNotBeNull().ShouldBe(0.5, 0.001);
    }

    [Fact]
    public async Task EarthfallGainedWithoutRisingEarth_CountsAsGranted()
    {
        var parser = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.Earthfall),
            ApplyBuffStack(PullStart + 1_000, Spells.Earthfall, 2),
            Cast(PullStart + 3_000, Spells.EarthfistBarrage),
            RemoveBuffStack(PullStart + 3_000, Spells.Earthfall, 1));

        var analyzer = parser.EarthfallAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.EarthfallGranted.ShouldBe(2);
        analyzer.EarthfallOverwritten.ShouldBe(0);
        analyzer.EmpoweredBarrages.ShouldBe(1);
    }

    [Fact]
    public async Task APullWithNoCasts_ReadsNullShares()
    {
        var parser = await Analyze(Cast(PullStart + 1_000, Spells.EarthFist));

        var analyzer = parser.EarthfallAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.OverwriteShare.ShouldBeNull();
        analyzer.EmpoweredShare.ShouldBeNull();
    }
}
