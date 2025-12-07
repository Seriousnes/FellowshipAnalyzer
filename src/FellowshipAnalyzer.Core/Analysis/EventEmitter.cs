using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Central pub/sub event dispatcher. Manages listener registration, event triggering,
/// and fabricated (synthetic) event injection.
/// Fabricated events are inserted into the main event queue immediately after the
/// current event, so they are processed as the very next event.
/// Registered as a scoped DI service — one instance per analysis run.
/// </summary>
public sealed class EventEmitter : Module
{
    private readonly List<RegisteredListener> _listeners = [];
    private List<Event>? _events;
    private int _insertionIndex;

    public void Subscribe(EventSubscriber module, Func<Event, bool> filter, Action<Event> handler)
    {
        _listeners.Add(new RegisteredListener(module, filter, handler));
    }

    public void Subscribe(EventSubscriber module, Func<Event, bool> filter, Func<Event, Task> handler)
    {
        _listeners.Add(new RegisteredListener(module, filter, handler));
    }

    public void SortListeners()
    {
        _listeners.Sort(static (a, b) => a.Module.Priority.CompareTo(b.Module.Priority));
    }

    /// <summary>
    /// Dispatches all events sequentially, processing fabricated events inline.
    /// </summary>
    public async Task DispatchEventsAsync(List<Event> events)
    {
        _events = events;
        events.Sort(static (a, b) => a.Timestamp.CompareTo(b.Timestamp));

        for (var i = 0; i < events.Count; i++)
        {
            _insertionIndex = i;
            await TriggerEventAsync(events[i]);
        }

        _events = null;
    }

    private async Task TriggerEventAsync(Event e)
    {
        foreach (var listener in _listeners)
        {
            if (listener.Module.Active && listener.Filter(e))
            {
                listener.Module.NumExecutions++;
                await listener.InvokeAsync(e);
            }
        }
    }

    /// <summary>
    /// Inserts a fabricated event into the event list immediately after the current event,
    /// so it will be the very next event processed.
    /// </summary>
    public T FabricateEvent<T>(T e, object? trigger = null) where T : Event
    {
        e.Fabricated = true;
        e.Trigger = trigger;

        if (_events != null)
        {
            _events.Insert(++_insertionIndex, e);
        }

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
}
