using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>
/// Where Sylvie's four pink butterflies are. Each one is either banked - the tertiary resource counts
/// the unassigned ones - or parked on an ally, where it heals that ally on its own period until it is
/// taken off. A <see cref="Spells.FluttercallHeal"/> cast spends one from the bank and parks it on the
/// cast's target, so the bank and the assignments are two views of the same four butterflies.
/// <para>
/// An assignment routinely outlives a pull: in the validation report the four opened in the first
/// seconds of the fight and two of them were still on the same ally 39 minutes later. That is why this
/// is fight-lifetime state - a pull-lifetime analyzer would see no application event at all and read a
/// butterfly that never left as a butterfly that was never there.
/// </para>
/// </summary>
public sealed partial class PinkButterflyTracker : HotTracker
{
    private readonly List<BankSample> _bank = [];

    /// <summary>The heal effect a parked butterfly delivers, and the aura that marks its assignment.</summary>
    public static int ButterflyHot => Spells.FluttercallHealHot.FSLID;

    /// <summary>Every butterfly assignment this parse, in the order it opened.</summary>
    public IEnumerable<HotAssignment> Butterflies => AssignmentsOf(ButterflyHot);

    /// <summary>The bank count at each moment the log reported one, in encounter order.</summary>
    public IReadOnlyList<BankSample> BankSamples => _bank;

    /// <summary>The highest number of butterflies observed banked at once.</summary>
    public int PeakBanked => _bank.Count > 0 ? _bank.Max(sample => sample.Count) : 0;

    /// <summary>Butterflies banked at <paramref name="timestamp"/>, taking the last sample at or before it.</summary>
    public int BankedAt(int timestamp)
    {
        var banked = 0;
        foreach (var sample in _bank)
        {
            if (sample.Timestamp > timestamp) break;
            banked = sample.Count;
        }

        return banked;
    }

    /// <summary>
    /// Butterfly-milliseconds spent parked on an ally between <paramref name="start"/> and
    /// <paramref name="end"/>. Four butterflies parked for the whole window reads four window-lengths.
    /// </summary>
    public long AssignedMsBetween(int start, int end)
    {
        long assigned = 0;
        foreach (var butterfly in Butterflies)
        {
            var from = Math.Max(butterfly.Start, start);
            var to = Math.Min(butterfly.End ?? end, end);
            if (to > from) assigned += to - from;
        }

        return assigned;
    }

    /// <summary>
    /// Every ally that held a butterfly between <paramref name="start"/> and <paramref name="end"/>,
    /// with the butterfly-milliseconds it held, heaviest first.
    /// </summary>
    public IReadOnlyList<ButterflyHolder> HoldersBetween(int start, int end)
    {
        var byUnit = new Dictionary<UnitKey, (long Ms, int Assignments)>();
        foreach (var butterfly in Butterflies)
        {
            var from = Math.Max(butterfly.Start, start);
            var to = Math.Min(butterfly.End ?? end, end);
            if (to <= from) continue;

            var current = byUnit.GetValueOrDefault(butterfly.Unit);
            byUnit[butterfly.Unit] = (current.Ms + (to - from), current.Assignments + 1);
        }

        return
        [
            .. byUnit
                .Select(entry => new ButterflyHolder(entry.Key, entry.Value.Ms, entry.Value.Assignments))
                .OrderByDescending(holder => holder.AssignedMs)
                .ThenBy(holder => holder.Unit.ActorId)
        ];
    }

    /// <inheritdoc/>
    protected override bool TracksSpell(int spellId) => spellId == ButterflyHot;

    [On<Event>]
    private void OnAnyEvent(Event e)
    {
        var resources = e switch
        {
            IHasSourceEvent source when Owner.ByPlayer(source) => e.SourceResources,
            IHasTargetEvent target when Owner.ToPlayer(target) => e.TargetResources,
            _ => null,
        };

        if (resources?.Resources is not { Count: > 0 } list) return;

        foreach (var resource in list)
        {
            if (resource.Type != ResourceTypes.Tertiary) continue;
            if (_bank.Count > 0 && _bank[^1].Count == resource.Amount) return;

            _bank.Add(new BankSample(e.Timestamp, resource.Amount));
            return;
        }
    }
}

/// <summary>One reading of how many pink butterflies were banked rather than parked on an ally.</summary>
/// <param name="Timestamp">When the reading was taken.</param>
/// <param name="Count">Butterflies banked, after the resource stream's scaling.</param>
public sealed record BankSample(int Timestamp, int Count);

/// <summary>One ally's share of the butterflies over a window.</summary>
/// <param name="Unit">The ally.</param>
/// <param name="AssignedMs">Butterfly-milliseconds it held.</param>
/// <param name="Assignments">Distinct butterflies parked on it during the window.</param>
public sealed record ButterflyHolder(UnitKey Unit, long AssignedMs, int Assignments);
