using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

public class EventSubscriber : Module
{
    internal int NumExecutions { get; set; }

    public void AddEventListener<T>(EventFilter<T> filter, Action<T> callback) where T : Event
    {
        var compiledFilter = filter.Build(Owner);
        void handler(Event e) => callback((T)e);
        Owner.EventEmitter.Subscribe(this, compiledFilter, handler);
    }

    public void AddEventListener<T>(EventFilter<T> filter, Func<T, Task> callback) where T : Event
    {
        var compiledFilter = filter.Build(Owner);
        Task handler(Event e) => callback((T)e);
        Owner.EventEmitter.Subscribe(this, compiledFilter, handler);
    }
}
