using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

[ForPull(PullKind.Multi)]
public sealed partial class RendSpreadAnalyzer : Analyzer, IRendAnalyzer
{
    private const int UnknownRosterReference = 3;

    private readonly HashSet<(int TargetId, int TargetInstance)> _debuffedTargets = [];

    public int DistinctTargets => _debuffedTargets.Count;

    public int TargetCount => Pull.TargetCount;

    public double Coverage
    {
        get
        {
            var denominator = TargetCount > 0 ? TargetCount : Math.Max(DistinctTargets, UnknownRosterReference);
            return denominator == 0 ? 0d : Math.Min(1d, DistinctTargets / (double)denominator);
        }
    }

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
