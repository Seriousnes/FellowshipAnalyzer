using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

/// <summary>
/// Measures how quickly Tariq converts a Focused Wrath cast into a Fury spender on every pull,
/// regardless of shape. Focused Wrath discounts the Fury cost of the next spenders and adds damage on
/// top, and its charges are consumed by spenders alone, so the whole value of the cooldown is the
/// spender that follows it: the latency between the two is the measure here.
/// </summary>
/// <remarks>
/// Pairing keys off the Focused Wrath <see cref="CastEvent"/>, never off the buff. The Mouth for War
/// talent applies the same self-buff without a cast, many times per pull, so buff applications vastly
/// outnumber casts and would drown the reading; only a cast is a cooldown the player spent and can
/// therefore mistime.
/// <para>
/// Casts and spenders are held in one dispatch-ordered list and paired by scanning forward, so a
/// spender logged at the same millisecond as the cast (Focused Wrath is off the global cooldown) pairs
/// correctly on stream order rather than on a timestamp tie-break.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class FocusedWrathAnalyzer : Analyzer
{
    /// <summary>
    /// Milliseconds the Focused Wrath self-buff lasts, the span a spender has to capture it. Season 3
    /// <c>hero_data</c> Kit constant (Focused Wrath Duration 15).
    /// </summary>
    public const int BuffDurationMs = 15_000;

    private readonly List<TrackedCast> _tracked = [];

    private List<FocusedWrathCast>? _evaluated;
    private List<FocusedWrathCast> Evaluated => _evaluated ??= Build();

    /// <summary>Every Focused Wrath cast in the pull, in the order it was cast.</summary>
    public IReadOnlyList<FocusedWrathCast> Casts => Evaluated;

    public int CastCount => Evaluated.Count;

    /// <summary>Casts that no spender followed inside the buff.</summary>
    public int UnpairedCasts => Evaluated.Count(cast => cast.LatencyToSpenderMs is null);

    /// <summary>
    /// Mean milliseconds from a cast to the spender that captured it, over the paired casts. Null when
    /// no cast in the pull was followed by a spender inside the buff.
    /// </summary>
    public double? AverageLatencyMs
    {
        get
        {
            var paired = Evaluated.Where(cast => cast.LatencyToSpenderMs is not null).ToList();
            return paired.Count == 0 ? null : paired.Average(cast => cast.LatencyToSpenderMs!.Value);
        }
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.FocusedWrath))]
    private void OnFocusedWrathCast(CastEvent @event) => Track(@event, isFocusedWrath: true);

    [On<CastEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.SkullCrusher),
        nameof(Spells.HammerStorm),
        nameof(Spells.CullingStrike),
    })]
    private void OnSpenderCast(CastEvent @event) => Track(@event, isFocusedWrath: false);

    private void Track(CastEvent @event, bool isFocusedWrath)
    {
        if (@event.Fake)
            return;

        _tracked.Add(new TrackedCast(@event.Timestamp, isFocusedWrath));
    }

    private List<FocusedWrathCast> Build()
    {
        var built = new List<FocusedWrathCast>();
        for (var i = 0; i < _tracked.Count; i++)
        {
            if (!_tracked[i].IsFocusedWrath)
                continue;

            var timestamp = _tracked[i].Timestamp;
            int? latency = null;
            var spenders = 0;

            for (var j = i + 1; j < _tracked.Count; j++)
            {
                var candidate = _tracked[j];
                if (candidate.Timestamp - timestamp > BuffDurationMs)
                    break;

                if (candidate.IsFocusedWrath)
                    continue;

                spenders++;
                latency ??= candidate.Timestamp - timestamp;
            }

            built.Add(new FocusedWrathCast(timestamp, latency, spenders));
        }

        return built;
    }

    private readonly record struct TrackedCast(int Timestamp, bool IsFocusedWrath);
}

/// <summary>One Focused Wrath cast and the spenders that captured it.</summary>
/// <param name="Timestamp">Fight-relative timestamp of the cast.</param>
/// <param name="LatencyToSpenderMs">
/// Milliseconds to the first spender cast inside the buff, or null when none followed.
/// </param>
/// <param name="SpendersInBuff">Spender casts within <see cref="FocusedWrathAnalyzer.BuffDurationMs"/> of the cast.</param>
public readonly record struct FocusedWrathCast(int Timestamp, int? LatencyToSpenderMs, int SpendersInBuff);
