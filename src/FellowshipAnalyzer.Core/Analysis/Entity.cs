using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Base class for trackable entities (players, enemies).
/// Maintains the full buff/debuff history and provides query methods.
/// </summary>
public abstract class Entity
{
    public List<TrackedBuffEvent> Buffs { get; } = [];

    /// <summary>
    /// Returns all buff activations for a spell, optionally filtered by source.
    /// </summary>
    public IEnumerable<TrackedBuffEvent> GetBuffHistory(int spellId, int? sourceId = null)
    {
        var bySpell = SpellIdFilter(spellId);
        var bySource = SourceIdFilter(sourceId);
        return Buffs.Where(b => bySpell(b) && bySource(b));
    }

    /// <summary>
    /// Returns the active buff for a spell at the given timestamp (current if null).
    /// </summary>
    public TrackedBuffEvent? GetBuff(int spellId, int? forTimestamp = null, int bufferTime = 0, int minimalActiveTime = 0, int? sourceId = null)
    {
        var bySpell = SpellIdFilter(spellId);
        var active = ActiveAtTimestampFilter(forTimestamp, bufferTime, minimalActiveTime);
        var bySource = SourceIdFilter(sourceId);
        return Buffs.FirstOrDefault(b => bySpell(b) && active(b) && bySource(b));
    }

    /// <summary>
    /// Returns true if the spell buff is active at the given timestamp.
    /// </summary>
    public bool HasBuff(int spellId, int? forTimestamp = null, int bufferTime = 0, int minimalActiveTime = 0, int? sourceId = null)
        => GetBuff(spellId, forTimestamp, bufferTime, minimalActiveTime, sourceId) is not null;

    /// <summary>
    /// Returns the current stack count of the buff, or 0 if not present.
    /// </summary>
    public int GetBuffStacks(int spellId, int? forTimestamp = null, int? sourceId = null)
        => GetBuff(spellId, forTimestamp, sourceId: sourceId)?.Stacks ?? 0;

    /// <summary>
    /// Returns total uptime in milliseconds for the given spell buff.
    /// </summary>
    public int GetBuffUptime(int spellId, int? sourceId = null)
        => GetBuffHistory(spellId, sourceId)
            .Sum(b => (b.End ?? b.Start) - b.Start);

    internal void ApplyBuff(TrackedBuffEvent buff) => Buffs.Add(buff);

    protected Func<TrackedBuffEvent, bool> SpellIdFilter(int spellId)
        => b => b.Ability.Id == spellId;

    protected Func<TrackedBuffEvent, bool> SourceIdFilter(int? sourceId)
        => sourceId is null ? _ => true : b => b.SourceId == sourceId;

    protected Func<TrackedBuffEvent, bool> ActiveAtTimestampFilter(int? timestamp, int bufferTime = 0, int minimalActiveTime = 0)
    {
        if (timestamp is null)
            return b => b.End is null;

        return b =>
            timestamp - minimalActiveTime >= b.Start &&
            (b.End is null || b.End + bufferTime >= timestamp);
    }
}
