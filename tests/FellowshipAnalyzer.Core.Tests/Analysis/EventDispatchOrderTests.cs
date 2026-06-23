using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed class EventDispatchOrderTests
{
    private static Pull SamplePull => new(
        Index: 0, Name: "P", StartTime: 100, EndTime: 5000,
        Targets: PullKind.Single, IsBoss: false, Kill: false, TargetCount: 1);

    [Fact]
    public void CompareForDispatch_NestsBoundariesAroundGameplayAtSharedTimestamps()
    {
        var fightStart = new FightStartEvent { Timestamp = 100 };
        var pullStart = new PullStartEvent { Timestamp = 100, Pull = SamplePull };
        var early = new ApplyBuffEvent { Timestamp = 100 };
        var mid = new ApplyBuffEvent { Timestamp = 2500 };
        var late = new ApplyBuffEvent { Timestamp = 5000 };
        var pullEnd = new PullEndEvent { Timestamp = 5000, Pull = SamplePull };
        var fightEnd = new FightEndEvent { Timestamp = 5000 };

        var events = new List<Event> { fightEnd, late, pullEnd, mid, early, pullStart, fightStart };
        events.Sort(EventEmitter.CompareForDispatch);

        Assert.Equal(
            new Event[] { fightStart, pullStart, early, mid, late, pullEnd, fightEnd },
            events);
    }

    [Fact]
    public void CompareForDispatch_OpensBeforeAndClosesAfterSameTimestampGameplay()
    {
        var open = new PullStartEvent { Timestamp = 100, Pull = SamplePull };
        var close = new PullEndEvent { Timestamp = 100, Pull = SamplePull };
        var gameplay = new ApplyBuffEvent { Timestamp = 100 };

        Assert.True(EventEmitter.CompareForDispatch(open, gameplay) < 0);
        Assert.True(EventEmitter.CompareForDispatch(close, gameplay) > 0);
        Assert.True(EventEmitter.CompareForDispatch(open, close) < 0);
    }

    [Fact]
    public void CompareForDispatch_OrdersByTimestampBeforeDispatchOrder()
    {
        var lateOpen = new FightStartEvent { Timestamp = 200 };
        var earlyClose = new FightEndEvent { Timestamp = 100 };

        Assert.True(EventEmitter.CompareForDispatch(earlyClose, lateOpen) < 0);
    }
}
