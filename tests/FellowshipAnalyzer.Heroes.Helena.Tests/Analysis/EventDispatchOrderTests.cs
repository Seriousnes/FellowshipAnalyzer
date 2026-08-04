using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Helena.Tests.Analysis;

/// <summary>
/// Covers the two ordering guarantees Helena's Toughness measurement rests on: the parse-lifetime tier
/// always runs ahead of the pull tier, so a tracker has seen an event before any analyzer of that pull
/// can read it, and listeners within a tier dispatch in registration order.
/// <para>
/// These drive <see cref="EventEmitter"/> directly rather than through a parser, because the invariants
/// belong to the emitter and reproducing them through hero wiring would mean adding modules to the
/// production parser to observe them.
/// </para>
/// </summary>
public sealed class EventDispatchOrderTests
{
    private const int ListenerCount = 24;

    [Fact]
    public void TheParseLifetimeTier_DispatchesBeforeThePullTierWhateverRegisteredFirst()
    {
        var emitter = Emitter();
        var order = new List<string>();

        emitter.BeginPullSubscriptions();
        emitter.Subscribe(new EventSubscriber(), static _ => true, _ => order.Add("pull"));
        emitter.EndPullSubscriptions();

        emitter.Subscribe(new EventSubscriber(), static _ => true, _ => order.Add("state"));
        emitter.SortListeners();

        emitter.Emit(new DamageEvent());

        order.ShouldBe(["state", "pull"]);
    }

    /// <summary>
    /// A pull analyzer is never assigned a priority, so every listener in that tier shares the default and
    /// the sort comparison returns zero for every pair. Registration order is the only thing left to order
    /// the tier by, and only a stable sort preserves it. The listener count is deliberately above the 16
    /// below which an unstable sort would happen to hold, so this fails if the ordering is ever swapped
    /// back to <c>List&lt;T&gt;.Sort</c>.
    /// </summary>
    [Fact]
    public void ThePullTier_DispatchesInRegistrationOrderAcrossMoreListenersThanAnUnstableSortWouldHold()
    {
        var emitter = Emitter();
        var order = new List<int>();

        emitter.BeginPullSubscriptions();
        RegisterInterleaved(emitter, order);
        emitter.EndPullSubscriptions();

        emitter.Emit(new DamageEvent());

        order.ShouldBe([.. Enumerable.Range(0, ListenerCount)]);
    }

    [Fact]
    public void TheParseLifetimeTier_KeepsOneModulesHandlersInRegistrationOrder()
    {
        var emitter = Emitter();
        var order = new List<int>();

        RegisterInterleaved(emitter, order);
        emitter.SortListeners();

        emitter.Emit(new DamageEvent());

        order.ShouldBe([.. Enumerable.Range(0, ListenerCount)]);
    }

    /// <summary>
    /// Registers <see cref="ListenerCount"/> listeners across several subscribers with three handlers
    /// each, mirroring a real pull tier: ties both between modules and between one module's own handlers,
    /// every one of them at the default priority.
    /// </summary>
    private static void RegisterInterleaved(EventEmitter emitter, List<int> order)
    {
        const int HandlersPerModule = 3;

        for (var i = 0; i < ListenerCount; i += HandlersPerModule)
        {
            var subscriber = new EventSubscriber();
            subscriber.Priority.ShouldBe(0);

            for (var handler = 0; handler < HandlersPerModule; handler++)
            {
                var index = i + handler;
                emitter.Subscribe(subscriber, static _ => true, _ => order.Add(index));
            }
        }
    }

    private static EventEmitter Emitter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();

        return new EventEmitter(provider.GetRequiredService<ILogger<EventEmitter>>());
    }
}
