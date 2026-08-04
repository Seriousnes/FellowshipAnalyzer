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

/// <summary>
/// Tracks Helena's Toughness, which the log streams as <see cref="ResourceTypes.Secondary"/> with a
/// per-event maximum that moves with Strength and secondary stats.
/// <para>
/// Generation is never logged as its own event, so a source that fires while Toughness already sits
/// at maximum produces no delta and is invisible to the base tracker's gain accounting. The overcap
/// incidents below close that gap by testing the generating event's own snapshot against that
/// snapshot's maximum: a Shields Up, Shield Slam or Shockwave cast made at maximum threw its
/// generation away, and so did a block landed at maximum.
/// </para>
/// <para>
/// The band Toughness sits in lives here rather than on a pull analyzer, because it is a property of
/// the resource. The base tracker's own event stream cannot supply it: <see cref="GetResourceEvents"/>
/// records a gain but not a decrease, so rebuilding band membership from it would hold a high band
/// through every drop. A snapshot timeline is therefore recorded here, and every band figure is served
/// over an explicit window by <see cref="BandsBetween"/> and <see cref="MitigationBetween"/>.
/// </para>
/// </summary>
public sealed partial class ToughnessTracker : ResourceTracker
{
    private readonly List<ToughnessOvercap> _overcaps = [];
    private readonly Dictionary<int, int> _generatorCasts = [];
    private readonly List<BandSample> _bandSamples = [];
    private readonly List<MitigatedHit> _mitigatedHits = [];

    private Event? _lastSampledEvent;
    private int _observedBlocks;
    private int _overcappedBlocks;

    private Computed Result => field ??= Compute();

    /// <summary>Creates the tracker and labels <see cref="ResourceTypes.Secondary"/> as Toughness for the resource UI.</summary>
    public ToughnessTracker(ILogger<ResourceTracker> logger) : base(logger)
    {
        DisplayNameOverrides[ResourceTypes.Secondary] = "Toughness";
    }

    /// <summary>The generation sources whose casts are checked for overcap, with each one's <c>StrengthScaler</c>.</summary>
    public static IReadOnlyList<ToughnessGenerator> Generators { get; } =
    [
        new(Spells.ShieldsUp.FSLID, 2.31),
        new(Spells.Shockwave.FSLID, 1.925),
        new(Spells.ShieldSlam.FSLID, 1.155),
    ];

    /// <summary>The nominal share of maximum Toughness a block restores.</summary>
    public static double BlockGeneration => ToughnessBands.NominalGeneration(0.24);

    /// <summary>Whether the player took Greater Shockwave, whose casts return an extra tenth of maximum Toughness.</summary>
    public bool HasGreaterShockwave => Owner.SelectedCombatant.HasTalent(HelenaTalents.GreaterShockwave);

    /// <summary>Whether the player took Front Line Defender, which adds five points to every band that grants anything.</summary>
    public bool HasFrontLineDefender => Owner.SelectedCombatant.HasTalent(HelenaTalents.FrontLineDefender);

    /// <summary>The physical damage reduction <paramref name="band"/> grants this build.</summary>
    public double DamageReductionIn(ToughnessBand band) =>
        ToughnessBands.DamageReduction(band, HasFrontLineDefender);

    /// <summary>The most physical damage reduction Toughness can grant this build.</summary>
    public double DamageReductionCeiling => ToughnessBands.Ceiling(HasFrontLineDefender);

    /// <summary>The band the most recent Toughness snapshot put the player in.</summary>
    public ToughnessBand CurrentBand =>
        _bandSamples.Count > 0 ? _bandSamples[^1].Band : ToughnessBand.Depleted;

