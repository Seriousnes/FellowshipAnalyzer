using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>
/// Projects one pull's slice of <see cref="ToughnessTracker"/> for the guide: where Toughness sat, how
/// long it took to reach the top band, and how much of the physical damage aimed at Helena it turned
/// away.
/// <para>
/// Every figure here comes from the tracker, which owns Toughness. This analyzer samples nothing and
/// derives nothing of its own; it narrows the tracker's fight-wide view to <see cref="Analyzer.Pull"/>
/// and memoises the result.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<ToughnessTracker>]
public sealed partial class ToughnessAnalyzer : Analyzer
{
    private ToughnessBandWindow Bands => field ??= ToughnessTracker.BandsBetween(Pull.StartTime, Pull.EndTime);

    private ToughnessMitigationWindow Mitigation => field ??= ToughnessTracker.MitigationBetween(Pull.StartTime, Pull.EndTime);

    /// <summary>Toughness snapshots seen this pull.</summary>
    public int SampleCount => Bands.SampleCount;

    /// <summary>Milliseconds of the pull covered by a Toughness snapshot.</summary>
    public int MeasuredMs => Bands.MeasuredMs;

    /// <summary>Milliseconds spent in each band, keyed by band.</summary>
    public IReadOnlyDictionary<ToughnessBand, int> BandMs => Bands.BandMs;

    /// <summary>Milliseconds spent with Toughness at its maximum.</summary>
    public int AtMaximumMs => Bands.AtMaximumMs;

    /// <summary>Share (0-1) of the measured time spent with Toughness at its maximum.</summary>
    public double AtMaximumShare => Share(Bands.AtMaximumMs);

    /// <summary>Milliseconds spent in <paramref name="band"/>.</summary>
    public int MsIn(ToughnessBand band) => Bands.BandMs.GetValueOrDefault(band);

    /// <summary>Share (0-1) of the measured time spent in <paramref name="band"/>.</summary>
    public double ShareIn(ToughnessBand band) => Share(MsIn(band));

    /// <summary>Share (0-1) of the measured time spent in the top band, where Toughness grants its full reduction.</summary>
    public double TopBandShare => ShareIn(ToughnessBand.Level4);

    /// <summary>
    /// Milliseconds from the pull opening to the first snapshot in the top band, or <c>null</c> when
    /// Toughness never reached it. A pull that opened in the top band still carries a small figure here
    /// when its first snapshot arrived after the pull start, so read it alongside
    /// <see cref="StartedAtTopBand"/> rather than on its own.
    /// </summary>
    public int? MsToTopBand => Bands.MsToTopBand;

    /// <summary>
    /// Whether the pull's first Toughness snapshot was already in the top band. Common on a trash chain,
    /// where Toughness carries over from the previous pull.
    /// </summary>
    public bool StartedAtTopBand => Bands.StartedAtTopBand;

    /// <summary>Whether the player took Front Line Defender, which adds five points to every band that grants anything.</summary>
    public bool HasFrontLineDefender => ToughnessTracker.HasFrontLineDefender;

    /// <summary>The physical damage reduction <paramref name="band"/> grants this build.</summary>
    public double DamageReductionIn(ToughnessBand band) => ToughnessTracker.DamageReductionIn(band);

    /// <summary>The most physical damage reduction Toughness can grant this build.</summary>
    public double DamageReductionCeiling => ToughnessTracker.DamageReductionCeiling;

    /// <summary>
    /// The physical damage reduction Toughness averaged over the pull, weighted by time in band. The
    /// game caps it at the top band's value, so this reads directly against
    /// <see cref="DamageReductionCeiling"/>.
    /// </summary>
    public double AverageDamageReduction
    {
        get
        {
            if (Bands.MeasuredMs <= 0) return 0;

            var weighted = 0d;
            foreach (var (band, ms) in Bands.BandMs)
                weighted += DamageReductionIn(band) * ms;

            return weighted / Bands.MeasuredMs;
        }
    }

    /// <summary>The damage Toughness turned away across the pull's qualifying physical hits.</summary>
    public double ToughnessMitigated => Mitigation.Mitigated;

    /// <summary>
    /// What the same hits would have had turned away with Toughness held in the top band throughout,
    /// which is the denominator <see cref="MitigationEfficiency"/> reads against.
    /// </summary>
    public double MitigationAtCeiling => Mitigation.AtCeiling;

    /// <summary>
    /// Share (0-1) of the physical damage aimed at the player that Toughness turned away, against what
    /// the top band would have turned away from the same hits. Weighted by damage rather than by time,
    /// so a low band while nothing is landing costs nothing and a low band inside a burst costs the most.
    /// </summary>
    public double MitigationEfficiency => Mitigation.Efficiency;

    /// <summary>Qualifying physical hits folded into <see cref="MitigationEfficiency"/>.</summary>
    public int MitigatedHits => Mitigation.Hits;

    /// <summary>Qualifying hits that were periodic ticks rather than direct hits.</summary>
    public int MitigatedTicks => Mitigation.Ticks;

    private double Share(int ms) => Bands.MeasuredMs > 0 ? Math.Clamp((double)ms / Bands.MeasuredMs, 0, 1) : 0;
}
