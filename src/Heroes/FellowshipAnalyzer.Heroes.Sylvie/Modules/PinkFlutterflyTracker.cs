using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

public sealed partial class PinkFlutterflyTracker : HotTracker
{
    private readonly List<BankSample> _bank = [];

    public static int FlutterflyHot => Spells.FluttercallHealHot.FSLID;

    public static int RestoreLifeHot => Spells.FluttercallRestoreLifeHot.FSLID;

    public IEnumerable<HotAssignment> Flutterflies =>
        Assignments.Where(assignment => assignment.SpellId == FlutterflyHot || assignment.SpellId == RestoreLifeHot);

    public IEnumerable<HotAssignment> HealAssignments => AssignmentsOf(FlutterflyHot);

    public IEnumerable<HotAssignment> RestoreLifeAssignments => AssignmentsOf(RestoreLifeHot);

    public static FlutterflyPlacement PlacementOf(HotAssignment assignment) =>
        assignment.SpellId == RestoreLifeHot ? FlutterflyPlacement.RestoreLife : FlutterflyPlacement.Heal;

    public List<BankSample> BankSamples => _bank;

    public int PeakBanked => _bank.Count > 0 ? _bank.Max(sample => sample.Count) : 0;

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

    public long AssignedMsBetween(int start, int end) => AssignedMs(Flutterflies, start, end);

    public long AssignedMsBetween(int start, int end, FlutterflyPlacement placement) =>
        AssignedMs(
            placement == FlutterflyPlacement.RestoreLife ? RestoreLifeAssignments : HealAssignments,
            start,
            end);

    public List<FlutterflyHolder> HoldersBetween(int start, int end)
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

public enum FlutterflyPlacement
{
    Heal,

    RestoreLife,
}

public sealed record BankSample(int Timestamp, int Count);

public sealed record FlutterflyHolder(UnitKey Unit, long AssignedMs, int Assignments);
