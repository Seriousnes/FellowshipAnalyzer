using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Tracks the time gap between Lunarlight Mark and Starfall Volley casts within a pull. Casting them
/// too close together (within ~5s) wastes the window where marks could be re-applied and erupted
/// between Volley casts. The guide recommends staggering by 10-15 seconds. Both builds open with
/// Lunarlight Mark into Starfall Volley, so this runs on every pull shape.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class StarfallVolleyDesyncAnalyzer : Analyzer
{
    private const int CloseGapThresholdMs = 5000;

    private int? _lastLunarlightMarkTimestamp;
    private readonly List<DesyncEvent> _volleys = [];

    public IReadOnlyList<DesyncEvent> Volleys => _volleys;
    public int CloseGapCount { get; private set; }
    public int VolleyCount => _volleys.Count;
    public double AverageGapMs { get; private set; }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.LunarlightMark))]
    private void OnLunarlightMark(CastEvent e)
    {
        _lastLunarlightMarkTimestamp = e.Timestamp;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.StarfallVolley))]
    private void OnStarfallVolley(CastEvent e)
    {
        if (_lastLunarlightMarkTimestamp is int last)
        {
            _volleys.Add(new DesyncEvent(e.Timestamp, e.Timestamp - last));
        }
        else
        {
            _volleys.Add(new DesyncEvent(e.Timestamp, GapMs: int.MaxValue));
        }
    }

    public override void OnPullEnd()
    {
        CloseGapCount = _volleys.Count(e => e.GapMs < CloseGapThresholdMs);
        var measuredGaps = _volleys.Where(e => e.GapMs != int.MaxValue).Select(e => e.GapMs).ToList();
        AverageGapMs = measuredGaps.Count == 0 ? 0 : measuredGaps.Average();
    }

    public readonly record struct DesyncEvent(int VolleyTimestamp, int GapMs);
}
