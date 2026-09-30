using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>The pull read surface for Oblivion.</summary>
public interface IOblivionAnalyzer : IAnalyzerSurface;

/// <summary>One Oblivion cast: the healing, shielding, and damage it produced, and the tank's Stagger at the cast.</summary>
/// <param name="Timestamp">When the cast completed.</param>
/// <param name="Target">The enemy the cast named.</param>
/// <param name="TankStaggerFraction">The tank's Stagger as a share of its maximum health at the cast, or null when nothing within <see cref="StaggerTracker.StaggerMaxAgeMs"/> precedes it.</param>
/// <param name="CleanseAvailable">Whether Amend Fate or Restore Continuity was available at the cast with the tank alive.</param>
/// <param name="EffectiveHealing">Effective healing across the allies the cast healed.</param>
/// <param name="Overheal">Overheal across those allies.</param>
/// <param name="AlliesHealed">Allies the cast healed.</param>
/// <param name="ShieldApplied">Absorb the cast's Oblivion's Embrace shields applied, summed across allies.</param>
/// <param name="AlliesShielded">Allies that took a shield from this cast.</param>
/// <param name="Damage">Damage the cast dealt.</param>
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
/// Oblivion over one pull: every cast with what it produced, the casts made while the tank was at cleanse
/// priority, and the same figures grouped by the enemy each cast was made into.
/// </summary>
/// <remarks>
/// A cast's heals, shields, and damage arrive on the next millisecond, so each is credited to the most
/// recent cast within <see cref="AttributionMs"/>.
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<StaggerTracker>]
[Dependency<FreeCastTracker>]
[Dependency<SpellUsable>]
public sealed partial class OblivionAnalyzer : Analyzer, IOblivionAnalyzer
{
    /// <summary>The share of the tank's maximum health in Stagger past which a cleanse outranks Oblivion.</summary>
    public const double CleansePriorityStaggerFraction = 0.40;

    /// <summary>Milliseconds after a cast within which its heals, shields, and damage are credited to it.</summary>
    public const int AttributionMs = 50;

    private readonly List<CastState> _casts = [];

    /// <summary>Every Oblivion cast in the pull, in cast order.</summary>
    public IReadOnlyList<OblivionCast> Casts => field ??= [.. _casts.Select(Build)];

    /// <summary>Oblivion casts in the pull.</summary>
    public int CastCount => _casts.Count;

    /// <summary>Casts that could be rated.</summary>
    public int CastsRated => Casts.Count(cast => cast.Rated);

    /// <summary>Casts at cleanse priority.</summary>
    public int CastsAtCleansePriority => Casts.Count(cast => cast.AtCleansePriority);

    /// <summary>Effective healing across every cast.</summary>
    public long EffectiveHealing => Casts.Sum(cast => cast.EffectiveHealing);

    /// <summary>Overheal across every cast.</summary>
    public long Overheal => Casts.Sum(cast => cast.Overheal);

    /// <summary>Damage across every cast.</summary>
    public long Damage => Casts.Sum(cast => cast.Damage);

    /// <summary>Whether the build has Oblivion's Embrace.</summary>
    public bool OblivionsEmbraceTalented => Owner.SelectedCombatant.HasTalent(AeonaTalents.OblivionsEmbrace);

    /// <summary>Absorb the shields applied across every cast. Null without Oblivion's Embrace.</summary>
    public long? ShieldApplied => OblivionsEmbraceTalented ? Casts.Sum(cast => cast.ShieldApplied) : null;

    /// <summary>Casts that cost no Chrona.</summary>
    public int FreeCasts => Casts.Count(cast => cast.WasFree);

    /// <summary>Uchronia and Epoch Break windows opened inside the pull.</summary>
    public int FreeCastOpportunities => FreeCastTracker.OpportunitiesBetween(Pull.StartTime, Pull.EndTime);

    /// <summary>Every enemy Oblivion was cast into, most casts first.</summary>
    public IReadOnlyList<OblivionTarget> Targets => field ??= BuildTargets();

    /// <summary>Effective healing per cast.</summary>
    public double EffectiveHealingPerCast => _casts.Count == 0 ? 0 : (double)EffectiveHealing / _casts.Count;

    /// <summary>Shield absorb applied per cast. Null without Oblivion's Embrace.</summary>
    public double? ShieldAppliedPerCast =>
        ShieldApplied is { } applied && _casts.Count > 0 ? (double)applied / _casts.Count : null;

    /// <summary>Damage per cast.</summary>
    public double DamagePerCast => _casts.Count == 0 ? 0 : (double)Damage / _casts.Count;

    /// <summary>Effective healing plus shield absorb applied, per cast. Null with no cast.</summary>
    public double? ValuePerCast =>
        _casts.Count == 0 ? null : (EffectiveHealing + (ShieldApplied ?? 0)) / (double)_casts.Count;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Oblivion))]
    private void OnCast(CastEvent e)
    {
        var tankAlive = StaggerTracker.TankId is { } tank && StaggerTracker.IsAlive(tank, e.Timestamp);
        var cleanseReady = SpellUsable.IsAvailable(Spells.AmendFate.FSLID)
            || SpellUsable.IsAvailable(Spells.RestoreContinuity.FSLID);

        _casts.Add(new CastState(e.Timestamp, new UnitKey(e.TargetId, e.TargetInstance ?? 0), tankAlive && cleanseReady));
    }

    [On<HealEvent>(By = Actor.Player, Spell = nameof(Spells.Oblivion))]
    private void OnHeal(HealEvent e)
    {
        if (Current(e.Timestamp) is not { } cast) return;

        cast.EffectiveHealing += e.Amount;
        cast.Overheal += e.Overheal ?? 0;
        cast.AlliesHealed++;
    }

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.OblivionAbsorbAbsorb))]
    private void OnShieldApplied(ApplyBuffEvent e) => Shield(e.Timestamp, e.Absorb ?? 0);

    [On<RefreshBuffEvent>(By = Actor.Player, Spell = nameof(Spells.OblivionAbsorbAbsorb))]
    private void OnShieldRefreshed(RefreshBuffEvent e) => Shield(e.Timestamp, e.Absorb ?? 0);

    [On<DamageEvent>(By = Actor.Player, Spells = [nameof(Spells.Oblivion), nameof(Spells.OblivionDamage)])]
    private void OnDamage(DamageEvent e)
    {
        if (Current(e.Timestamp) is not { } cast) return;

        cast.Damage += e.Amount;
    }

    private void Shield(int timestamp, long absorb)
    {
        if (Current(timestamp) is not { } cast) return;

        cast.ShieldApplied += absorb;
        cast.AlliesShielded++;
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
