using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Tracks Lunarlight Mark application and eruption within a pull. Heartseeker Barrage is the primary
/// mark eruption tool; if it lands without any active marks, the eruption is wasted. Marks that
/// expire before a Barrage is cast are also wasted opportunities. Both builds open with Lunarlight
/// Mark, so this runs on every pull shape; a pull with no Barrage casts indicates the player is on
/// the Highwind Arrow build.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class LunarlightMarkEruptionAnalyzer : Analyzer
{
    private const string LunarlightMarkName = "Lunarlight Mark";

    private readonly HashSet<(int TargetId, int? Instance)> _activeMarks = [];
    private readonly List<BarrageEvent> _barrages = [];
    private bool _recentlyErupted;

    public IReadOnlyList<BarrageEvent> Barrages => _barrages;
    public int TotalMarksApplied { get; private set; }
    public int MarksExpired { get; private set; }
    public int BarrageWithEruption { get; private set; }
    public int BarrageWithoutEruption { get; private set; }

    [On<ApplyDebuffEvent>(By = Actor.Player)]
    private void OnApplyMark(ApplyDebuffEvent e)
    {
        if (!IsLunarlightMark(e.Ability)) return;

        _activeMarks.Add((e.TargetId, e.TargetInstance));
        TotalMarksApplied++;
    }

    [On<RefreshDebuffEvent>(By = Actor.Player)]
    private void OnRefreshMark(RefreshDebuffEvent e)
    {
        if (!IsLunarlightMark(e.Ability)) return;

        _activeMarks.Add((e.TargetId, e.TargetInstance));
        TotalMarksApplied++;
    }

    [On<RemoveDebuffEvent>(By = Actor.Player)]
    private void OnRemoveMark(RemoveDebuffEvent e)
    {
        if (!IsLunarlightMark(e.Ability)) return;

        if (_activeMarks.Remove((e.TargetId, e.TargetInstance)) && !_recentlyErupted)
        {
            MarksExpired++;
        }
    }

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
        if (e.Ability.Id != Spells.HeartseekerBarrage.FSLID)
        {
            _recentlyErupted = false;
        }
    }

    public override void OnPullEnd()
    {
        BarrageWithEruption = _barrages.Count(b => b.ErupedMarks > 0);
        BarrageWithoutEruption = _barrages.Count(b => b.ErupedMarks == 0);
    }

    private static bool IsLunarlightMark(Ability ability) =>
        ability.Id == Spells.LunarlightMark.FSLID ||
        string.Equals(ability.Name, LunarlightMarkName, StringComparison.Ordinal);

    public readonly record struct BarrageEvent(int Timestamp, int ErupedMarks);
}
