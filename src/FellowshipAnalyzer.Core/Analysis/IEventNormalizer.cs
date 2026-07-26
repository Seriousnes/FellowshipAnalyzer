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
    int Priority { get; }
    List<Event> Normalize(List<Event> events, int playerId);
}
