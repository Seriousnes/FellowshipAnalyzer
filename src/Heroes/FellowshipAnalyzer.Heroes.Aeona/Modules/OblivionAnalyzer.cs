using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>The pull read surface for Oblivion.</summary>
public interface IOblivionAnalyzer : IAnalyzerSurface;

/// <summary>One Oblivion cast: the healing, shielding, and damage its hit produced, and the tank's Stagger at the cast.</summary>
/// <param name="Timestamp">When the cast completed.</param>
/// <param name="Target">The enemy the cast named.</param>
/// <param name="TankStaggerFraction">The tank's Stagger as a share of its maximum health at the cast, or null when nothing within <see cref="StaggerTracker.StaggerMaxAgeMs"/> precedes it.</param>
/// <param name="CleanseAvailable">Whether Amend Fate or Restore Continuity was available at the cast with the tank alive.</param>
/// <param name="EffectiveHealing">Effective healing across the allies the cast's hit healed, not counting Erasure.</param>
/// <param name="Overheal">Overheal across those allies.</param>
/// <param name="AlliesHealed">Allies the cast's hit healed.</param>
/// <param name="ShieldApplied">Absorb the cast's hit added to allies' Oblivion's Embrace shields, summed across allies.</param>
/// <param name="AlliesShielded">Allies whose shield the cast's hit grew.</param>
/// <param name="Damage">Damage the cast's hit dealt, not counting Erasure.</param>
/// <param name="FreeCastSource">What made the cast free, or null when it cost Chrona.</param>
public sealed record OblivionCast(
    int Timestamp,
    UnitKey Target,
    double? TankStaggerFraction,
    bool CleanseAvailable,
    long EffectiveHealing,
    long Overheal,
    int AlliesHealed,
    long ShieldApplied,
    int AlliesShielded,
    long Damage,
    FreeCastSource? FreeCastSource)
{
    /// <summary>Whether the cast cost no Chrona.</summary>
    public bool WasFree => FreeCastSource is not null;

    /// <summary>
    /// Whether the tank held more than <see cref="OblivionAnalyzer.CleansePriorityStaggerFraction"/> of its
    /// maximum health in Stagger and a cleanse was available.
    /// </summary>
    public bool AtCleansePriority =>
        TankStaggerFraction > OblivionAnalyzer.CleansePriorityStaggerFraction && CleanseAvailable;

    /// <summary>Whether the cast could be rated.</summary>
    public bool Rated => TankStaggerFraction is not null;
}

/// <summary>One enemy Oblivion was cast into during the pull.</summary>
/// <param name="Unit">The enemy.</param>
/// <param name="Casts">Oblivion casts into it.</param>
/// <param name="CastsRated">Casts into it that could be rated.</param>
/// <param name="CastsAtCleansePriority">Casts into it at cleanse priority.</param>
/// <param name="Damage">Damage those casts dealt.</param>
/// <param name="EffectiveHealing">Effective healing those casts did.</param>
public sealed record OblivionTarget(
    UnitKey Unit,
    int Casts,
    int CastsRated,
    int CastsAtCleansePriority,
    long Damage,
    long EffectiveHealing);

/// <summary>
/// Oblivion over one pull: every cast with what its hit produced, the casts made while the tank was at
/// cleanse priority, the same figures grouped by the enemy each cast was made into, and what Erasure
/// produced apart from the casts.
/// </summary>
/// <remarks>
/// <para>
/// A cast's damage arrives within a few milliseconds, so it is credited to the most recent cast within
/// <see cref="AttributionMs"/>. The cast's heals and shields are the ones
/// <see cref="Normalizers.HitLinkNormalizer"/> linked to that damage.
/// </para>
/// <para>
/// Every Oblivion hit adds to one accumulating Erasure dot on its target, so an Erasure tick belongs to
/// no single cast. Erasure's damage, and the heals and shields linked to its ticks, are counted for the
/// pull and read per cast against the pull's casts.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<StaggerTracker>]
[Dependency<FreeCastTracker>]
[Dependency<SpellUsable>]
public sealed partial class OblivionAnalyzer : Analyzer, IOblivionAnalyzer
{
    /// <summary>The share of the tank's maximum health in Stagger past which a cleanse outranks Oblivion.</summary>
    public const double CleansePriorityStaggerFraction = 0.40;

