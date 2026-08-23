using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<ToughnessTracker>]
public sealed partial class ToughnessAnalyzer : Analyzer
{
    private ToughnessBandWindow Bands => field ??= ToughnessTracker.BandsBetween(Pull.StartTime, Pull.EndTime);

    private ToughnessMitigationWindow Mitigation => field ??= ToughnessTracker.MitigationBetween(Pull.StartTime, Pull.EndTime);

    public int SampleCount => Bands.SampleCount;

    public int MeasuredMs => Bands.MeasuredMs;

    public Dictionary<ToughnessBand, int> BandMs => Bands.BandMs;

    public int AtMaximumMs => Bands.AtMaximumMs;

    public double AtMaximumShare => Share(Bands.AtMaximumMs);

    public int MsIn(ToughnessBand band) => Bands.BandMs.GetValueOrDefault(band);

    public double ShareIn(ToughnessBand band) => Share(MsIn(band));

    public double TopBandShare => ShareIn(ToughnessBand.Level4);

    public int? MsToTopBand => Bands.MsToTopBand;

    public bool StartedAtTopBand => Bands.StartedAtTopBand;

    public bool HasFrontLineDefender => ToughnessTracker.HasFrontLineDefender;

    public double DamageReductionIn(ToughnessBand band) => ToughnessTracker.DamageReductionIn(band);

    public double DamageReductionCeiling => ToughnessTracker.DamageReductionCeiling;

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

    public double ToughnessMitigated => Mitigation.Mitigated;

    public double MitigationAtCeiling => Mitigation.AtCeiling;

    public double MitigationEfficiency => Mitigation.Efficiency;

    public int MitigatedHits => Mitigation.Hits;

    public int MitigatedTicks => Mitigation.Ticks;

    private double Share(int ms) => Bands.MeasuredMs > 0 ? Math.Clamp((double)ms / Bands.MeasuredMs, 0, 1) : 0;
}
