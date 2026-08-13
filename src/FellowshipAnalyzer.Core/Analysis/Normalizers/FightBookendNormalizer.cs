using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>
/// Prepends a <see cref="DungeonStartEvent"/> at the fight's start timestamp and appends a
/// <see cref="DungeonEndEvent"/> at its end timestamp so modules can hook bookend lifecycle
/// work through <c>[On&lt;FightStartEvent&gt;]</c> / <c>[On&lt;FightEndEvent&gt;]</c> handlers.
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
            new DungeonStartEvent { Timestamp = parseContext.FightStartTime },
            .. events,
            new DungeonEndEvent { Timestamp = parseContext.FightEndTime }
        ];
    }
}