    /// <summary>Milliseconds after a cast within which its damage is credited to it.</summary>
    public const int AttributionMs = 50;

    private readonly List<CastState> _casts = [];

    private long _erasureShieldApplied;

    /// <summary>Every Oblivion cast in the pull, in cast order.</summary>
    public IReadOnlyList<OblivionCast> Casts => field ??= [.. _casts.Select(Build)];

    /// <summary>Oblivion casts in the pull.</summary>
    public int CastCount => _casts.Count;

    /// <summary>Casts that could be rated.</summary>
    public int CastsRated => Casts.Count(cast => cast.Rated);

    /// <summary>Casts at cleanse priority.</summary>
    public int CastsAtCleansePriority => Casts.Count(cast => cast.AtCleansePriority);

    /// <summary>Effective healing across every cast, not counting Erasure.</summary>
    public long EffectiveHealing => Casts.Sum(cast => cast.EffectiveHealing);

    /// <summary>Overheal across every cast, not counting Erasure.</summary>
    public long Overheal => Casts.Sum(cast => cast.Overheal);

    /// <summary>Damage across every cast, not counting Erasure.</summary>
    public long Damage => Casts.Sum(cast => cast.Damage);

    /// <summary>Whether the build has Oblivion's Embrace.</summary>
    public bool OblivionsEmbraceTalented => Owner.SelectedCombatant.HasTalent(AeonaTalents.OblivionsEmbrace);

    /// <summary>Absorb the casts' hits added to shields, not counting Erasure. Null without Oblivion's Embrace.</summary>
    public long? ShieldApplied => OblivionsEmbraceTalented ? Casts.Sum(cast => cast.ShieldApplied) : null;

    /// <summary>Whether the build has Erasure.</summary>
    public bool ErasureTalented => Owner.SelectedCombatant.HasTalent(AeonaTalents.Erasure);

    /// <summary>Damage Erasure's ticks dealt in the pull.</summary>
    public long ErasureDamage { get; private set; }

    /// <summary>Effective healing Erasure's ticks did in the pull.</summary>
    public long ErasureEffectiveHealing { get; private set; }

    /// <summary>Overheal from Erasure's ticks in the pull.</summary>
    public long ErasureOverheal { get; private set; }

    /// <summary>Absorb Erasure's ticks added to shields in the pull. Null without Oblivion's Embrace.</summary>
    public long? ErasureShieldApplied => OblivionsEmbraceTalented ? _erasureShieldApplied : null;

    /// <summary>Casts that cost no Chrona.</summary>
    public int FreeCasts => Casts.Count(cast => cast.WasFree);

    /// <summary>Uchronia and Epoch Break windows opened inside the pull.</summary>
    public int FreeCastOpportunities => FreeCastTracker.OpportunitiesBetween(Pull.StartTime, Pull.EndTime);

    /// <summary>Every enemy Oblivion was cast into, most casts first.</summary>
    public IReadOnlyList<OblivionTarget> Targets => field ??= BuildTargets();

    /// <summary>Effective healing per cast, not counting Erasure.</summary>
    public double EffectiveHealingPerCast => _casts.Count == 0 ? 0 : (double)EffectiveHealing / _casts.Count;

    /// <summary>Shield absorb applied per cast, not counting Erasure. Null without Oblivion's Embrace.</summary>
    public double? ShieldAppliedPerCast =>
        ShieldApplied is { } applied && _casts.Count > 0 ? (double)applied / _casts.Count : null;

    /// <summary>Damage per cast, not counting Erasure.</summary>
    public double DamagePerCast => _casts.Count == 0 ? 0 : (double)Damage / _casts.Count;

    /// <summary>Erasure's effective healing per cast in the pull. Null without Erasure or with no cast.</summary>
    public double? ErasureEffectiveHealingPerCast =>
        ErasureTalented && _casts.Count > 0 ? (double)ErasureEffectiveHealing / _casts.Count : null;

    /// <summary>Erasure's shield absorb applied per cast in the pull. Null without Erasure, without Oblivion's Embrace, or with no cast.</summary>
    public double? ErasureShieldAppliedPerCast =>
        ErasureTalented && ErasureShieldApplied is { } applied && _casts.Count > 0 ? (double)applied / _casts.Count : null;

