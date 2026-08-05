using FellowshipAnalyzer.Core.Events;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Central pub/sub event dispatcher. Manages listener registration, event triggering,
/// and fabricated (synthetic) event injection.
/// A fabricated event at the current dispatch timestamp is spliced in immediately after the
/// current event and processed next; one with a future timestamp is inserted after every
/// already-queued event at or before its timestamp. See <see cref="FabricateEvent"/>.
/// Resolved through the parser's per-analysis service cache so each analysis run has its own listener set.
/// </summary>
public sealed class EventEmitter(ILogger<EventEmitter> logger) : Module
{
    private List<RegisteredListener> _stateListeners = [];
    private List<RegisteredListener> _pullListeners = [];
    private readonly List<Event> _scheduled = [];
    private bool _subscribingToPull;
    private List<Event>? _events;
    private int _insertionIndex;

    /// <summary>Registers a synchronous handler for <paramref name="module"/>, routed into the pull or state listener tier depending on whether a pull is currently registering its subscriptions.</summary>
    public void Subscribe(Analyzer module, Func<Event, bool> filter, Action<Event> handler)
    {
        (_subscribingToPull ? _pullListeners : _stateListeners).Add(new RegisteredListener(module, filter, handler));
    }

    /// <summary>Registers an asynchronous handler for <paramref name="module"/>, routed into the pull or state listener tier depending on whether a pull is currently registering its subscriptions.</summary>
    public void Subscribe(Analyzer module, Func<Event, bool> filter, Func<Event, Task> handler)
    {
        (_subscribingToPull ? _pullListeners : _stateListeners).Add(new RegisteredListener(module, filter, handler));
    }

    /// <summary>
    /// Orders the state listener tier by <see cref="Module.Priority"/>. Called once the parse-lifetime
    /// modules have registered, which is the only time that tier changes.
    /// </summary>
    public void SortListeners()
    {
        _stateListeners = Ordered(_stateListeners);
    }

    /// <summary>
    /// Routes subscriptions registered while open into the per-pull listener tier. Called by the
    /// parser when constructing a pull's analyzers; paired with <see cref="EndPullSubscriptions"/>.
    /// </summary>
    public void BeginPullSubscriptions()
    {
        _pullListeners.Clear();
        _subscribingToPull = true;
    }

    /// <summary>Orders the per-pull listener tier by <see cref="Module.Priority"/> and closes registration. Paired with <see cref="BeginPullSubscriptions"/>.</summary>
    public void EndPullSubscriptions()
    {
        _pullListeners = Ordered(_pullListeners);
        _subscribingToPull = false;
    }

    /// <summary>Retires the current pull's analyzers so they stop receiving events once the pull has ended.</summary>
    public void UnsubscribePullAnalyzers()
    {
        _pullListeners.Clear();
    }

    /// <summary>
    /// Orders one tier by <see cref="Module.Priority"/>. The sort has to stay stable: construction order
    /// is what carries the <c>[Before&lt;T&gt;]</c> and <c>[After&lt;T&gt;]</c> constraints, and only a
    /// stable sort leaves it intact. Modules sharing a priority with no constraint between them keep
    /// whatever relative order they were constructed in.
    /// </summary>
    private static List<RegisteredListener> Ordered(List<RegisteredListener> listeners) =>
        [.. listeners.OrderBy(static listener => listener.Module.Priority)];

