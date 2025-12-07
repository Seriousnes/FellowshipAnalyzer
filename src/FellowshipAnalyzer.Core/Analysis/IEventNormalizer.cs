using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Pre-processes a list of combat log events before they are dispatched to modules.
/// Normalizers run in <see cref="Priority"/> order before any module sees the events.
/// They can reorder, link, fabricate, or drop events.
/// </summary>
public interface IEventNormalizer
{
    int Priority { get; }
    List<Event> Normalize(List<Event> events, int playerId);
}
