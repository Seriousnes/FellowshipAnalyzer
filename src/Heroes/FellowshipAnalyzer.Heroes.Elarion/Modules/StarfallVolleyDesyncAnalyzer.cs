using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Elarion.Statistics;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Tracks the time gap between Lunarlight Mark and Starfall Volley casts. Casting them too
/// close together (within ~5s) wastes the window where marks could be re-applied and erupted
/// between Volley casts. The guide recommends staggering by 10-15 seconds.
/// </summary>
public sealed partial class StarfallVolleyDesyncAnalyzer : Analyzer
{
    private const int CloseGapThresholdMs = 5000;

    private int? _lastLunarlightMarkTimestamp;
    private readonly List<DesyncEvent> _events = [];

    public IReadOnlyList<DesyncEvent> Events => _events;
    public int CloseGapCount => _events.Count(e => e.GapMs < CloseGapThresholdMs);
    public int VolleyCount => _events.Count;

    public double AverageGapMs => _events.Count == 0
        ? 0
        : _events.Average(e => e.GapMs);

    public override Type? StatisticsComponentType => typeof(StarfallVolleyDesyncStatistics);

    [On<CastEvent>(By = Actor.Player, Spell = SpellIds.LunarlightMark)]
    private void OnLunarlightMark(CastEvent e)
    {
        _lastLunarlightMarkTimestamp = e.Timestamp;
    }

    [On<CastEvent>(By = Actor.Player, Spell = SpellIds.StarfallVolley)]
    private void OnStarfallVolley(CastEvent e)
    {
        if (_lastLunarlightMarkTimestamp is int last)
        {
            _events.Add(new DesyncEvent(e.Timestamp, e.Timestamp - last));
        }
        else
        {
            _events.Add(new DesyncEvent(e.Timestamp, GapMs: int.MaxValue));
        }
    }

    public readonly record struct DesyncEvent(int VolleyTimestamp, int GapMs);
}