    /// <summary>
    /// Dispatches all events sequentially, processing fabricated events inline.
    /// Yields to the UI scheduler on a <see cref="ProgressPacer"/> interval to maintain responsiveness.
    /// </summary>
    public async Task DispatchEventsAsync(List<Event> events, ReportLoadingTracker? tracker = null)
    {
        _events = events;
        _scheduled.Clear();
        var ordered = events.OrderBy(static e => e.Timestamp).ToArray();
        for (var i = 0; i < ordered.Length; i++)
            events[i] = ordered[i];

        var pacer = new ProgressPacer();
        for (var i = 0; i < events.Count; i++)
        {
            if (_scheduled.Count > 0 && _scheduled[0].Timestamp < events[i].Timestamp)
            {
                events.Insert(i, _scheduled[0]);
                _scheduled.RemoveAt(0);
            }

            _insertionIndex = i;
            var e = events[i];
            Owner.CurrentTimestamp = e.Timestamp;

            switch (e)
            {
                case PullStartEvent pullStart:
                    Owner.BeginPull(pullStart.Pull);
                    await TriggerEventAsync(e);
                    break;
                case PullEndEvent pullEnd:
                    Owner.EndPull(pullEnd.Pull);
                    break;
                default:
                    await TriggerEventAsync(e);
                    break;
            }

            if (pacer.ShouldYield(i))
            {
                if (tracker is not null)
                {
                    tracker.TotalEventCount = events.Count;
                    tracker.AnalyzedEventCount = i + 1;
                }
                await Task.Yield();
            }
        }

        if (tracker is not null)
        {
            tracker.TotalEventCount = events.Count;
            tracker.AnalyzedEventCount = events.Count;
        }

        _events = null;
        _scheduled.Clear();
    }

    private async Task TriggerEventAsync(Event e)
    {
        await DispatchToListenersAsync(_stateListeners, e);
        await DispatchToListenersAsync(_pullListeners, e);
    }

    /// <summary>
    /// Dispatches <paramref name="e"/> synchronously to the state and current pull listener tiers.
    /// Used by <see cref="CombatLogParser.EndPull"/> to deliver a pull's <see cref="FellowshipAnalyzer.Core.Events.PullEndEvent"/>
    /// to that pull's own listeners at the moment it closes, before the pull tier is retired — the one
    /// point that fires reliably for every pull, including a force-close by <see cref="CombatLogParser.BeginPull"/>.
    /// Pull-end handlers are synchronous.
    /// </summary>
    public void Emit(Event e)
    {
        DispatchToListeners(_stateListeners, e);
        DispatchToListeners(_pullListeners, e);
    }

