using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>
/// Where Sylvie's four pink Flutterflies are. Each one is either banked - the tertiary resource counts
/// the unassigned ones - or parked on an ally, where it heals that ally on its own period until it is
/// taken off. Both placing abilities are tracked: <see cref="Spells.FluttercallHeal"/> and
/// <see cref="Spells.FluttercallRestoreLife"/> each open an assignment under their own heal effect, and
/// <see cref="PlacementOf"/> reads back which one placed a given assignment.
/// <para>
/// The bank and the assignments are two views of the same four flutterflies. A Fluttercall: Heal takes
/// one out of the bank and opens one assignment; a Fluttercall: Restore Life takes
/// <see cref="SylvieKit.RestoreLifeFlutterflies"/> and is marked by a single aura, which is why every
/// flutterfly-millisecond figure here counts a Restore Life assignment twice. Weighted that way the two
/// views reconcile: banked plus parked is always four.
/// </para>
/// <para>
/// A Restore Life window closes on its own removal event rather than on a modelled duration, so a
/// window cut short by a redirect ends where the log says it ended.
/// </para>
/// <para>
/// An assignment routinely outlives a pull: in the validation report the four opened in the first
/// seconds of the fight and two of them were still on the same ally 39 minutes later. That is why this
/// is fight-lifetime state - a pull-lifetime analyzer would see no application event at all and read a
/// Flutterfly that never left as a Flutterfly that was never there.
/// </para>
/// </summary>
public sealed partial class PinkFlutterflyTracker : HotTracker
{
    private readonly List<BankSample> _bank = [];

    /// <summary>The heal effect a Flutterfly placed by <see cref="Spells.FluttercallHeal"/> delivers, and the aura that marks its assignment.</summary>
    public static int FlutterflyHot => Spells.FluttercallHealHot.FSLID;

    /// <summary>The heal effect <see cref="Spells.FluttercallRestoreLife"/> delivers, and the aura that marks its assignment.</summary>
    public static int RestoreLifeHot => Spells.FluttercallRestoreLifeHot.FSLID;

    /// <summary>Every Flutterfly assignment this parse, whichever ability placed it, in the order it opened.</summary>
    public IEnumerable<HotAssignment> Flutterflies =>
        Assignments.Where(assignment => assignment.SpellId == FlutterflyHot || assignment.SpellId == RestoreLifeHot);

    /// <summary>Assignments <see cref="Spells.FluttercallHeal"/> placed, in the order they opened.</summary>
    public IEnumerable<HotAssignment> HealAssignments => AssignmentsOf(FlutterflyHot);

    /// <summary>Assignments <see cref="Spells.FluttercallRestoreLife"/> placed, in the order they opened.</summary>
    public IEnumerable<HotAssignment> RestoreLifeAssignments => AssignmentsOf(RestoreLifeHot);

    /// <summary>Which ability placed <paramref name="assignment"/>.</summary>
    public static FlutterflyPlacement PlacementOf(HotAssignment assignment) =>
        assignment.SpellId == RestoreLifeHot ? FlutterflyPlacement.RestoreLife : FlutterflyPlacement.Heal;

    /// <summary>The bank count at each moment the log reported one, in encounter order.</summary>
    public IReadOnlyList<BankSample> BankSamples => _bank;

    /// <summary>The highest number of Flutterflies observed banked at once.</summary>
    public int PeakBanked => _bank.Count > 0 ? _bank.Max(sample => sample.Count) : 0;

    /// <summary>Flutterflies banked at <paramref name="timestamp"/>, taking the last sample at or before it.</summary>
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
    /// Flutterfly-milliseconds spent parked on an ally between <paramref name="start"/> and
    /// <paramref name="end"/>. Four Flutterflies parked for the whole window reads four window-lengths.
    /// A Restore Life assignment counts <see cref="SylvieKit.RestoreLifeFlutterflies"/> times its
    /// elapsed time, because its single aura holds a pair.
    /// </summary>
    public long AssignedMsBetween(int start, int end) => AssignedMs(Flutterflies, start, end);

