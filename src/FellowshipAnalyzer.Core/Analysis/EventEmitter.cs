using FellowshipAnalyzer.Core.Events;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Central pub/sub event dispatcher. Manages listener registration, event triggering,
/// and fabricated (synthetic) event injection.
/// Fabricated events are inserted into the main event queue immediately after the
/// current event, so they are processed as the very next event.
/// Resolved through the parser's per-analysis service cache so each analysis run has its own listener set.
/// </summary>
public sealed class EventEmitter(ILogger<EventEmitter> logger) : Module
{
    private readonly List<RegisteredListener> _stateListeners = [];
    private readonly List<RegisteredListener> _pullListeners = [];
    private bool _subscribingToPull;
    private List<Event>? _events;
    private int _insertionIndex;

    public void Subscribe(EventSubscriber module, Func<Event, bool> filter, Action<Event> handler)
    {
        (_subscribingToPull ? _pullListeners : _stateListeners).Add(new RegisteredListener(module, filter, handler));
    }

    public void Subscribe(EventSubscriber module, Func<Event, bool> filter, Func<Event, Task> handler)
    {
        (_subscribingToPull ? _pullListeners : _stateListeners).Add(new RegisteredListener(module, filter, handler));
    }

    public void SortListeners()
    {
        _stateListeners.Sort(static (a, b) => a.Module.Priority.CompareTo(b.Module.Priority));
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

    public void EndPullSubscriptions()
    {
        _pullListeners.Sort(static (a, b) => a.Module.Priority.CompareTo(b.Module.Priority));
        _subscribingToPull = false;
    }

    /// <summary>Retires the current pull's listeners at <see cref="Events.PullEndEvent"/>.</summary>
    public void ClearPullListeners()
    {
        _pullListeners.Clear();
    }

    /// <summary>
    /// Dispatches all events sequentially, processing fabricated events inline.
    /// Yields to the UI scheduler every <c>YieldInterval</c> events to maintain responsiveness.
    /// </summary>
    public async Task DispatchEventsAsync(List<Event> events, ReportLoadingTracker? tracker = null)
    {
        _events = events;
        events.Sort(CompareForDispatch);

        for (var i = 0; i < events.Count; i++)
        {
            _insertionIndex = i;
            var e = events[i];
            Owner.CurrentTimestamp = e.Timestamp;

            if (e is PullStartEvent pullStart) Owner.BeginPull(pullStart.Pull);

            if (e is PullEndEvent pullEnd)
                Owner.EndPull(pullEnd.Pull);
            else
                await TriggerEventAsync(e);

            if (i % YieldInterval == YieldInterval - 1)
            {
                if (tracker is not null)
                {
                    tracker.TotalEventCount = events.Count;
                    tracker.AnalyzedEventCount = i + 1;
                }
                await Task.Yield();
            }
        }

        _events = null;
    }

    private const int YieldInterval = 250;

    /// <summary>
    /// Dispatch ordering: primarily ascending <see cref="Event.Timestamp"/>, breaking ties by
    /// <see cref="Event.DispatchOrder"/> so fight/pull boundary events nest deterministically.
    /// Gameplay events that share both keys retain no defined relative order.
    /// </summary>
    public static int CompareForDispatch(Event a, Event b)
    {
        var byTimestamp = a.Timestamp.CompareTo(b.Timestamp);
        return byTimestamp != 0 ? byTimestamp : a.DispatchOrder.CompareTo(b.DispatchOrder);
    }

    private async Task TriggerEventAsync(Event e)
    {
        await DispatchToListenersAsync(_stateListeners, e);
        await DispatchToListenersAsync(_pullListeners, e);
    }

    /// <summary>
    /// Dispatches <paramref name="e"/> synchronously to the state and current pull listener tiers.
    /// Used by <see cref="CombatLogParser.EndPull"/> to deliver a pull's <see cref="Events.PullEndEvent"/>
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
    /// Inserts a fabricated event into the event list immediately after the current event,
    /// so it will be the very next event processed.
    /// </summary>
    public T FabricateEvent<T>(T e, Event? trigger = null) where T : Event
    {
        e.Fabricated = true;
        e.Trigger = trigger;

        _events?.Insert(++_insertionIndex, e);

        return e;
    }
}

/// <summary>
/// Wraps a listener registration supporting both synchronous and asynchronous handlers.
/// </summary>
public readonly struct RegisteredListener
{
    public EventSubscriber Module { get; }
    public Func<Event, bool> Filter { get; }
    private readonly Action<Event>? _syncHandler;
    private readonly Func<Event, Task>? _asyncHandler;

    public RegisteredListener(EventSubscriber module, Func<Event, bool> filter, Action<Event> handler)
    {
        Module = module;
        Filter = filter;
        _syncHandler = handler;
    }

    public RegisteredListener(EventSubscriber module, Func<Event, bool> filter, Func<Event, Task> handler)
    {
        Module = module;
        Filter = filter;
        _asyncHandler = handler;
    }

    public Task InvokeAsync(Event e)
    {
        if (_asyncHandler != null)
            return _asyncHandler(e);

        _syncHandler!(e);
        return Task.CompletedTask;
    }

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
