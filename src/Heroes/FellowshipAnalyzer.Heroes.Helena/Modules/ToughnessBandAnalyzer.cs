using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>
/// Measures where Helena's Toughness sat over a pull, weighted by time rather than by sample count.
/// The log carries a Toughness snapshot on most events, so sample density rises sharply while she is
/// being hit; counting samples would read that burst as time spent in whichever band the damage drove
/// her into. Each sample therefore holds its band until the next one, and the analyzer only
/// accumulates inside its own pull, so the gaps between pulls - where Toughness parks at maximum with
/// nothing hitting her - never inflate the top band.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class ToughnessBandAnalyzer : Analyzer
{
    private readonly Dictionary<ToughnessBand, int> _bandMs = [];

    private int _atMaximumMs;
    private int _samples;
    private int? _lastTimestamp;
    private ToughnessBand _lastBand;
    private bool _lastAtMaximum;

    /// <summary>Toughness snapshots seen this pull.</summary>
    public int SampleCount => _samples;

    /// <summary>Milliseconds of the pull covered by a Toughness snapshot.</summary>
    public int MeasuredMs => Result.MeasuredMs;

    /// <summary>Milliseconds spent in each band, keyed by band.</summary>
    public IReadOnlyDictionary<ToughnessBand, int> BandMs => Result.BandMs;

    /// <summary>Milliseconds spent with Toughness at its maximum.</summary>
    public int AtMaximumMs => Result.AtMaximumMs;

    /// <summary>Share (0-1) of the measured time spent with Toughness at its maximum.</summary>
    public double AtMaximumShare => Share(Result.AtMaximumMs);

    /// <summary>Milliseconds spent in <paramref name="band"/>.</summary>
    public int MsIn(ToughnessBand band) => Result.BandMs.GetValueOrDefault(band);

    /// <summary>Share (0-1) of the measured time spent in <paramref name="band"/>.</summary>
    public double ShareIn(ToughnessBand band) => Share(MsIn(band));

    /// <summary>Share (0-1) of the measured time spent in the top band, where Toughness grants its full 35%.</summary>
    public double TopBandShare => ShareIn(ToughnessBand.Level4);

    /// <summary>
    /// The physical damage reduction Toughness averaged over the pull, weighted by time in band. A
    /// game-defined ceiling of 35% applies, so this reads directly against that cap.
    /// </summary>
    public double AverageDamageReduction
    {
        get
        {
            if (Result.MeasuredMs <= 0) return 0;

            var weighted = 0d;
            foreach (var (band, ms) in Result.BandMs)
                weighted += ToughnessBands.DamageReduction(band) * ms;

            return weighted / Result.MeasuredMs;
        }
    }

    [On<Event>]
    private void OnAnyEvent(Event e)
    {
        if (FindToughness(e) is not { Max: > 0 } toughness) return;

        Accumulate(e.Timestamp);

        _samples++;
        _lastTimestamp = e.Timestamp;
        _lastBand = ToughnessBands.For(toughness.Amount / (double)toughness.Max);
        _lastAtMaximum = toughness.Amount >= toughness.Max;
    }

    private void Accumulate(int upTo)
    {
        if (_lastTimestamp is not { } from) return;

        var elapsed = Math.Clamp(upTo, Pull.StartTime, Pull.EndTime) - Math.Clamp(from, Pull.StartTime, Pull.EndTime);
        if (elapsed <= 0) return;

        _bandMs[_lastBand] = _bandMs.GetValueOrDefault(_lastBand) + elapsed;
        if (_lastAtMaximum) _atMaximumMs += elapsed;
    }

    private ClassResource? FindToughness(Event e)
    {
        var resources = e switch
        {
            IHasSourceEvent source when Owner.ByPlayer(source) => e.SourceResources,
            IHasTargetEvent target when Owner.ToPlayer(target) => e.TargetResources,
            _ => null,
        };

        if (resources?.Resources is not { Count: > 0 } list) return null;

        foreach (var resource in list)
            if (resource.Type == ResourceTypes.Secondary)
                return resource;

        return null;
    }

    private double Share(int ms) => Result.MeasuredMs > 0 ? Math.Clamp((double)ms / Result.MeasuredMs, 0, 1) : 0;

    private Computed Result => field ??= Compute();

    private Computed Compute()
    {
        Accumulate(Pull.EndTime);
        _lastTimestamp = null;

        var measured = 0;
        foreach (var ms in _bandMs.Values) measured += ms;

        return new Computed(new Dictionary<ToughnessBand, int>(_bandMs), measured, _atMaximumMs);
    }

    private sealed record Computed(IReadOnlyDictionary<ToughnessBand, int> BandMs, int MeasuredMs, int AtMaximumMs);
}