    private void DispatchToListeners(List<RegisteredListener> listeners, Event e)
    {
        foreach (var listener in listeners)
        {
            if (listener.Module.Active && listener.Filter(e))
            {
                listener.Module.NumExecutions++;
                try
                {
                    listener.Invoke(e);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Event handler in {Module} threw while processing {EventType} (Timestamp={Timestamp})",
                        listener.Module.GetType().Name, e.GetType().Name, e.Timestamp);
                }
            }
        }
    }

    private async Task DispatchToListenersAsync(List<RegisteredListener> listeners, Event e)
    {
        foreach (var listener in listeners)
        {
            if (listener.Module.Active && listener.Filter(e))
            {
                listener.Module.NumExecutions++;
                try
                {
                    await listener.InvokeAsync(e);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Event handler in {Module} threw while processing {EventType} (Timestamp={Timestamp})",
                        listener.Module.GetType().Name, e.GetType().Name, e.Timestamp);
                }
            }
        }
    }

    /// <summary>
    /// Inserts a fabricated event into the event list at its timestamp position within the remaining
    /// stream, so dispatch is never driven out of order. An event at the current timestamp is spliced in
    /// immediately after the current event (and after any earlier fabrications from it), so it is the next
    /// event processed; a later event is placed after every queued event at or before its timestamp. A
    /// fabricated event is never dispatched earlier than the current one: a timestamp below the current
    /// dispatch time is clamped up to it.
    /// </summary>
    public T FabricateEvent<T>(T e, Event? trigger = null) where T : Event
    {
        e.Fabricated = true;
        e.Trigger = trigger;

        if (_events is not null)
        {
            var currentTimestamp = Owner.CurrentTimestamp;
            if (e.Timestamp < currentTimestamp)
                e.Timestamp = currentTimestamp;

            var index = e.Timestamp == currentTimestamp
                ? ++_insertionIndex
                : FutureInsertionIndex(e.Timestamp);
            _events.Insert(index, e);
        }

        return e;
    }

    private int FutureInsertionIndex(int timestamp)
    {
        var lo = _insertionIndex + 1;
        var hi = _events!.Count;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) / 2);
            if (_events[mid].Timestamp <= timestamp) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    /// <summary>
    /// Places <paramref name="e"/> on the private schedule queue, kept in ascending timestamp order. A scheduled
    /// event is invisible to anything scanning the main event list and is materialized into that list only
    /// when the drain in <see cref="DispatchEventsAsync"/> reaches an unscheduled event with a strictly
    /// greater timestamp. Used for a natural cooldown expiry whose true instant may fall in a gap between
    /// logged events. A scheduled end that never precedes a later event - one past the final event, such as a
    /// cooldown still recharging at fight end - is left unfired when the stream ends rather than dispatched in
    /// post-combat dead time.
    /// </summary>
    public void Schedule(Event e, Event? trigger = null)
    {
        e.Fabricated = true;
        if (trigger is not null) e.Trigger = trigger;
        InsertScheduled(e);
    }

    /// <summary>
    /// Repositions an already-scheduled event after its <see cref="Event.Timestamp"/> was mutated in place,
    /// so the queue stays ascending. A no-op if the event is not currently scheduled.
    /// </summary>
    public void Reschedule(Event e)
    {
        if (_scheduled.Remove(e))
            InsertScheduled(e);
    }

    /// <summary>Removes an event from the schedule queue before it materializes. A no-op if it is not scheduled.</summary>
    public void Cancel(Event e) => _scheduled.Remove(e);

    private void InsertScheduled(Event e)
    {
        var index = _scheduled.Count;
        for (var i = 0; i < _scheduled.Count; i++)
        {
            if (_scheduled[i].Timestamp > e.Timestamp)
            {
                index = i;
                break;
            }
        }
        _scheduled.Insert(index, e);
    }
}

/// <summary>
/// Wraps a listener registration supporting both synchronous and asynchronous handlers.
/// </summary>
public readonly struct RegisteredListener
{
    /// <summary>The subscriber this listener dispatches into, whose <see cref="Module.Priority"/> places it in the dispatch order and whose <see cref="Module.Active"/> gates whether it runs.</summary>
    public Analyzer Module { get; }

    /// <summary>The predicate an event must satisfy for this listener's handler to run.</summary>
    public Func<Event, bool> Filter { get; }
    private readonly Action<Event>? _syncHandler;
    private readonly Func<Event, Task>? _asyncHandler;

    /// <summary>Wraps a synchronous handler registered by <paramref name="module"/>.</summary>
    public RegisteredListener(Analyzer module, Func<Event, bool> filter, Action<Event> handler)
    {
        Module = module;
        Filter = filter;
        _syncHandler = handler;
    }

    /// <summary>Wraps an asynchronous handler registered by <paramref name="module"/>.</summary>
    public RegisteredListener(Analyzer module, Func<Event, bool> filter, Func<Event, Task> handler)
    {
        Module = module;
        Filter = filter;
        _asyncHandler = handler;
    }

    /// <summary>Invokes the wrapped handler for <paramref name="e"/>, awaiting it when asynchronous or running it synchronously otherwise.</summary>
    public Task InvokeAsync(Event e)
    {
        if (_asyncHandler != null)
            return _asyncHandler(e);

        _syncHandler!(e);
        return Task.CompletedTask;
    }

    /// <summary>Invokes the wrapped handler for <paramref name="e"/>, blocking on the asynchronous handler when the listener was registered with one.</summary>
    public void Invoke(Event e)
    {
        if (_syncHandler != null)
        {
            _syncHandler(e);
            return;
        }

        _asyncHandler!(e).GetAwaiter().GetResult();
    }
}