    /// <summary>
    /// The band in force immediately before <paramref name="e"/> was dispatched, which is the one that
    /// acted on it. A hit carries its Toughness snapshot sampled after the hit resolves, so
    /// <see cref="CurrentBand"/> read from a damage handler is the band the hit left behind rather than
    /// the band that reduced it. This resolves against the snapshot's own event rather than its
    /// timestamp, so it answers the same whether or not the snapshot handler has already run for
    /// <paramref name="e"/>, and several events sharing a timestamp cannot confuse it.
    /// </summary>
    public ToughnessBand BandBefore(Event e) =>
        SampleIndexBefore(e) is var index and >= 0 ? _bandSamples[index].Band : ToughnessBand.Depleted;

    private int SampleIndexBefore(Event e) =>
        ReferenceEquals(_lastSampledEvent, e) ? _bandSamples.Count - 2 : _bandSamples.Count - 1;

    /// <summary>
    /// The share of maximum Toughness <paramref name="spellId"/> nominally restores for the build being
    /// analyzed, which for Shockwave under Greater Shockwave is its per-hit generation plus a flat tenth
    /// of maximum Toughness.
    /// </summary>
    public double NominalGenerationFor(int spellId)
    {
        if (FindGenerator(spellId) is not { } generator) return 0;

        return spellId == Spells.Shockwave.FSLID && HasGreaterShockwave
            ? generator.NominalShare + GreaterShockwaveAnalyzer.AdditionalToughnessShare
            : generator.NominalShare;
    }

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.General;

    /// <summary>The tracked Toughness state, or <c>null</c> when the log carried no Toughness snapshot.</summary>
    public ResourceState? Toughness => GetResourceState(ResourceTypes.Secondary);

    /// <summary>Every generation event that fired while Toughness was already at maximum, in encounter order.</summary>
    public IReadOnlyList<ToughnessOvercap> Overcaps => _overcaps;

    /// <summary>Generation-source casts that landed at maximum Toughness, keyed by spell id.</summary>
    public IReadOnlyDictionary<int, int> OvercappedCastsBySpell => Result.OvercappedCasts;

    /// <summary>Every generation-source cast, keyed by spell id, whether or not it overcapped.</summary>
    public IReadOnlyDictionary<int, int> GeneratorCastsBySpell => _generatorCasts;

    /// <summary>Generation-source casts made at maximum Toughness.</summary>
    public int OvercappedCasts => Result.OvercappedCastTotal;

    /// <summary>Generation-source casts made at any Toughness level.</summary>
    public int GeneratorCasts => Result.GeneratorCastTotal;

    /// <summary>Blocks landed while Toughness was already at maximum.</summary>
    public int OvercappedBlocks => _overcappedBlocks;

    /// <summary>Blocks the player landed while a Toughness snapshot was available to judge them against.</summary>
    public int ObservedBlocks => _observedBlocks;

    /// <summary>Share (0-1) of generation-source casts that were made at maximum Toughness.</summary>
    public double OvercappedCastShare =>
        Result.GeneratorCastTotal > 0 ? (double)Result.OvercappedCastTotal / Result.GeneratorCastTotal : 0;

    /// <summary>Share (0-1) of observed blocks that landed at maximum Toughness.</summary>
    public double OvercappedBlockShare =>
        _observedBlocks > 0 ? (double)_overcappedBlocks / _observedBlocks : 0;

    /// <summary>The overcapped-cast count for <paramref name="spellId"/>.</summary>
    public int OvercappedCastsFor(int spellId) => Result.OvercappedCasts.GetValueOrDefault(spellId);

    /// <summary>The total cast count for <paramref name="spellId"/> as a generation source.</summary>
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

        _observedBlocks++;
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

    /// <summary>
    /// Records what Toughness turned away from one physical hit, by inverting the band in force out of
    /// the damage that reached the player. Armour needs no modelling because whatever it removed is
    /// already inside <see cref="DamageEvent.Amount"/>; an absorb and a block are added back because
    /// both apply after Toughness rather than as part of it.
    /// </summary>
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

