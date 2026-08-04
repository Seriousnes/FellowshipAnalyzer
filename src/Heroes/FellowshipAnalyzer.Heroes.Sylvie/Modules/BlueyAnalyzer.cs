using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>
/// Where Bluey sat through this pull, and what that placement was worth. Parked on an ally it doubles
/// that ally's flutterfly healing; recalled onto Sylvie it is worth half as much but discounts her
/// mana, so the two placements are counted separately rather than scored against each other.
/// <para>
/// Flutterfly healing observed on whoever carries Bluey already includes the multiplier, so the uplift
/// is the share of that healing the multiplier accounts for: <c>amount * (1 - 1 / scaler)</c>, half of
/// an ally's tick and a third of Sylvie's. Only effective healing counts, because a doubled tick that
/// falls on full health delivered nothing.
/// </para>
/// <para>
/// The postings come from the fight-lifetime <see cref="BlueyTracker"/> and are clipped to the pull.
/// Bluey routinely sits still for many pulls at a time, so a pull that contains no reassignment still
/// has a placement to report.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<BlueyTracker>]
[Dependency<SylvieManaTracker>]
public sealed partial class BlueyAnalyzer : Analyzer
{
    private double _upliftOnAlly;
    private double _upliftOnSylvie;

    private IReadOnlyList<(int TargetId, int Ms, bool OnSylvie)> Result =>
        field ??= BlueyTracker.TimeByHolderBetween(Pull.StartTime, Pull.EndTime);

    /// <summary>Every unit Bluey sat on this pull, with the time it spent there, longest first.</summary>
    public IReadOnlyList<(int TargetId, int Ms, bool OnSylvie)> TimeByHolder => Result;

    /// <summary>Milliseconds Bluey spent parked on somebody other than Sylvie.</summary>
    public int OnAllyMs => Result.Where(entry => !entry.OnSylvie).Sum(entry => entry.Ms);

    /// <summary>Milliseconds Bluey spent recalled onto Sylvie herself.</summary>
    public int OnSylvieMs => Result.Where(entry => entry.OnSylvie).Sum(entry => entry.Ms);

    /// <summary>Milliseconds Bluey was nowhere the log could place it.</summary>
    public int UnplacedMs => Math.Max(0, PullDurationMs - OnAllyMs - OnSylvieMs);

    /// <summary>Share (0-1) of the pull Bluey spent on an ally.</summary>
    public double OnAllyShare => PullDurationMs > 0 ? OnAllyMs / (double)PullDurationMs : 0;

    /// <summary>Share (0-1) of the pull Bluey spent on Sylvie.</summary>
    public double OnSylvieShare => PullDurationMs > 0 ? OnSylvieMs / (double)PullDurationMs : 0;

    /// <summary>Distinct units Bluey sat on this pull.</summary>
    public int Holders => Result.Count;

    /// <summary>Casts that moved Bluey during this pull.</summary>
    public int Reassignments => BlueyTracker.Postings
        .Count(posting => posting.Start > Pull.StartTime && posting.Start <= Pull.EndTime);

    /// <summary>The pull's length in milliseconds.</summary>
    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    /// <summary>Flutterfly healing this pull that Bluey's multiplier accounts for while it sat on an ally.</summary>
    public long HealingUpliftOnAlly => (long)Math.Round(_upliftOnAlly);

    /// <summary>Flutterfly healing this pull that Bluey's multiplier accounts for while it was recalled onto Sylvie.</summary>
    public long HealingUpliftOnSylvie => (long)Math.Round(_upliftOnSylvie);

    /// <summary>Flutterfly healing this pull that Bluey's multiplier accounts for, wherever it sat.</summary>
    public long HealingUplift => (long)Math.Round(_upliftOnAlly + _upliftOnSylvie);

    /// <summary>Flutterfly ticks this pull that landed on whoever was carrying Bluey.</summary>
    public int UpliftedTicks { get; private set; }

    /// <summary>Mana the Bluey discount took off Sylvie's casts this pull.</summary>
    public int ManaSaved => SylvieManaTracker.SavedBetween(Pull.StartTime, Pull.EndTime);

    [On<HealEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.FluttercallHealHot),
        nameof(Spells.FluttercallRestoreLifeHot),
    })]
    private void OnFlutterflyHeal(HealEvent healEvent)
    {
        if (BlueyTracker.PostingAt(healEvent.Timestamp) is not { } posting) return;
        if (posting.TargetId != healEvent.TargetId) return;

        UpliftedTicks++;

        if (posting.OnSylvie)
            _upliftOnSylvie += healEvent.Amount * (1 - (1 / SylvieKit.BlueySelfHealScaler));
        else
            _upliftOnAlly += healEvent.Amount * (1 - (1 / SylvieKit.BlueyAllyHealScaler));
    }
}
