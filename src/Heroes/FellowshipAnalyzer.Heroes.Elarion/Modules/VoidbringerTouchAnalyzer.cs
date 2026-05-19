using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Elarion.Statistics;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Tracks Voidbringer's Touch casts. The guide notes the player should send the first cast
/// ASAP and then save the next so the cadence becomes void → ult → void. Casting on the same
/// target before the first mark is consumed (double-mark) is a misuse.
/// </summary>
public sealed partial class VoidbringerTouchAnalyzer : Analyzer
{
    private const int DoubleMarkWindowMs = 8000;

    private readonly List<VoidbringerCast> _casts = [];

    public IReadOnlyList<VoidbringerCast> Casts => _casts;
    public int TotalCasts => _casts.Count;
    public int DoubleMarks => _casts.Count(c => c.IsDoubleMark);

    public override Type? StatisticsComponentType => typeof(VoidbringerTouchStatistics);

    [On<CastEvent>(By = Actor.Player, Spell = SpellIds.VoidbringerTouch)]
    private void OnCast(CastEvent e)
    {
        var doubleMark = _casts.Any(prior =>
            prior.TargetId == e.TargetId &&
            e.Timestamp - prior.Timestamp < DoubleMarkWindowMs);

        _casts.Add(new VoidbringerCast(e.Timestamp, e.TargetId, doubleMark));
    }

    public readonly record struct VoidbringerCast(int Timestamp, int TargetId, bool IsDoubleMark);
}