    /// <summary>
    /// Where Toughness sat between <paramref name="start"/> and <paramref name="end"/>, weighted by time
    /// rather than by sample count. Each snapshot holds its band until the next one, and the window opens
    /// at its first snapshot rather than at <paramref name="start"/>, so a stretch with no snapshot to
    /// read is never attributed to whichever band happened to precede the window.
    /// </summary>
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

    /// <summary>
    /// What Toughness turned away between <paramref name="start"/> and <paramref name="end"/>, against
    /// what the top band would have turned away from the same hits. A hit taken before the window's first
    /// snapshot is left out, because the only band available to measure it against is one carried in from
    /// before the window.
    /// </summary>
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
        IReadOnlyDictionary<int, int> OvercappedCasts,
        int OvercappedCastTotal,
        int GeneratorCastTotal);
}

/// <summary>
/// A Toughness generation source and the share of maximum Toughness it nominally restores.
/// </summary>
/// <param name="SpellId">The generating ability's spell id.</param>
/// <param name="StrengthScaler">The source's <c>StrengthScaler</c> from <c>Constants.Toughness</c>.</param>
public sealed record ToughnessGenerator(int SpellId, double StrengthScaler)
{
    /// <summary>The nominal share of maximum Toughness this source restores.</summary>
    public double NominalShare => ToughnessBands.NominalGeneration(StrengthScaler);
}

/// <summary>
/// Where Toughness sat over one window of the fight.
/// </summary>
/// <param name="BandMs">Milliseconds spent in each band, keyed by band.</param>
/// <param name="MeasuredMs">Milliseconds of the window covered by a Toughness snapshot.</param>
/// <param name="AtMaximumMs">Milliseconds spent with Toughness at its maximum.</param>
/// <param name="SampleCount">Toughness snapshots seen in the window.</param>
/// <param name="MsToTopBand">
/// Milliseconds from the window opening to its first top-band snapshot, or <c>null</c> when Toughness
/// never reached it. A window that opened in the top band still carries a small figure here when its
/// first snapshot arrived late, so read it alongside <paramref name="StartedAtTopBand"/>.
/// </param>
/// <param name="StartedAtTopBand">Whether the window's first snapshot was already in the top band.</param>
public sealed record ToughnessBandWindow(
    IReadOnlyDictionary<ToughnessBand, int> BandMs,
    int MeasuredMs,
    int AtMaximumMs,
    int SampleCount,
    int? MsToTopBand,
    bool StartedAtTopBand)
{
    /// <summary>A window no Toughness snapshot fell inside.</summary>
    public static ToughnessBandWindow Empty { get; } =
        new(new Dictionary<ToughnessBand, int>(), 0, 0, 0, null, false);
}

/// <summary>
/// What Toughness turned away over one window of the fight.
/// </summary>
/// <param name="Mitigated">Damage Toughness turned away across the window's qualifying hits.</param>
/// <param name="AtCeiling">What the top band would have turned away from those same hits.</param>
/// <param name="Hits">Qualifying physical hits folded in.</param>
/// <param name="Ticks">The share of <paramref name="Hits"/> that were periodic ticks.</param>
public sealed record ToughnessMitigationWindow(double Mitigated, double AtCeiling, int Hits, int Ticks)
{
    /// <summary>A window no qualifying hit fell inside.</summary>
    public static ToughnessMitigationWindow Empty { get; } = new(0, 0, 0, 0);

    /// <summary>Share (0-1) of what the top band would have turned away that Toughness actually did.</summary>
    public double Efficiency => AtCeiling > 0 ? Mitigated / AtCeiling : 0;
}

/// <summary>
/// One generation event that fired while Toughness was already at maximum, so its generation was lost.
/// </summary>
/// <param name="Timestamp">When it happened.</param>
/// <param name="SpellId">The generating ability, or the auto-attack id for a block.</param>
/// <param name="NominalShare">The share of maximum Toughness the source nominally restores.</param>
public sealed record ToughnessOvercap(int Timestamp, int SpellId, double NominalShare);
