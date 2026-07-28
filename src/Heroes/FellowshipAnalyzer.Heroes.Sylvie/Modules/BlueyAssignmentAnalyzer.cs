using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>
/// Where Bluey sat through this pull. Parked on an ally it doubles that ally's flutterfly healing;
/// recalled onto Sylvie it is worth half as much but discounts her mana, so the two placements are
/// counted separately rather than scored against each other.
/// <para>
/// The postings come from the fight-lifetime <see cref="BlueyTracker"/> and are clipped to the pull.
/// Bluey routinely sits still for many pulls at a time, so a pull that contains no reassignment still
/// has a placement to report.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<BlueyTracker>]
public sealed partial class BlueyAssignmentAnalyzer : Analyzer
{
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
}
