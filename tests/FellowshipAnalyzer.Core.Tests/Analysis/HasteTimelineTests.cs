using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed class HasteTimelineTests
{
    private const int PlayerId = 7;

    private static ChangeHasteEvent Change(int timestamp, double haste, int sourceId = PlayerId) =>
        new() { Timestamp = timestamp, SourceId = sourceId, TargetId = sourceId, NewHaste = haste };

    [Fact]
    public void Mean_WeighsEachHasteByTheTimeItHeldInsideTheSpans()
    {
        List<Event> events = [Change(0, 0.10), Change(5_000, 0.30)];

        Assert.Equal(0.20, HasteTimeline.Mean(events, PlayerId, [(0, 10_000)]), precision: 6);
    }

    [Fact]
    public void Mean_OpensASpanOnTheHasteInForceAtItsStart()
    {
        List<Event> events = [Change(0, 0.10), Change(5_000, 0.30)];

        Assert.Equal(0.30, HasteTimeline.Mean(events, PlayerId, [(6_000, 8_000)]), precision: 6);
    }

    [Fact]
    public void Mean_ReadsOnlyThePlayersHaste()
    {
        List<Event> events = [Change(0, 0.10), Change(1_000, 0.90, sourceId: 99)];

        Assert.Equal(0.10, HasteTimeline.Mean(events, PlayerId, [(0, 4_000)]), precision: 6);
    }
}