    /// <summary>Erasure's damage per cast in the pull. Null without Erasure or with no cast.</summary>
    public double? ErasureDamagePerCast =>
        ErasureTalented && _casts.Count > 0 ? (double)ErasureDamage / _casts.Count : null;

    /// <summary>Effective healing plus shield absorb applied, Erasure's included, per cast. Null with no cast.</summary>
    public double? ValuePerCast =>
        _casts.Count == 0
            ? null
            : (EffectiveHealing + (ShieldApplied ?? 0) + ErasureEffectiveHealing + (ErasureShieldApplied ?? 0)) / (double)_casts.Count;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Oblivion))]
    private void OnCast(CastEvent e)
    {
        var tankAlive = StaggerTracker.TankId is { } tank && StaggerTracker.IsAlive(tank, e.Timestamp);
        var cleanseReady = SpellUsable.IsAvailable(Spells.AmendFate.FSLID)
            || SpellUsable.IsAvailable(Spells.RestoreContinuity.FSLID);

        _casts.Add(new CastState(e.Timestamp, new UnitKey(e.TargetId, e.TargetInstance ?? 0), tankAlive && cleanseReady));
    }

    [On<DamageEvent>(By = Actor.Player, Spells = [nameof(Spells.Oblivion), nameof(Spells.OblivionDamage)])]
    private void OnDamage(DamageEvent e)
    {
        if (Current(e.Timestamp) is not { } cast) return;

        var hit = HitYield.Of(e);
        cast.Damage += e.Amount;
        cast.EffectiveHealing += hit.EffectiveHealing;
        cast.Overheal += hit.Overheal;
        cast.AlliesHealed += hit.AlliesHealed;
        cast.ShieldApplied += hit.ShieldApplied;
        cast.AlliesShielded += hit.AlliesShielded;
    }

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.Erasure))]
    private void OnErasureTick(DamageEvent e)
    {
        var hit = HitYield.Of(e);
        ErasureDamage += e.Amount;
        ErasureEffectiveHealing += hit.EffectiveHealing;
        ErasureOverheal += hit.Overheal;
        _erasureShieldApplied += hit.ShieldApplied;
    }

    private CastState? Current(int timestamp) =>
        _casts.Count > 0 && timestamp - _casts[^1].Timestamp <= AttributionMs && timestamp >= _casts[^1].Timestamp
            ? _casts[^1]
            : null;

    private OblivionCast Build(CastState state) => new(
        state.Timestamp,
        state.Target,
        StaggerTracker.TankId is { } tank
            ? StaggerTracker.StaggerFractionOfMaxHp(tank, state.Timestamp, StaggerTracker.StaggerMaxAgeMs)
            : null,
        state.CleanseAvailable,
        state.EffectiveHealing,
        state.Overheal,
        state.AlliesHealed,
        OblivionsEmbraceTalented ? state.ShieldApplied : 0,
        OblivionsEmbraceTalented ? state.AlliesShielded : 0,
        state.Damage,
        FreeCastTracker.FreeCastAt(state.Timestamp, Spells.Oblivion.FSLID)?.Source);

    private List<OblivionTarget> BuildTargets() =>
    [
        .. Casts
            .GroupBy(cast => cast.Target)
            .Select(group => new OblivionTarget(
                group.Key,
                group.Count(),
                group.Count(cast => cast.Rated),
                group.Count(cast => cast.AtCleansePriority),
                group.Sum(cast => cast.Damage),
                group.Sum(cast => cast.EffectiveHealing)))
            .OrderByDescending(target => target.Casts)
            .ThenBy(target => target.Unit.ActorId)
            .ThenBy(target => target.Unit.Instance),
    ];

    private sealed class CastState(int timestamp, UnitKey target, bool cleanseAvailable)
    {
        public int Timestamp { get; } = timestamp;
        public UnitKey Target { get; } = target;
        public bool CleanseAvailable { get; } = cleanseAvailable;
        public long EffectiveHealing { get; set; }
        public long Overheal { get; set; }
        public int AlliesHealed { get; set; }
        public long ShieldApplied { get; set; }
        public int AlliesShielded { get; set; }
        public long Damage { get; set; }
    }
}
