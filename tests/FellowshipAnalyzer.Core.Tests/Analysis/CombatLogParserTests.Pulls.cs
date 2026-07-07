using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Events;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed partial class CombatLogParserTests
{
    [Fact]
    public async Task Analyze_WithPullNormalizer_NestsImplicitPullInsideFightBookends()
    {
        var owner = CreateCombatLogParser(
            normalizerTypes: [typeof(FightBookendNormalizer), typeof(PullBookendNormalizer)]);

        await owner.Analyze(CreateEvents(), playerId: 7, fight: TestFight);

        var events = owner.Events;
        Assert.IsType<FightStartEvent>(events[0]);
        var pullStart = Assert.IsType<PullStartEvent>(events[1]);
        Assert.IsType<FightEndEvent>(events[^1]);
        var pullEnd = Assert.IsType<PullEndEvent>(events[^2]);

        Assert.Same(pullStart.Pull, pullEnd.Pull);
        Assert.Equal(owner.FightStartTime, pullStart.Timestamp);
        Assert.Equal(owner.FightEndTime, pullEnd.Timestamp);
        Assert.Single(events.OfType<PullStartEvent>());
        Assert.Single(events.OfType<PullEndEvent>());
    }
}
