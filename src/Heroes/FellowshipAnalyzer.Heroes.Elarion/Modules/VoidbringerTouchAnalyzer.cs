using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

using Items = FellowshipAnalyzer.Core.Common.Spells.Items;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Tracks Voidbringer's Touch casts within a pull. The guide notes the player should send the first
/// cast ASAP and then save the next so the cadence becomes void → ult → void. Casting on the same
/// target before the first mark is consumed (double-mark) is a misuse. Voidbringer's Touch is a
/// trinket used regardless of pull shape, so this runs on every pull.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class VoidbringerTouchAnalyzer : Analyzer<VoidbringerTouchReport>
{
    private const int DoubleMarkWindowMs = 8000;

    private readonly List<VoidbringerCast> _casts = [];

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Items.VoidbringerTouch))]
    private void OnCast(CastEvent e)
    {
        var doubleMark = _casts.Any(prior =>
            prior.TargetId == e.TargetId &&
            e.Timestamp - prior.Timestamp < DoubleMarkWindowMs);

        _casts.Add(new VoidbringerCast(e.Timestamp, e.TargetId, doubleMark));
    }

    public override VoidbringerTouchReport OnPullEnd() =>
        new(_casts, _casts.Count, _casts.Count(c => c.IsDoubleMark));

    public readonly record struct VoidbringerCast(int Timestamp, int TargetId, bool IsDoubleMark);
}
