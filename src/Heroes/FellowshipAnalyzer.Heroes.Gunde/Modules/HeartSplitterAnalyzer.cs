using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<RendStackTracker>]
public sealed partial class HeartSplitterAnalyzer : Analyzer
{
    public const int SlaughterClipMs = 2_500;

    private readonly List<HeartSplitterCast> _casts = [];

    private int? _lastSlaughter;

    public IReadOnlyList<HeartSplitterCast> Casts => _casts;

    public int ClippedBySlaughter => _casts.Count(cast => cast.ClippedBySlaughter);

    public int MeasuredCasts => _casts.Count(cast => cast.RendOnTarget is not null);

    public int TotalRendExsanguinated => _casts.Sum(cast => cast.RendOnTarget ?? 0);

    public double AverageRendOnTarget =>
        MeasuredCasts > 0 ? (double)TotalRendExsanguinated / MeasuredCasts : 0d;

    public long TotalExsanguinateDamage => _casts.Sum(cast => cast.ExsanguinateDamage);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Slaughter))]
    private void OnSlaughter(CastEvent castEvent) => _lastSlaughter = castEvent.Timestamp;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.HeartSplitter))]
    private void OnHeartSplitter(CastEvent castEvent) =>
        _casts.Add(new HeartSplitterCast(
            castEvent.Timestamp,
            castEvent.TargetId,
            _lastSlaughter is { } slaughter ? castEvent.Timestamp - slaughter : null));

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.HeartSplitterDotBonusDamage))]
    private void OnExsanguinate(DamageEvent damageEvent)
    {
        if (_casts.Count == 0) return;

        var cast = _casts[^1];
        cast.ExsanguinateDamage += damageEvent.Amount;
        cast.RendOnTarget ??= RendStackTracker.StacksOn(damageEvent.TargetId, damageEvent.TargetInstance);
    }
}

public sealed class HeartSplitterCast(int timestamp, int targetId, int? millisecondsSinceSlaughter)
{
    public int Timestamp { get; } = timestamp;

    public int TargetId { get; } = targetId;

    public int? RendOnTarget { get; internal set; }

    public int? MillisecondsSinceSlaughter { get; } = millisecondsSinceSlaughter;

    public long ExsanguinateDamage { get; internal set; }

    public bool ClippedBySlaughter =>
        MillisecondsSinceSlaughter is { } gap && gap <= HeartSplitterAnalyzer.SlaughterClipMs;
}
