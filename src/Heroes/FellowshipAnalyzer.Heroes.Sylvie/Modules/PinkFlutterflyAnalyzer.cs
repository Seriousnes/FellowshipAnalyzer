using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>
/// Where the four pink Flutterflies were during this pull and what each one healed. A Flutterfly is
/// worth nothing while it sits in the bank, so the reading is Flutterfly-time parked on an ally against
/// the four-Flutterfly ceiling the game sets - an absolute, not a comparison with another pull.
/// <para>
/// Both placing abilities count. <see cref="Spells.FluttercallRestoreLife"/> heals under its own effect
/// and closes on its own removal event, and its share of the pull is reported apart from
/// <see cref="Spells.FluttercallHeal"/>'s singles by the <c>RestoreLife</c> members below. Its single
/// aura holds a pair, so it contributes <see cref="SylvieKit.RestoreLifeFlutterflies"/> Flutterfly-
/// milliseconds per elapsed millisecond against the same four-Flutterfly ceiling.
/// </para>
/// <para>
/// The assignments themselves come from <see cref="PinkFlutterflyTracker"/>, which spans the whole
/// fight; this clips them to the pull and attributes the pull's own ticks. A Flutterfly parked before
/// the pull started emits no application event inside it, which is exactly why the windows cannot be
/// rebuilt from this pull's events alone.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<PinkFlutterflyTracker>]
public sealed partial class PinkFlutterflyAnalyzer : Analyzer
{
    private readonly Dictionary<UnitKey, HoldingCapture> _byHolder = [];

    private Computed Result => field ??= Compute();

    /// <summary>Every ally that held a Flutterfly this pull, most Flutterfly-time first.</summary>
    public IReadOnlyList<FlutterflyHolding> Holdings => Result.Holdings;

    /// <summary>
    /// Flutterfly-milliseconds parked on an ally this pull, against <see cref="AssignableMs"/>. A
    /// Restore Life assignment counts double, because its single aura holds a pair.
    /// </summary>
    public long AssignedMs => Result.AssignedMs;

    /// <summary>Flutterfly-milliseconds the pull offered: four Flutterflies for its whole length.</summary>
    public long AssignableMs => (long)SylvieKit.PinkFlutterflies * Math.Max(0, Pull.EndTime - Pull.StartTime);

    /// <summary>Share (0-1) of the pull's Flutterfly-time that was parked on an ally rather than banked.</summary>
    public double AssignedShare => AssignableMs > 0 ? Math.Min(1d, AssignedMs / (double)AssignableMs) : 0;

    /// <summary>Flutterflies that were moved onto a new ally during this pull.</summary>
    public int AssignmentsOpened =>
        PinkFlutterflyTracker.Flutterflies.Count(Flutterfly => Flutterfly.Start >= Pull.StartTime && Flutterfly.Start <= Pull.EndTime);

    /// <summary>Flutterflies that came off an ally during this pull.</summary>
    public int AssignmentsClosed =>
        PinkFlutterflyTracker.Flutterflies.Count(Flutterfly => Flutterfly.End >= Pull.StartTime && Flutterfly.End <= Pull.EndTime);

    /// <summary>The most Flutterflies observed sitting in the bank at once during this pull.</summary>
    public int PeakBanked => Result.PeakBanked;

    /// <summary>Flutterfly healing that landed this pull.</summary>
    public long Effective => Result.Effective;

    /// <summary>Flutterfly healing lost to full health this pull.</summary>
    public long Overheal => Result.Overheal;

    /// <summary>Allies who held no Flutterfly at any point in the pull, out of everyone the Flutterflies healed.</summary>
    public int HoldersCovered => Result.Holdings.Count;

    /// <summary>Restore Life windows that opened during this pull.</summary>
    public int RestoreLifeAssignmentsOpened =>
        PinkFlutterflyTracker.RestoreLifeAssignments
            .Count(Flutterfly => Flutterfly.Start >= Pull.StartTime && Flutterfly.Start <= Pull.EndTime);

    /// <summary>
    /// Flutterfly-milliseconds this pull that came from a Restore Life window rather than a Heal,
    /// counting the pair, so a fifteen second window reads thirty Flutterfly-seconds.
    /// </summary>
    public long RestoreLifeAssignedMs =>
        PinkFlutterflyTracker.AssignedMsBetween(Pull.StartTime, Pull.EndTime, FlutterflyPlacement.RestoreLife);

    /// <summary>Restore Life ticks that landed this pull.</summary>
    public int RestoreLifeTicks { get; private set; }

    /// <summary>Restore Life healing that landed this pull, already counted inside <see cref="Effective"/>.</summary>
    public long RestoreLifeEffective { get; private set; }

    /// <summary>Restore Life healing lost to full health this pull, already counted inside <see cref="Overheal"/>.</summary>
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

/// <summary>One ally's Flutterfly time and Flutterfly healing over a pull.</summary>
/// <param name="Unit">The ally.</param>
/// <param name="AssignedMs">Flutterfly-milliseconds parked on it this pull.</param>
/// <param name="Assignments">Distinct Flutterflies that sat on it this pull.</param>
/// <param name="Ticks">Flutterfly heal ticks it received.</param>
/// <param name="Effective">Flutterfly healing that landed on it.</param>
/// <param name="Overheal">Flutterfly healing lost to that ally already being at full health.</param>
public sealed record FlutterflyHolding(
    UnitKey Unit,
    long AssignedMs,
    int Assignments,
    int Ticks,
    long Effective,
    long Overheal)
{
    /// <summary>Effective healing plus overheal.</summary>
    public long TotalHealing => Effective + Overheal;

    /// <summary>Share (0-1) of this ally's Flutterfly healing that was overheal.</summary>
    public double OverhealShare => TotalHealing > 0 ? Overheal / (double)TotalHealing : 0;
}
