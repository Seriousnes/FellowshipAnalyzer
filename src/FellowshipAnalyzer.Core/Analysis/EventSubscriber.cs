using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

public class EventSubscriber : Module
{
    internal int NumExecutions { get; set; }

    /// <summary>
    /// Wires up subscriptions declared via <see cref="OnAttribute{TEvent}"/>. The source generator
    /// emits an override of this method on partial subclasses; the base implementation is empty.
    /// </summary>
    protected virtual void RegisterAttributeSubscriptions() { }

    /// <summary>
    /// Called once per analysis run, after every module has been constructed and after
    /// <see cref="Module.Owner"/> has been assigned. Wires up declarative <c>[On&lt;&gt;]</c>
    /// handlers via <see cref="RegisterAttributeSubscriptions"/>.
    /// </summary>
    public void RegisterSubscriptions()
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
