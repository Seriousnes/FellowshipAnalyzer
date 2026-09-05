using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;
using FellowshipAnalyzer.Core.UI;

using Microsoft.Extensions.Logging;

using HelenaTalents = FellowshipAnalyzer.Core.Common.Spells.HelenaTalents;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

public sealed partial class ToughnessTracker : ResourceTracker
{
    private readonly List<ToughnessOvercap> _overcaps = [];
    private readonly Dictionary<int, int> _generatorCasts = [];
    private readonly List<BandSample> _bandSamples = [];
    private readonly List<MitigatedHit> _mitigatedHits = [];

    private Event? _lastSampledEvent;
    private int _blocks;
    private int _overcappedBlocks;

    private Computed Result => field ??= Compute();

    public ToughnessTracker(ILogger<ResourceTracker> logger) : base(logger)
    {
        DisplayNameOverrides[ResourceTypes.Secondary] = "Toughness";
    }

    public static List<ToughnessGenerator> Generators { get; } =
    [
        new(Spells.ShieldsUp.FSLID, 2.31),
        new(Spells.Shockwave.FSLID, 1.925),
        new(Spells.ShieldSlam.FSLID, 1.155),
    ];

    public static double BlockGeneration => ToughnessBands.NominalGeneration(0.24);

    public bool HasGreaterShockwave => Owner.SelectedCombatant.HasTalent(HelenaTalents.GreaterShockwave);

    public bool HasFrontLineDefender => Owner.SelectedCombatant.HasTalent(HelenaTalents.FrontLineDefender);

    public double DamageReductionIn(ToughnessBand band) =>
        ToughnessBands.DamageReduction(band, HasFrontLineDefender);

    public double DamageReductionCeiling => ToughnessBands.Ceiling(HasFrontLineDefender);

    public ToughnessBand CurrentBand =>
        _bandSamples.Count > 0 ? _bandSamples[^1].Band : ToughnessBand.Depleted;

    public ToughnessBand BandBefore(Event e) =>
        SampleIndexBefore(e) is var index and >= 0 ? _bandSamples[index].Band : ToughnessBand.Depleted;

    private int SampleIndexBefore(Event e) =>
        ReferenceEquals(_lastSampledEvent, e) ? _bandSamples.Count - 2 : _bandSamples.Count - 1;

    public double NominalGenerationFor(int spellId)
    {
        if (FindGenerator(spellId) is not { } generator) return 0;

        return spellId == Spells.Shockwave.FSLID && HasGreaterShockwave
            ? generator.NominalShare + GreaterShockwaveAnalyzer.AdditionalToughnessShare
            : generator.NominalShare;
    }

    public override StatisticCategory StatisticCategory => StatisticCategory.General;

    public ResourceState? Toughness => GetResourceState(ResourceTypes.Secondary);

    public List<ToughnessOvercap> Overcaps => _overcaps;

    public Dictionary<int, int> OvercappedCastsBySpell => Result.OvercappedCasts;

    public Dictionary<int, int> GeneratorCastsBySpell => _generatorCasts;

    public int OvercappedCasts => Result.OvercappedCastTotal;

    public int GeneratorCasts => Result.GeneratorCastTotal;

    public int OvercappedBlocks => _overcappedBlocks;

    public int Blocks => _blocks;

    public double OvercappedCastShare =>
        Result.GeneratorCastTotal > 0 ? (double)Result.OvercappedCastTotal / Result.GeneratorCastTotal : 0;

    public double OvercappedBlockShare =>
        _blocks > 0 ? (double)_overcappedBlocks / _blocks : 0;

    public int OvercappedCastsFor(int spellId) => Result.OvercappedCasts.GetValueOrDefault(spellId);

    public int GeneratorCastsFor(int spellId) => _generatorCasts.GetValueOrDefault(spellId);

    [On<CastEvent>(By = Actor.Player)]
    private void OnGeneratorCast(CastEvent castEvent)
    {
        if (FindGenerator(castEvent.Ability.Id) is null) return;

        _generatorCasts[castEvent.Ability.Id] = _generatorCasts.GetValueOrDefault(castEvent.Ability.Id) + 1;

        if (IsAtMaximum(castEvent.SourceResources))
        {
            _overcaps.Add(new ToughnessOvercap(
                castEvent.Timestamp, castEvent.Ability.Id, NominalGenerationFor(castEvent.Ability.Id)));
        }
    }

    [On<DamageEvent>(To = Actor.Player)]
    private void OnDamageTaken(DamageEvent damageEvent)
    {
        if (damageEvent.HitType != HitType.Block) return;
        if (FindToughness(damageEvent.TargetResources) is not { Max: > 0 } toughness) return;

        _blocks++;
        if (toughness.Amount < toughness.Max) return;

        _overcappedBlocks++;
        _overcaps.Add(new ToughnessOvercap(damageEvent.Timestamp, Spells.Attack.FSLID, BlockGeneration));
    }

    [On<Event>]
    private void OnToughnessSnapshot(Event e)
    {
        if (FindToughness(e) is not { Max: > 0 } toughness) return;

        _bandSamples.Add(new BandSample(
            e.Timestamp,
            ToughnessBands.For(toughness.Amount / (double)toughness.Max),
            toughness.Amount >= toughness.Max));
        _lastSampledEvent = e;
    }

