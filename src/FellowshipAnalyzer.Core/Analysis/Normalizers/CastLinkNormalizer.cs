using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Links BeginCastEvent to its completing CastEvent, and EndChannelEvent back to its
/// corresponding BeginChannelEvent. Runs before any module sees events.
/// </summary>
public sealed class CastLinkNormalizer : IEventNormalizer
{
    public int Priority => 0;

    public List<Event> Normalize(List<Event> events, int playerId)
    {
        var pendingCasts = new Dictionary<(int abilityId, int sourceId), BeginCastEvent>();
        var pendingChannels = new Dictionary<(int abilityId, int sourceId), BeginChannelEvent>();

        foreach (var e in events)
        {
            switch (e)
            {
                case BeginCastEvent bc when bc.Ability is not null:
                    pendingCasts[(bc.Ability.Guid, bc.SourceId)] = bc;
                    break;

                case CastEvent cast when cast.Ability is not null:
                    var castKey = (cast.Ability.Guid, cast.SourceId);
                    if (pendingCasts.TryGetValue(castKey, out var beginCast))
                    {
                        beginCast.CastEvent = cast;
                        pendingCasts.Remove(castKey);
                    }
                    break;

                case BeginChannelEvent beginChannel when beginChannel.Ability is not null:
                    pendingChannels[(beginChannel.Ability.Guid, beginChannel.SourceId)] = beginChannel;
                    break;

                case EndChannelEvent ec when ec.Ability is not null:
                    var ecKey = (ec.Ability.Guid, ec.SourceId);
                    if (pendingChannels.TryGetValue(ecKey, out var beginChannel2))
                    {
                        ec.BeginChannel = beginChannel2;
                        pendingChannels.Remove(ecKey);
                    }
                    break;
            }
        }

        return events;
    }
}
