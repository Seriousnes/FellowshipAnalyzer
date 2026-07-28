using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>
/// Where the four pink butterflies were during this pull and what each one healed. A butterfly is
/// worth nothing while it sits in the bank, so the reading is butterfly-time parked on an ally against
/// the four-butterfly ceiling the game sets - an absolute, not a comparison with another pull.
/// <para>
/// The assignments themselves come from <see cref="PinkButterflyTracker"/>, which spans the whole
/// fight; this clips them to the pull and attributes the pull's own ticks. A butterfly parked before
/// the pull started emits no application event inside it, which is exactly why the windows cannot be
/// rebuilt from this pull's events alone.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<PinkButterflyTracker>]
public sealed partial class PinkButterflyAssignmentAnalyzer : Analyzer
{
    private readonly Dictionary<UnitKey, HoldingCapture> _byHolder = [];

    private Computed Result => field ??= Compute();

    /// <summary>Every ally that held a butterfly this pull, most butterfly-time first.</summary>
    public IReadOnlyList<ButterflyHolding> Holdings => Result.Holdings;

    /// <summary>Butterfly-milliseconds parked on an ally this pull, against <see cref="AssignableMs"/>.</summary>
    public long AssignedMs => Result.AssignedMs;

    /// <summary>Butterfly-milliseconds the pull offered: four butterflies for its whole length.</summary>
    public long AssignableMs => (long)SylvieKit.PinkButterflies * Math.Max(0, Pull.EndTime - Pull.StartTime);

    /// <summary>Share (0-1) of the pull's butterfly-time that was parked on an ally rather than banked.</summary>
    public double AssignedShare => AssignableMs > 0 ? Math.Min(1d, AssignedMs / (double)AssignableMs) : 0;

    /// <summary>Butterflies that were moved onto a new ally during this pull.</summary>
    public int AssignmentsOpened =>
        PinkButterflyTracker.Butterflies.Count(butterfly => butterfly.Start >= Pull.StartTime && butterfly.Start <= Pull.EndTime);

    /// <summary>Butterflies that came off an ally during this pull.</summary>
    public int AssignmentsClosed =>
        PinkButterflyTracker.Butterflies.Count(butterfly => butterfly.End >= Pull.StartTime && butterfly.End <= Pull.EndTime);

    /// <summary>The most butterflies observed sitting in the bank at once during this pull.</summary>
    public int PeakBanked => Result.PeakBanked;

    /// <summary>Butterfly healing that landed this pull.</summary>
    public long Effective => Result.Effective;

    /// <summary>Butterfly healing lost to full health bars this pull.</summary>
    public long Overheal => Result.Overheal;

    /// <summary>Allies who held no butterfly at any point in the pull, out of everyone the butterflies healed.</summary>
    public int HoldersCovered => Result.Holdings.Count;

    [On<HealEvent>(By = Actor.Player, Spell = nameof(Core.Common.Spells.Sylvie.Spells.FluttercallHealHot))]
    private void OnButterflyTick(HealEvent healEvent)
    {
        var unit = new UnitKey(healEvent.TargetId, healEvent.TargetInstance ?? 0);
        if (!_byHolder.TryGetValue(unit, out var capture))
            _byHolder[unit] = capture = new HoldingCapture();

        capture.Ticks++;
        capture.Effective += healEvent.Amount;
        capture.Overheal += healEvent.Overheal ?? 0;
    }

    private Computed Compute()
    {
        var assignedByUnit = PinkButterflyTracker
            .HoldersBetween(Pull.StartTime, Pull.EndTime)
            .ToDictionary(holder => holder.Unit);

        long effective = 0, overheal = 0;
        var holdings = new List<ButterflyHolding>(_byHolder.Count);

        foreach (var (unit, capture) in _byHolder)
        {
            effective += capture.Effective;
            overheal += capture.Overheal;

            var held = assignedByUnit.GetValueOrDefault(unit);
            holdings.Add(new ButterflyHolding(
                unit,
                held?.AssignedMs ?? 0,
                held?.Assignments ?? 0,
                capture.Ticks,
                capture.Effective,
                capture.Overheal));
        }

        holdings.Sort(static (left, right) => right.AssignedMs.CompareTo(left.AssignedMs));

        var banked = 0;
        foreach (var sample in PinkButterflyTracker.BankSamples)
        {
            if (sample.Timestamp < Pull.StartTime || sample.Timestamp > Pull.EndTime) continue;
            banked = Math.Max(banked, sample.Count);
        }

        return new Computed(
            holdings,
            PinkButterflyTracker.AssignedMsBetween(Pull.StartTime, Pull.EndTime),
            Math.Max(banked, PinkButterflyTracker.BankedAt(Pull.StartTime)),
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
        IReadOnlyList<ButterflyHolding> Holdings,
        long AssignedMs,
        int PeakBanked,
        long Effective,
        long Overheal);
}

/// <summary>One ally's butterfly time and butterfly healing over a pull.</summary>
/// <param name="Unit">The ally.</param>
/// <param name="AssignedMs">Butterfly-milliseconds parked on it this pull.</param>
/// <param name="Assignments">Distinct butterflies that sat on it this pull.</param>
/// <param name="Ticks">Butterfly heal ticks it received.</param>
/// <param name="Effective">Butterfly healing that landed on it.</param>
/// <param name="Overheal">Butterfly healing lost to its full health bar.</param>
public sealed record ButterflyHolding(
    UnitKey Unit,
    long AssignedMs,
    int Assignments,
    int Ticks,
    long Effective,
    long Overheal)
{
    /// <summary>Effective healing plus overheal.</summary>
    public long TotalHealing => Effective + Overheal;

    /// <summary>Share (0-1) of this ally's butterfly healing that was overheal.</summary>
    public double OverhealShare => TotalHealing > 0 ? Overheal / (double)TotalHealing : 0;
}
