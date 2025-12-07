using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks ability casts during event processing.
/// Resource tracking is handled by <see cref="ResourceTracker"/>.
/// </summary>
public sealed class TrackedStateModule : EventSubscriber
{
    private readonly List<TrackedAbilityCast> _casts = [];

    public override void Initialize()
    {
        AddEventListener(Events.Cast.By(Analyzer.SELECTED_PLAYER), OnCast);
    }

    public IReadOnlyList<TrackedAbilityCast> Casts => _casts;

    private void OnCast(CastEvent castEvent)
    {
        _casts.Add(new TrackedAbilityCast(castEvent.Timestamp, castEvent.AbilityGameId, castEvent.TargetId));
    }
}

public sealed record TrackedAbilityCast(
    int Timestamp,
    int AbilityId,
    int TargetId);