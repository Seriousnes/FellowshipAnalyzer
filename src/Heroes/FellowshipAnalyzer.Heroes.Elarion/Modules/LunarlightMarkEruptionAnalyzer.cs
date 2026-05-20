using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Tracks Lunarlight Mark application and eruption. Heartseeker Barrage is the primary mark
/// eruption tool; if it lands without any active marks, the eruption is wasted. Marks that
/// expire before a Barrage is cast are also wasted opportunities.
/// </summary>
public sealed partial class LunarlightMarkEruptionAnalyzer : Analyzer
{
    private const string LunarlightMarkName = "Lunarlight Mark";

    private readonly HashSet<(int TargetId, int? Instance)> _activeMarks = [];
    private readonly List<BarrageEvent> _barrages = [];
    private int _totalMarksApplied;
    private int _marksExpired;

    public IReadOnlyList<BarrageEvent> Barrages => _barrages;
    public int TotalMarksApplied => _totalMarksApplied;
    public int MarksExpired => _marksExpired;
    public int BarrageWithEruption => _barrages.Count(b => b.ErupedMarks > 0);
    public int BarrageWithoutEruption => _barrages.Count(b => b.ErupedMarks == 0);

    public int ActiveMarkCount => _activeMarks.Count;

    [On<ApplyDebuffEvent>(By = Actor.Player)]
    private void OnApplyMark(ApplyDebuffEvent e)
    {
        if (!IsLunarlightMark(e.Ability)) return;

        _activeMarks.Add((e.TargetId, e.TargetInstance));
        _totalMarksApplied++;
    }

    [On<RefreshDebuffEvent>(By = Actor.Player)]
    private void OnRefreshMark(RefreshDebuffEvent e)
    {
        if (!IsLunarlightMark(e.Ability)) return;

        _activeMarks.Add((e.TargetId, e.TargetInstance));
        _totalMarksApplied++;
    }

    [On<RemoveDebuffEvent>(By = Actor.Player)]
    private void OnRemoveMark(RemoveDebuffEvent e)
    {
        if (!IsLunarlightMark(e.Ability)) return;

        if (_activeMarks.Remove((e.TargetId, e.TargetInstance)) && !_recentlyErupted)
        {
            _marksExpired++;
        }
    }

    private bool _recentlyErupted;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.HeartseekerBarrage))]
    private void OnBarrage(CastEvent e)
    {
        var erupted = _activeMarks.Count;
        _barrages.Add(new BarrageEvent(e.Timestamp, erupted));
        _activeMarks.Clear();
        _recentlyErupted = true;
    }

    [On<CastEvent>(By = Actor.Player)]
    private void OnAnyCast(CastEvent e)
    {
        if (e.Ability.Id != Spells.HeartseekerBarrage.Guid)
        {
            _recentlyErupted = false;
        }
    }

    private static bool IsLunarlightMark(Ability ability) =>
        ability.Id == Spells.LunarlightMark.Guid ||
        string.Equals(ability.Name, LunarlightMarkName, StringComparison.Ordinal);

    public readonly record struct BarrageEvent(int Timestamp, int ErupedMarks);
}
