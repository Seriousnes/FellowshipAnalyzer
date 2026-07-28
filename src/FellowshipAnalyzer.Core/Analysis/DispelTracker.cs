using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Every aura the player dispelled, tallied per dispelling ability and per unit it was taken off.
/// The log names both the ability that dispelled and the aura it removed, so nothing here is
/// inferred; a dispel the player never got to is not an event and cannot be counted.
/// </summary>
public sealed partial class DispelTracker : EventSubscriber
{
    private readonly List<DispelRecord> _dispels = [];

    /// <summary>Every dispel the player performed, in encounter order.</summary>
    public IReadOnlyList<DispelRecord> Dispels => _dispels;

    /// <summary>Dispels the player performed.</summary>
    public int TotalDispels => _dispels.Count;

    /// <summary>Dispels performed with <paramref name="spellId"/>.</summary>
    public int DispelsWith(int spellId) => _dispels.Count(dispel => dispel.SpellId == spellId);

    /// <summary>Dispels performed between <paramref name="start"/> and <paramref name="end"/>, inclusive of both.</summary>
    public IEnumerable<DispelRecord> Between(int start, int end) =>
        _dispels.Where(dispel => dispel.Timestamp >= start && dispel.Timestamp <= end);

    /// <summary>Dispel counts keyed by the ability that was removed.</summary>
    public IReadOnlyDictionary<int, int> ByRemovedAura => field ??= Tally(dispel => dispel.RemovedSpellId);

    /// <summary>Dispel counts keyed by the unit the aura was taken off.</summary>
    public IReadOnlyDictionary<UnitKey, int> ByTarget => field ??= Tally(dispel => dispel.Target);

    /// <summary>Dispel counts keyed by the dispelling ability.</summary>
    public IReadOnlyDictionary<int, int> BySpell => field ??= Tally(dispel => dispel.SpellId);

    [On<DispelEvent>(By = Actor.Player)]
    private void OnDispel(DispelEvent dispelEvent) =>
        _dispels.Add(new DispelRecord(
            dispelEvent.Timestamp,
            dispelEvent.Ability.Id,
            dispelEvent.ExtraAbility?.Id ?? 0,
            dispelEvent.ExtraAbility?.Name ?? string.Empty,
            new UnitKey(dispelEvent.TargetId, dispelEvent.TargetInstance ?? 0),
            dispelEvent.IsBuff));

    private Dictionary<TKey, int> Tally<TKey>(Func<DispelRecord, TKey> selector) where TKey : notnull
    {
        var counts = new Dictionary<TKey, int>();
        foreach (var dispel in _dispels)
        {
            var key = selector(dispel);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts;
    }
}

/// <summary>
/// One dispel the player performed.
/// </summary>
/// <param name="Timestamp">When it happened.</param>
/// <param name="SpellId">The ability that dispelled.</param>
/// <param name="RemovedSpellId">The aura that was removed.</param>
/// <param name="RemovedName">The removed aura's name as the log reported it.</param>
/// <param name="Target">The unit the aura was taken off.</param>
/// <param name="WasBuff">Whether the removed aura was a buff rather than a debuff.</param>
public sealed record DispelRecord(
    int Timestamp,
    int SpellId,
    int RemovedSpellId,
    string RemovedName,
    UnitKey Target,
    bool WasBuff);
