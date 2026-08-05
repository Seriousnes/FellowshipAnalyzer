using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<PinkFlutterflyTracker>]
public sealed partial class PinkFlutterflyAnalyzer : Analyzer
{
    private readonly Dictionary<UnitKey, HoldingCapture> _byHolder = [];

    private Computed Result => field ??= Compute();

    public IReadOnlyList<FlutterflyHolding> Holdings => Result.Holdings;

    public long AssignedMs => Result.AssignedMs;

    public long AssignableMs => (long)SylvieKit.PinkFlutterflies * Math.Max(0, Pull.EndTime - Pull.StartTime);

    public double AssignedShare => AssignableMs > 0 ? Math.Min(1d, AssignedMs / (double)AssignableMs) : 0;

    public int AssignmentsOpened =>
        PinkFlutterflyTracker.Flutterflies.Count(Flutterfly => Flutterfly.Start >= Pull.StartTime && Flutterfly.Start <= Pull.EndTime);

    public int AssignmentsClosed =>
        PinkFlutterflyTracker.Flutterflies.Count(Flutterfly => Flutterfly.End >= Pull.StartTime && Flutterfly.End <= Pull.EndTime);

    public int PeakBanked => Result.PeakBanked;

    public long Effective => Result.Effective;

    public long Overheal => Result.Overheal;

    public int HoldersCovered => Result.Holdings.Count;

    public int RestoreLifeAssignmentsOpened =>
        PinkFlutterflyTracker.RestoreLifeAssignments
            .Count(Flutterfly => Flutterfly.Start >= Pull.StartTime && Flutterfly.Start <= Pull.EndTime);

    public long RestoreLifeAssignedMs =>
        PinkFlutterflyTracker.AssignedMsBetween(Pull.StartTime, Pull.EndTime, FlutterflyPlacement.RestoreLife);

    public int RestoreLifeTicks { get; private set; }

    public long RestoreLifeEffective { get; private set; }

    public long RestoreLifeOverheal { get; private set; }

    [On<HealEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.FluttercallHealHot),
        nameof(Spells.FluttercallRestoreLifeHot),
    })]
    private void OnFlutterflyTick(HealEvent healEvent)
    {
        var unit = new UnitKey(healEvent.TargetId, healEvent.TargetInstance ?? 0);
        if (!_byHolder.TryGetValue(unit, out var capture))
            _byHolder[unit] = capture = new HoldingCapture();

        capture.Ticks++;
        capture.Effective += healEvent.Amount;
        capture.Overheal += healEvent.Overheal ?? 0;

        if (healEvent.Ability.Id != PinkFlutterflyTracker.RestoreLifeHot) return;

        RestoreLifeTicks++;
        RestoreLifeEffective += healEvent.Amount;
        RestoreLifeOverheal += healEvent.Overheal ?? 0;
    }

    private Computed Compute()
    {
        var assignedByUnit = PinkFlutterflyTracker
            .HoldersBetween(Pull.StartTime, Pull.EndTime)
            .ToDictionary(holder => holder.Unit);

        long effective = 0, overheal = 0;
        var holdings = new List<FlutterflyHolding>(_byHolder.Count);

        foreach (var (unit, capture) in _byHolder)
        {
            effective += capture.Effective;
            overheal += capture.Overheal;

            var held = assignedByUnit.GetValueOrDefault(unit);
            holdings.Add(new FlutterflyHolding(
                unit,
                held?.AssignedMs ?? 0,
                held?.Assignments ?? 0,
                capture.Ticks,
                capture.Effective,
                capture.Overheal));
        }

        holdings.Sort(static (left, right) => right.AssignedMs.CompareTo(left.AssignedMs));

        var banked = 0;
        foreach (var sample in PinkFlutterflyTracker.BankSamples)
        {
            if (sample.Timestamp < Pull.StartTime || sample.Timestamp > Pull.EndTime) continue;
            banked = Math.Max(banked, sample.Count);
        }

        return new Computed(
            holdings,
            PinkFlutterflyTracker.AssignedMsBetween(Pull.StartTime, Pull.EndTime),
            Math.Max(banked, PinkFlutterflyTracker.BankedAt(Pull.StartTime)),
            effective,
            overheal);
    }

    private sealed class HoldingCapture
    {
        public int Ticks { get; set; }
        public long Effective { get; set; }
        public long Overheal { get; set; }
    }

    private sealed record Computed(
        IReadOnlyList<FlutterflyHolding> Holdings,
        long AssignedMs,
        int PeakBanked,
        long Effective,
        long Overheal);
}

public sealed record FlutterflyHolding(
    UnitKey Unit,
    long AssignedMs,
    int Assignments,
    int Ticks,
    long Effective,
    long Overheal)
{
    public long TotalHealing => Effective + Overheal;

    public double OverhealShare => TotalHealing > 0 ? Overheal / (double)TotalHealing : 0;
}
