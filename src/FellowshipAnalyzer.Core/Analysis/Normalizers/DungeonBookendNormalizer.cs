using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>
/// Merges a <see cref="DungeonStartEvent"/> at the dungeon's start timestamp and a
/// <see cref="DungeonEndEvent"/> at its end timestamp into the stream so modules can hook bookend
/// lifecycle work through <c>[On&lt;DungeonStartEvent&gt;]</c> / <c>[On&lt;DungeonEndEvent&gt;]</c>
/// handlers. Each is seated at its timestamp position - the open before the first event at or after it,
/// the close after the last event at or before it - so what comes back is still ascending. Running after
/// <see cref="PullBookendNormalizer"/> is what makes the dungeon bookends wrap the pull bookends.
/// </summary>
public sealed class DungeonBookendNormalizer(ParseContext parseContext) : IEventNormalizer
{
    /// <inheritdoc/>
    public int Priority => -999;

    /// <inheritdoc/>
    public List<Event> Normalize(List<Event> events, int playerId)
    {
        var start = new DungeonStartEvent { Timestamp = parseContext.DungeonStartTime };
        var end = new DungeonEndEvent { Timestamp = parseContext.DungeonEndTime };

        var result = new List<Event>(events.Count + 2);
        var startPlaced = false;
        var endPlaced = false;

        foreach (var e in events)
        {
            if (!startPlaced && e.Timestamp >= start.Timestamp)
            {
                result.Add(start);
                startPlaced = true;
            }

            if (!endPlaced && e.Timestamp > end.Timestamp)
            {
                result.Add(end);
                endPlaced = true;
            }

            result.Add(e);
        }

        if (!startPlaced) result.Add(start);
        if (!endPlaced) result.Add(end);

        return result;
    }
}
