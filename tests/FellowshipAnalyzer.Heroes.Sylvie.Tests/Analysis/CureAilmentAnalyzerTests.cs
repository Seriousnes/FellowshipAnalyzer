using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Sylvie.Spells;

using static FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis.SylvieAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Sylvie.Tests.Analysis;

public sealed class CureAilmentAnalyzerTests
{
    [Fact]
    public async Task TheFightLifetimeTrackerKeepsEveryDispel()
    {
        var parser = await Analyze(
            Dispel(PullStart + 1_000, Spells.CureAilment, TankId, Spells.EnfeeblingRootsapDebuff),
            Dispel(PullStart + 2_000, Spells.CureAilment, AllyId, Spells.EnfeeblingRootsapDebuff));

        var tracker = parser.DispelTracker.ShouldNotBeNull();

        tracker.TotalDispels.ShouldBe(2);
        tracker.DispelsWith(Spells.CureAilment.FSLID).ShouldBe(2);
        tracker.ByTarget.Count.ShouldBe(2);
    }
}
