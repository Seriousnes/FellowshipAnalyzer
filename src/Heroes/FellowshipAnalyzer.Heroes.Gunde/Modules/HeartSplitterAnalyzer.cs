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
/// The pile is read from <see cref="RendStackTracker"/> at the Exsanguinate hit rather than at the
/// cast, because that is the instant the game takes its share and Rend keeps accruing over the third
/// of a second between the two. Reading at the cast is demonstrably the wrong moment: on live Season
/// 3 data it produces anything from 123 to 1195 damage per stack, while reading at the hit lands
/// every cast of the fight between 120 and 149. It is a stack count, so it is the same measure on
/// any gear; the Exsanguinate damage is recorded alongside as evidence rather than as a verdict,
/// because that figure does scale with gear.
/// </para>
/// <para>
/// The pile is read for the enemy the Exsanguinate actually hit rather than across the pack. A talent
/// can spread the strike, in which case the first enemy hit carries the reading and the damage sums
/// across all of them.
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

    /// <summary>Casts whose Exsanguinate landed, so the pile behind it could be read.</summary>
    public int MeasuredCasts => _casts.Count(cast => cast.RendOnTarget is not null);

    /// <summary>Rend Exsanguinate took its share of, summed across every measured cast.</summary>
    public int TotalRendExsanguinated => _casts.Sum(cast => cast.RendOnTarget ?? 0);

    /// <summary>Mean Rend per measured cast, or zero when none could be measured.</summary>
    public double AverageRendOnTarget =>
        MeasuredCasts > 0 ? (double)TotalRendExsanguinated / MeasuredCasts : 0d;

    /// <summary>Exsanguinate damage across every Heart Splitter on the pull.</summary>
    public long TotalExsanguinateDamage => _casts.Sum(cast => cast.ExsanguinateDamage);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Slaughter))]
    private void OnSlaughter(CastEvent castEvent) => _lastSlaughter = castEvent.Timestamp;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.HeartSplitter))]
    private void OnHeartSplitter(CastEvent castEvent) =>
        _casts.Add(new HeartSplitterCast(
            castEvent.Timestamp,
            castEvent.TargetId,
            _lastSlaughter is { } slaughter ? castEvent.Timestamp - slaughter : null));

    /// <summary>
    /// Records the hit against the cast that produced it. The pile is read here, at the moment the
    /// game takes its share, and only from the first hit, so a strike that a talent spread across the
    /// pack reports the enemy it was aimed at while the damage totals the whole spread.
    /// </summary>
    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.HeartSplitterDotBonusDamage))]
    private void OnExsanguinate(DamageEvent damageEvent)
    {
        if (_casts.Count == 0) return;

        var cast = _casts[^1];
        cast.ExsanguinateDamage += damageEvent.Amount;
        cast.RendOnTarget ??= RendStackTracker.StacksOn(damageEvent.TargetId, damageEvent.TargetInstance);
    }
}

/// <summary>One Heart Splitter and the bleed its Exsanguinate found.</summary>
public sealed class HeartSplitterCast(int timestamp, int targetId, int? millisecondsSinceSlaughter)
{
    /// <summary>Encounter time of the cast.</summary>
    public int Timestamp { get; } = timestamp;

    /// <summary>The enemy the cast named.</summary>
    public int TargetId { get; } = targetId;

    /// <summary>
    /// Rend standing on the enemy at the instant Exsanguinate hit it, which is the pile the game took
    /// its share of. Null when no Exsanguinate followed the cast, leaving nothing to measure.
    /// </summary>
    public int? RendOnTarget { get; internal set; }

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
