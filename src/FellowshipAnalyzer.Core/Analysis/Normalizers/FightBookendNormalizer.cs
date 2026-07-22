using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>
/// Prepends a <see cref="FightStartEvent"/> at the fight's start timestamp and appends a
/// <see cref="FightEndEvent"/> at its end timestamp so modules can hook bookend lifecycle
/// work through <c>[On&lt;FightStartEvent&gt;]</c> / <c>[On&lt;FightEndEvent&gt;]</c> handlers.
/// </summary>
public sealed class FightBookendNormalizer(ParseContext parseContext) : IEventNormalizer
{
    public int Priority => -999;

    public List<Event> Normalize(List<Event> events, int playerId)
    {
        return
        [
            new FightStartEvent { Timestamp = parseContext.FightStartTime },
            .. events,
            new FightEndEvent { Timestamp = parseContext.FightEndTime }
        ];
    }
}
