using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Measures how widely Gunde seeded Rend across a multi-target pull. Blood Feather generation and
/// Slaughter's payoff both scale with how many enemies carry the bleed, so an unseeded enemy is
/// lost damage twice over. Each enemy that took Rend from the player is counted once by
/// (TargetId, TargetInstance); coverage is that distinct count against the pull's enemy roster
/// (<see cref="Pull.TargetCount"/>). Applies and refreshes both mark a target debuffed and the
/// HashSet dedupes, so a refresh never counts as a new enemy. Counting is monotonic, so enemies
/// that die carrying Rend (which never log a remove) do not drift the metric.
/// </summary>
[ForPull(PullKind.Multi)]
public sealed partial class RendSpreadAnalyzer : Analyzer, IRendAnalyzer
{
    private const int UnknownRosterReference = 3;

    private readonly HashSet<(int TargetId, int TargetInstance)> _debuffedTargets = [];

    /// <summary>Distinct enemies that took Rend from the player.</summary>
    public int DistinctTargets => _debuffedTargets.Count;

    /// <summary>The pull's reported enemy roster size; zero when the roster was not reported.</summary>
    public int TargetCount => Pull.TargetCount;

    /// <summary>
    /// Share of the roster covered (0-1). When the roster is unknown, coverage is estimated against
    /// a reference pack size, so it stays comparable across pulls.
    /// </summary>
    public double Coverage
    {
        get
        {
            var denominator = TargetCount > 0 ? TargetCount : Math.Max(DistinctTargets, UnknownRosterReference);
            return denominator == 0 ? 0d : Math.Min(1d, DistinctTargets / (double)denominator);
        }
    }

    /// <summary>Fresh Rend applications during the pull (refreshes excluded).</summary>
    public int TotalApplications { get; private set; }

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.Rend))]
    private void OnApplied(ApplyDebuffEvent e)
    {
        _debuffedTargets.Add((e.TargetId, e.TargetInstance ?? 0));
        TotalApplications++;
    }

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.Rend))]
    private void OnRefreshed(RefreshDebuffEvent e)
        => _debuffedTargets.Add((e.TargetId, e.TargetInstance ?? 0));
}
