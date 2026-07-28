using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Helena.Statistics;

using Microsoft.Extensions.Logging;

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
/// </summary>
public sealed partial class ToughnessTracker : ResourceTracker
{
    private readonly List<ToughnessOvercap> _overcaps = [];
    private readonly Dictionary<int, int> _generatorCasts = [];

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

    /// <inheritdoc/>
    public override Type? StatisticsComponentType => Toughness is null ? null : typeof(ToughnessStatistics);

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
        if (FindGenerator(castEvent.Ability.Id) is not { } generator) return;

        _generatorCasts[castEvent.Ability.Id] = _generatorCasts.GetValueOrDefault(castEvent.Ability.Id) + 1;

        if (IsAtMaximum(castEvent.SourceResources))
            _overcaps.Add(new ToughnessOvercap(castEvent.Timestamp, castEvent.Ability.Id, generator.NominalShare));
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
/// One generation event that fired while Toughness was already at maximum, so its generation was lost.
/// </summary>
/// <param name="Timestamp">When it happened.</param>
/// <param name="SpellId">The generating ability, or the auto-attack id for a block.</param>
/// <param name="NominalShare">The share of maximum Toughness the source nominally restores.</param>
public sealed record ToughnessOvercap(int Timestamp, int SpellId, double NominalShare);
