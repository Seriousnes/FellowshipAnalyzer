using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Pre-processes a list of combat log events before they are dispatched to modules.
/// Normalizers run in the order declared via <c>[AddNormalizer&lt;T&gt;]</c> on the parser class,
/// by convention ascending by <see cref="Priority"/>; the declaration order, not the
/// <see cref="Priority"/> value, is what the parser executes.
/// They can reorder, link, fabricate, or drop events.
/// </summary>
public interface IEventNormalizer
{
    /// <summary>The relative order this normalizer runs in among the other registered normalizers, ascending.</summary>
    int Priority { get; }

    /// <summary>Applies this normalizer's transformation to <paramref name="events"/> for the player identified by <paramref name="playerId"/>.</summary>
    /// <param name="events">The event stream as normalized so far.</param>
    /// <param name="playerId">The FellowshipLogs id of the player the report is being analyzed for.</param>
    /// <returns>The normalized event list.</returns>
    List<Event> Normalize(List<Event> events, int playerId);
}
