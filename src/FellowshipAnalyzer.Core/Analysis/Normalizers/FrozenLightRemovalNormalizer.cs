using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>Drops the Frozen Light stack change to 1 before a removal less than 30 seconds after the buff's application on the same target, and sets that removal's <see cref="RemoveBuffEvent.Stack"/> to 0.</summary>
public sealed class FrozenLightRemovalNormalizer : IEventNormalizer
{
    private const int BuffDurationMs = 30_000;

    /// <inheritdoc/>
    public int Priority => 25;

    /// <inheritdoc/>
    public List<Event> Normalize(List<Event> events, int playerId)
    {
        var appliedAtByTarget = new Dictionary<int, int>();
        var lastStackChangeByTarget = new Dictionary<int, BuffEvent>();
        var dropped = new HashSet<Event>(ReferenceEqualityComparer.Instance);

        foreach (var e in events)
        {
            if (e is not BuffEvent buffEvent || buffEvent.Ability.FSLID != Items.RepositoryOfFrozenLightBuff.FSLID) continue;

            switch (buffEvent)
            {
                case ApplyBuffEvent:
                    appliedAtByTarget[buffEvent.TargetId] = buffEvent.Timestamp;
                    lastStackChangeByTarget.Remove(buffEvent.TargetId);
                    break;
                case IBuffStackEvent:
                    lastStackChangeByTarget[buffEvent.TargetId] = buffEvent;
                    break;
                case RemoveBuffEvent removal:
                    if (appliedAtByTarget.Remove(removal.TargetId, out var appliedAt)
                        && lastStackChangeByTarget.Remove(removal.TargetId, out var previous)
                        && previous is IBuffStackEvent { Stack: 1 }
                        && removal.Timestamp - appliedAt < BuffDurationMs)
                    {
                        dropped.Add(previous);
                        removal.Stack = 0;
                    }
                    break;
            }
        }

        return dropped.Count == 0 ? events : events.Where(e => !dropped.Contains(e)).ToList();
    }
}
