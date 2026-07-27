using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Judges each Heart Splitter on the Rend it had to work with. Its Exsanguinate half deals a share
/// of the whole bleed standing on the target and leaves that bleed in place, so Heart Splitter wants
/// to land while Rend is deep - which means before Slaughter consumes it, never just after. Pressing
/// Slaughter with Heart Splitter about to come up empties the target and the Exsanguinate that
/// follows cashes almost nothing; that is the mistake this names, cast by cast.
/// </summary>
/// <remarks>
/// <para>
/// The Rend standing on the target is read from <see cref="RendStackTracker"/> at the instant of the
/// cast, which is a stack count and so is the same measure on any gear. The Exsanguinate damage that
/// follows is recorded alongside it as evidence rather than as a verdict, because that figure scales
/// with gear while the stack count does not. Live Season 3 data ties the two together tightly: 8
/// stacks paid 1.2k, 230 stacks paid 32k.
/// </para>
/// <para>
/// Heart Splitter carries a target on its cast event, so the pile it exsanguinated is read for that
/// enemy specifically rather than across the pack.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
[Uses<RendStackTracker>]
public sealed partial class HeartSplitterAnalyzer : Analyzer
{
    /// <summary>
    /// How recently a Slaughter must have emptied the target for this Heart Splitter to count as
    /// clipped by it. Two hasted global cooldowns, which live Season 3 data puts a little under a
    /// second each, so a Slaughter this close was pressed with Heart Splitter already in hand.
    /// </summary>
    public const int SlaughterClipMs = 2_500;

    private readonly List<HeartSplitterCast> _casts = [];

    private int? _lastSlaughter;

    /// <summary>Every Heart Splitter on the pull, in cast order, with the Rend it found.</summary>
    public IReadOnlyList<HeartSplitterCast> Casts => _casts;

    /// <summary>Heart Splitters cast on a target a Slaughter had just emptied.</summary>
    public int ClippedBySlaughter => _casts.Count(cast => cast.ClippedBySlaughter);

    /// <summary>Rend standing across every Heart Splitter's target at the moment it was cast.</summary>
    public int TotalRendExsanguinated => _casts.Sum(cast => cast.RendOnTarget);

    /// <summary>Mean Rend on target per cast, or zero when none were cast.</summary>
    public double AverageRendOnTarget => _casts.Count > 0 ? (double)TotalRendExsanguinated / _casts.Count : 0d;

    /// <summary>Exsanguinate damage across every Heart Splitter on the pull.</summary>
    public long TotalExsanguinateDamage => _casts.Sum(cast => cast.ExsanguinateDamage);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Slaughter))]
    private void OnSlaughter(CastEvent castEvent) => _lastSlaughter = castEvent.Timestamp;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.HeartSplitter))]
    private void OnHeartSplitter(CastEvent castEvent) =>
        _casts.Add(new HeartSplitterCast(
            castEvent.Timestamp,
            castEvent.TargetId,
            RendStackTracker.StacksOn(castEvent.TargetId, castEvent.TargetInstance),
            _lastSlaughter is { } slaughter ? castEvent.Timestamp - slaughter : null));

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.HeartSplitterDotBonusDamage))]
    private void OnExsanguinate(DamageEvent damageEvent)
    {
        if (_casts.Count > 0)
            _casts[^1].ExsanguinateDamage += damageEvent.Amount;
    }
}

/// <summary>One Heart Splitter and the bleed it found on its target.</summary>
public sealed class HeartSplitterCast(int timestamp, int targetId, int rendOnTarget, int? millisecondsSinceSlaughter)
{
    /// <summary>Encounter time of the cast.</summary>
    public int Timestamp { get; } = timestamp;

    /// <summary>The enemy the cast named.</summary>
    public int TargetId { get; } = targetId;

    /// <summary>Rend standing on that enemy when the cast went out.</summary>
    public int RendOnTarget { get; } = rendOnTarget;

    /// <summary>
    /// Time since the last Slaughter of the pull, or null when none had been cast yet, in which case
    /// nothing had consumed Rend and the cast cannot have been clipped.
    /// </summary>
    public int? MillisecondsSinceSlaughter { get; } = millisecondsSinceSlaughter;

    /// <summary>Exsanguinate damage this cast went on to deal.</summary>
    public long ExsanguinateDamage { get; internal set; }

    /// <summary>Whether a Slaughter emptied the target within <see cref="HeartSplitterAnalyzer.SlaughterClipMs"/>.</summary>
    public bool ClippedBySlaughter =>
        MillisecondsSinceSlaughter is { } gap && gap <= HeartSplitterAnalyzer.SlaughterClipMs;
}
