using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Base class for trackable entities (players, enemies).
/// Maintains the full buff/debuff history and provides query methods.
/// <para>
/// Every query names its spell as a <see cref="SpellRef"/>, which takes either the
/// <see cref="Spell"/> or the raw id <see cref="Spell.Id"/> carries.
/// </para>
/// </summary>
public abstract class Entity
{
    /// <summary>The full buff/debuff history tracked for this unit, in application order.</summary>
    public List<TrackedBuffEvent> Buffs { get; } = [];

    /// <summary>
    /// Returns all buff activations for a spell, optionally filtered by source.
    /// </summary>
    public IEnumerable<TrackedBuffEvent> GetBuffHistory(SpellRef spell, int? sourceId = null)
    {
        var bySpell = SpellIdFilter(spell);
        var bySource = SourceIdFilter(sourceId);
        return Buffs.Where(b => bySpell(b) && bySource(b));
    }

    /// <summary>
    /// Returns the active buff for a spell at the given timestamp (current if null).
    /// </summary>
    public TrackedBuffEvent? GetBuff(SpellRef spell, int? forTimestamp = null, int bufferTime = 0, int minimalActiveTime = 0, int? sourceId = null)
    {
        var bySpell = SpellIdFilter(spell);
        var active = ActiveAtTimestampFilter(forTimestamp, bufferTime, minimalActiveTime);
        var bySource = SourceIdFilter(sourceId);
        return Buffs.FirstOrDefault(b => bySpell(b) && active(b) && bySource(b));
    }

    /// <summary>
    /// Returns true if the spell buff is active at the given timestamp.
    /// </summary>
    public bool HasBuff(SpellRef spell, int? forTimestamp = null, int bufferTime = 0, int minimalActiveTime = 0, int? sourceId = null)
        => GetBuff(spell, forTimestamp, bufferTime, minimalActiveTime, sourceId) is not null;

    /// <summary>
    /// Returns the current stack count of the buff, or 0 if not present.
    /// </summary>
    public int GetBuffStacks(SpellRef spell, int? forTimestamp = null, int? sourceId = null)
        => GetBuff(spell, forTimestamp, sourceId: sourceId)?.Stacks ?? 0;

    /// <summary>
    /// Returns total uptime in milliseconds for the given spell buff.
    /// </summary>
    public int GetBuffUptime(SpellRef spell, int? sourceId = null)
        => GetBuffHistory(spell, sourceId)
            .Sum(b => (b.End ?? b.Start) - b.Start);

    /// <summary>
    /// Counts the concurrently-open aura windows for an effect active at <paramref name="timestamp"/>
    /// (closed interval: <c>Start &lt;= timestamp &lt;= End</c>, an unclosed window counting as active),
    /// optionally restricted to auras applied by <paramref name="sourceId"/>. Multi-instance auras open
    /// one window per application, so this returns how many stack independently on the unit.
    /// </summary>
    public int GetAuraInstanceCount(SpellRef effect, long timestamp, int? sourceId = null)
    {
        var count = 0;
        foreach (var b in Buffs)
        {
            if (!effect.Matches(b.Ability.FSLID)) continue;
            if (sourceId is not null && b.SourceId != sourceId) continue;
            if (b.Start <= timestamp && ((long?)b.End ?? long.MaxValue) >= timestamp)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Sums the stacks carried by every concurrently-open window of an effect active at
    /// <paramref name="timestamp"/>, optionally restricted to auras applied by <paramref name="sourceId"/>.
    /// A stacking aura contributes its stack count and a multi-instance aura one per window, so the sum
    /// reads as the applications on the unit either way. Only meaningful while the parse is
    /// dispatching, since a window's stack count tracks the live value rather than the value at
    /// <paramref name="timestamp"/>.
    /// </summary>
    public int GetAuraStackSum(SpellRef effect, long timestamp, int? sourceId = null)
    {
        var stacks = 0;
        foreach (var b in Buffs)
        {
            if (!effect.Matches(b.Ability.FSLID)) continue;
            if (sourceId is not null && b.SourceId != sourceId) continue;
            if (b.Start <= timestamp && ((long?)b.End ?? long.MaxValue) >= timestamp)
                stacks += Math.Max(b.Stacks, 1);
        }
        return stacks;
    }

    /// <summary>
    /// Every window of an effect on this unit that overlaps <paramref name="from"/>..<paramref name="to"/>,
    /// clipped to that range, optionally restricted to auras applied by <paramref name="sourceId"/>. A
    /// window still open at the end of the range closes there.
    /// </summary>
    public IEnumerable<AuraWindow> GetAuraWindows(SpellRef effect, int from, int to, int? sourceId = null)
    {
        foreach (var b in Buffs)
        {
            if (!effect.Matches(b.Ability.FSLID)) continue;
            if (sourceId is not null && b.SourceId != sourceId) continue;

            var end = Math.Min(b.End ?? to, to);
            if (b.Start > to || end < from) continue;

            yield return new AuraWindow(Math.Max(b.Start, from), end);
        }
    }

    internal void ApplyBuff(TrackedBuffEvent buff) => Buffs.Add(buff);

    /// <summary>A predicate matching a tracked buff that <paramref name="spell"/> names.</summary>
    protected Func<TrackedBuffEvent, bool> SpellIdFilter(SpellRef spell)
        => b => spell.Matches(b.Ability.FSLID);

    /// <summary>A predicate matching a tracked buff by its applying source, or matching everything when <paramref name="sourceId"/> is <c>null</c>.</summary>
    protected Func<TrackedBuffEvent, bool> SourceIdFilter(int? sourceId)
        => sourceId is null ? _ => true : b => b.SourceId == sourceId;

    /// <summary>A predicate matching a tracked buff that was active at <paramref name="timestamp"/>, allowing <paramref name="bufferTime"/> past its end and requiring at least <paramref name="minimalActiveTime"/> since its start. Matches only still-open buffs when <paramref name="timestamp"/> is <c>null</c>.</summary>
    protected Func<TrackedBuffEvent, bool> ActiveAtTimestampFilter(int? timestamp, int bufferTime = 0, int minimalActiveTime = 0)
    {
        if (timestamp is null)
            return b => b.End is null;

        return b =>
            timestamp - minimalActiveTime >= b.Start &&
            (b.End is null || b.End + bufferTime >= timestamp);
    }
}
