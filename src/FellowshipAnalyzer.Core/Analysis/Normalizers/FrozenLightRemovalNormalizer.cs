using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>Drops a Frozen Light stack change to 1 within 50 milliseconds of the buff's removal on the same target, and sets that removal's <see cref="RemoveBuffEvent.Stack"/> to 0.</summary>
public sealed class FrozenLightRemovalNormalizer : IEventNormalizer
{
    private const int FinalStackWindowMs = 50;

    /// <inheritdoc/>
    public int Priority => 25;

    /// <inheritdoc/>
    public List<Event> Normalize(List<Event> events, int playerId)
    {
        var lastByTarget = new Dictionary<int, BuffEvent>();
        var dropped = new HashSet<Event>(ReferenceEqualityComparer.Instance);

        foreach (var e in events)
        {
            if (e is not BuffEvent buffEvent || buffEvent.Ability.FSLID != Items.RepositoryOfFrozenLightBuff.FSLID) continue;

            if (buffEvent is RemoveBuffEvent removal
                && lastByTarget.TryGetValue(removal.TargetId, out var previous)
                && previous is RemoveBuffStackEvent { Stack: 1 }
                && removal.Timestamp - previous.Timestamp <= FinalStackWindowMs)
            {
                dropped.Add(previous);
                removal.Stack = 0;
            }

            lastByTarget[buffEvent.TargetId] = buffEvent;
        }

        return dropped.Count == 0 ? events : events.Where(e => !dropped.Contains(e)).ToList();
    }
}