    [On<DamageEvent>(To = Actor.Player)]
    private void OnPhysicalDamageTaken(DamageEvent damageEvent)
    {
        if (damageEvent.HitType is not (HitType.Normal or HitType.Crit or HitType.Block or HitType.GrievousCrit)) return;
        if (!damageEvent.IsPhysical) return;

        var sampleIndex = SampleIndexBefore(damageEvent);
        if (sampleIndex < 0) return;

        var reduction = DamageReductionIn(_bandSamples[sampleIndex].Band);
        var reachedThePlayer = damageEvent.Amount + (damageEvent.Absorbed ?? 0) + damageEvent.Blocked;
        var preToughness = reachedThePlayer / (1 - reduction);

        _mitigatedHits.Add(new MitigatedHit(
            damageEvent.Timestamp,
            sampleIndex,
            preToughness * reduction,
            preToughness * DamageReductionCeiling,
            damageEvent.Tick));
    }

    public ToughnessBandWindow BandsBetween(int start, int end)
    {
        var first = FirstSampleIndexIn(start, end);
        if (first < 0) return ToughnessBandWindow.Empty;

        var bandMs = new Dictionary<ToughnessBand, int>();
        var atMaximumMs = 0;
        var samples = 0;
        int? msToTopBand = null;

        for (var i = first; i < _bandSamples.Count && _bandSamples[i].Timestamp <= end; i++)
        {
            var sample = _bandSamples[i];
            samples++;

            if (msToTopBand is null && sample.Band == ToughnessBand.Level4)
                msToTopBand = sample.Timestamp - start;

            var until = i + 1 < _bandSamples.Count && _bandSamples[i + 1].Timestamp <= end
                ? _bandSamples[i + 1].Timestamp
                : end;

            var elapsed = until - sample.Timestamp;
            if (elapsed <= 0) continue;

            bandMs[sample.Band] = bandMs.GetValueOrDefault(sample.Band) + elapsed;
            if (sample.AtMaximum) atMaximumMs += elapsed;
        }

        var measured = 0;
        foreach (var ms in bandMs.Values) measured += ms;

        return new ToughnessBandWindow(
            bandMs, measured, atMaximumMs, samples, msToTopBand,
            _bandSamples[first].Band == ToughnessBand.Level4);
    }

    public ToughnessMitigationWindow MitigationBetween(int start, int end)
    {
        var first = FirstSampleIndexIn(start, end);
        if (first < 0) return ToughnessMitigationWindow.Empty;

        double mitigated = 0, atCeiling = 0;
        int hits = 0, ticks = 0;

        foreach (var hit in _mitigatedHits)
        {
            if (hit.SampleIndex < first || hit.Timestamp < start || hit.Timestamp > end) continue;

            mitigated += hit.Mitigated;
            atCeiling += hit.AtCeiling;
            hits++;
            if (hit.Tick) ticks++;
        }

        return new ToughnessMitigationWindow(mitigated, atCeiling, hits, ticks);
    }

    private int FirstSampleIndexIn(int start, int end)
    {
        for (var i = 0; i < _bandSamples.Count; i++)
        {
            if (_bandSamples[i].Timestamp > end) break;
            if (_bandSamples[i].Timestamp >= start) return i;
        }

        return -1;
    }

    private ClassResource? FindToughness(Event e)
    {
        var resources = e switch
        {
            IHasSourceEvent source when Owner.ByPlayer(source) => e.SourceResources,
            IHasTargetEvent target when Owner.ToPlayer(target) => e.TargetResources,
            _ => null,
        };

        return FindToughness(resources);
    }

    private readonly record struct BandSample(int Timestamp, ToughnessBand Band, bool AtMaximum);

    private readonly record struct MitigatedHit(
        int Timestamp, int SampleIndex, double Mitigated, double AtCeiling, bool Tick);

    private static ToughnessGenerator? FindGenerator(int spellId)
    {
        foreach (var generator in Generators)
            if (generator.SpellId == spellId)
                return generator;

        return null;
    }

    private static bool IsAtMaximum(ActorResources? resources) =>
        FindToughness(resources) is { Max: > 0 } toughness && toughness.Amount >= toughness.Max;

    private static ClassResource? FindToughness(ActorResources? resources)
    {
        if (resources?.Resources is not { Count: > 0 } list) return null;

        foreach (var resource in list)
            if (resource.Type == ResourceTypes.Secondary)
                return resource;

        return null;
    }

    private Computed Compute()
    {
        var overcappedCasts = new Dictionary<int, int>();
        foreach (var overcap in _overcaps)
        {
            if (FindGenerator(overcap.SpellId) is null) continue;
            overcappedCasts[overcap.SpellId] = overcappedCasts.GetValueOrDefault(overcap.SpellId) + 1;
        }

        var generatorCastTotal = 0;
        foreach (var count in _generatorCasts.Values) generatorCastTotal += count;

        var overcappedCastTotal = 0;
        foreach (var count in overcappedCasts.Values) overcappedCastTotal += count;

        return new Computed(overcappedCasts, overcappedCastTotal, generatorCastTotal);
    }

    private sealed record Computed(
        Dictionary<int, int> OvercappedCasts,
        int OvercappedCastTotal,
        int GeneratorCastTotal);
}

public sealed record ToughnessGenerator(int SpellId, double StrengthScaler)
{
    public double NominalShare => ToughnessBands.NominalGeneration(StrengthScaler);
}

public sealed record ToughnessBandWindow(
    Dictionary<ToughnessBand, int> BandMs,
    int MeasuredMs,
    int AtMaximumMs,
    int SampleCount,
    int? MsToTopBand,
    bool StartedAtTopBand)
{
    public static ToughnessBandWindow Empty { get; } =
        new([], 0, 0, 0, null, false);
}

public sealed record ToughnessMitigationWindow(double Mitigated, double AtCeiling, int Hits, int Ticks)
{
    public static ToughnessMitigationWindow Empty { get; } = new(0, 0, 0, 0);

    public double Efficiency => AtCeiling > 0 ? Mitigated / AtCeiling : 0;
}

public sealed record ToughnessOvercap(int Timestamp, int SpellId, double NominalShare);
