using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed class EventDispatchOrderTests
{
    private static Pull SamplePull => new(
        Index: 0, Id: 0, Name: "P", StartTime: 100, EndTime: 5000,
        Targets: PullKind.Single, IsBoss: false, Kill: false, TargetCount: 1);

    private static int CompareForDispatch(Event a, Event b)
    {
        var byTimestamp = a.Timestamp.CompareTo(b.Timestamp);
        return byTimestamp != 0 ? byTimestamp : a.DispatchOrder.CompareTo(b.DispatchOrder);
    }

    [Fact]
    public void DispatchOrder_NestsFightAroundPullAroundGameplay()
    {
        var fightStart = new FightStartEvent();
        var pullStart = new PullStartEvent { Pull = SamplePull };
        var gameplay = new ApplyBuffEvent();
        var pullEnd = new PullEndEvent { Pull = SamplePull };
        var fightEnd = new FightEndEvent();

        Assert.True(fightStart.DispatchOrder < pullStart.DispatchOrder);
        Assert.True(pullStart.DispatchOrder < gameplay.DispatchOrder);
        Assert.True(gameplay.DispatchOrder < pullEnd.DispatchOrder);
        Assert.True(pullEnd.DispatchOrder < fightEnd.DispatchOrder);
    }

    [Fact]
    public void Sort_NestsBoundariesAroundGameplayAtSharedTimestamps()
    {
        var fightStart = new FightStartEvent { Timestamp = 100 };
        var pullStart = new PullStartEvent { Timestamp = 100, Pull = SamplePull };
        var early = new ApplyBuffEvent { Timestamp = 100 };
        var mid = new ApplyBuffEvent { Timestamp = 2500 };
        var late = new ApplyBuffEvent { Timestamp = 5000 };
        var pullEnd = new PullEndEvent { Timestamp = 5000, Pull = SamplePull };
        var fightEnd = new FightEndEvent { Timestamp = 5000 };

        var events = new List<Event> { fightEnd, late, pullEnd, mid, early, pullStart, fightStart };
        events.Sort(CompareForDispatch);

        Assert.Equal(
            new Event[] { fightStart, pullStart, early, mid, late, pullEnd, fightEnd },
            events);
    }

    [Fact]
    public void Sort_OrdersByTimestampBeforeDispatchOrder()
    {
        var lateOpen = new FightStartEvent { Timestamp = 200 };
        var earlyClose = new FightEndEvent { Timestamp = 100 };

        var events = new List<Event> { lateOpen, earlyClose };
        events.Sort(CompareForDispatch);

        Assert.Equal(new Event[] { earlyClose, lateOpen }, events);
    }
}