    /// <summary>
    /// Flutterfly-milliseconds the ability named by <paramref name="placement"/> had parked on an ally
    /// between <paramref name="start"/> and <paramref name="end"/>, in the same weighted unit as
    /// <see cref="AssignedMsBetween(int, int)"/>, so a Restore Life window counts double.
    /// </summary>
    public long AssignedMsBetween(int start, int end, FlutterflyPlacement placement) =>
        AssignedMs(
            placement == FlutterflyPlacement.RestoreLife ? RestoreLifeAssignments : HealAssignments,
            start,
            end);

    /// <summary>
    /// Every ally that held a Flutterfly between <paramref name="start"/> and <paramref name="end"/>,
    /// with the Flutterfly-milliseconds it held, heaviest first. Weighted like
    /// <see cref="AssignedMsBetween(int, int)"/>, so these sum to it.
    /// </summary>
    public IReadOnlyList<FlutterflyHolder> HoldersBetween(int start, int end)
    {
        var byUnit = new Dictionary<UnitKey, (long Ms, int Assignments)>();
        foreach (var Flutterfly in Flutterflies)
        {
            var from = Math.Max(Flutterfly.Start, start);
            var to = Math.Min(Flutterfly.End ?? end, end);
            if (to <= from) continue;

            var current = byUnit.GetValueOrDefault(Flutterfly.Unit);
            byUnit[Flutterfly.Unit] = (current.Ms + ((to - from) * FlutterfliesIn(Flutterfly.SpellId)), current.Assignments + 1);
        }

        return
        [
            .. byUnit
                .Select(entry => new FlutterflyHolder(entry.Key, entry.Value.Ms, entry.Value.Assignments))
                .OrderByDescending(holder => holder.AssignedMs)
                .ThenBy(holder => holder.Unit.ActorId)
        ];
    }

    /// <inheritdoc/>
    protected override bool TracksSpell(int spellId) => spellId == FlutterflyHot || spellId == RestoreLifeHot;

    private static long AssignedMs(IEnumerable<HotAssignment> assignments, int start, int end)
    {
        long assigned = 0;
        foreach (var Flutterfly in assignments)
        {
            var from = Math.Max(Flutterfly.Start, start);
            var to = Math.Min(Flutterfly.End ?? end, end);
            if (to > from) assigned += (long)(to - from) * FlutterfliesIn(Flutterfly.SpellId);
        }

        return assigned;
    }

    private static int FlutterfliesIn(int spellId) =>
        spellId == RestoreLifeHot ? SylvieKit.RestoreLifeFlutterflies : 1;

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

/// <summary>The ability that placed a pink flutterfly assignment.</summary>
public enum FlutterflyPlacement
{
    /// <summary><see cref="Spells.FluttercallHeal"/>, which sends one flutterfly on a heal that carries no listed duration.</summary>
    Heal,

    /// <summary>
    /// <see cref="Spells.FluttercallRestoreLife"/>, which sends
    /// <see cref="SylvieKit.RestoreLifeFlutterflies"/> under a single aura, on a window that closes on
    /// its own removal.
    /// </summary>
    RestoreLife,
}

/// <summary>One reading of how many pink Flutterflies were banked rather than parked on an ally.</summary>
/// <param name="Timestamp">When the reading was taken.</param>
/// <param name="Count">Flutterflies banked, after the resource stream's scaling.</param>
public sealed record BankSample(int Timestamp, int Count);

/// <summary>One ally's share of the Flutterflies over a window.</summary>
/// <param name="Unit">The ally.</param>
/// <param name="AssignedMs">Flutterfly-milliseconds it held, counting a Restore Life assignment double.</param>
/// <param name="Assignments">Distinct assignments parked on it during the window, a Restore Life pair counting once.</param>
public sealed record FlutterflyHolder(UnitKey Unit, long AssignedMs, int Assignments);
