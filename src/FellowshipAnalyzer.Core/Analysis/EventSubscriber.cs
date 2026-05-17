using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

public class EventSubscriber : Module
{
    internal int NumExecutions { get; set; }

    /// <summary>
    /// Wires up subscriptions declared via <see cref="OnAttribute{TEvent}"/>. The source generator
    /// emits an override of this method on partial subclasses; the base implementation is empty.
    /// Override <see cref="Initialize"/> to add imperative subscriptions in addition.
    /// </summary>
    protected virtual void RegisterAttributeSubscriptions() { }

    public override void Initialize()
    {
        RegisterAttributeSubscriptions();
    }

    public void AddEventListener<T>(EventFilter<T> filter, Action<T> callback) where T : Event
    {
        var compiledFilter = filter.Compile(Owner);
        void handler(Event e) => callback((T)e);
        Owner.EventEmitter.Subscribe(this, compiledFilter, handler);
    }

    public void AddEventListener<T>(EventFilter<T> filter, Func<T, Task> callback) where T : Event
    {
        var compiledFilter = filter.Compile(Owner);
        Task handler(Event e) => callback((T)e);
        Owner.EventEmitter.Subscribe(this, compiledFilter, handler);
    }
}
