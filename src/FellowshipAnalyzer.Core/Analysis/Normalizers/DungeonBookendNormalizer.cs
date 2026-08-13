using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>
/// Prepends a <see cref="DungeonStartEvent"/> at the dungeon's start timestamp and appends a
/// <see cref="DungeonEndEvent"/> at its end timestamp so modules can hook bookend lifecycle
/// work through <c>[On&lt;DungeonStartEvent&gt;]</c> / <c>[On&lt;DungeonEndEvent&gt;]</c> handlers.
/// </summary>
public sealed class DungeonBookendNormalizer(ParseContext parseContext) : IEventNormalizer
{
    /// <inheritdoc/>
    public int Priority => -999;

    /// <inheritdoc/>
    public List<Event> Normalize(List<Event> events, int playerId)
    {
        return
        [
            new DungeonStartEvent { Timestamp = parseContext.DungeonStartTime },
            .. events,
            new DungeonEndEvent { Timestamp = parseContext.DungeonEndTime }
        ];
    }
}
