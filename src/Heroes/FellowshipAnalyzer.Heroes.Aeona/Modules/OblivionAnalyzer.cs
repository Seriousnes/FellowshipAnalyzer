using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;
using FSLID = FellowshipAnalyzer.Core.Common.Spells.FSLID;
using SpellKind = FellowshipAnalyzer.Core.Common.Spells.SpellKind;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>The read surface every Oblivion measurement is exposed on.</summary>
public interface IOblivionAnalyzer : IAnalyzerSurface;

/// <summary>One ally's share of a single Oblivion cast.</summary>
/// <param name="UnitId">The ally.</param>
/// <param name="Healing">Effective healing this cast put on the ally.</param>
/// <param name="Shielding">Absorb the Oblivion's Embrace shield from this cast put on the ally, zero without the talent.</param>
public sealed record OblivionTargetShare(int UnitId, long Healing, long Shielding);

/// <summary>
/// One direct Oblivion cast: the damage it dealt, what it put on each ally, what made it free, and the
/// tank's Stagger at the moment it went out.
/// </summary>
/// <param name="Timestamp">When the cast completed.</param>
/// <param name="TargetId">The enemy the cast named.</param>
/// <param name="Targets">The allies the cast healed or shielded, in the order the log reported them.</param>
/// <param name="Damage">Damage the cast dealt, Erasure ticks excluded.</param>
/// <param name="LargestSingleAbsorb">The most any of this cast's shields absorbed from one incoming hit.</param>
/// <param name="FreeCastSource">What made this cast free, or null when it cost resources.</param>
/// <param name="TankStagger">The tank's pending Stagger immediately before the cast, in hit points, or null when nothing within <see cref="StaggerTracker.StaggerMaxAgeMs"/> precedes it.</param>
/// <param name="StaggerRemoved">The Stagger removed from one ally by Amend Fate or Restore Continuity, or null when the report holds no clean cleanse cast.</param>
/// <param name="CleanseAvailable">Whether Amend Fate or Restore Continuity had a charge ready at the cast, and the tank was alive to receive it.</param>
public sealed record OblivionCast(
    int Timestamp,
    int TargetId,
    IReadOnlyList<OblivionTargetShare> Targets,
    long Damage,
    long LargestSingleAbsorb,
    FreeCastSource? FreeCastSource,
    int? TankStagger,
    int? StaggerRemoved,
    bool CleanseAvailable)
{
    /// <summary>Whether Fellowship logged this cast as costing no resources.</summary>
    public bool WasFree => FreeCastSource is not null;

    /// <summary>Effective healing this cast put on the party.</summary>
    public long Healing => Targets.Sum(target => target.Healing);

    /// <summary>Absorb this cast's Oblivion's Embrace shields put on the party.</summary>
    public long Shielding => Targets.Sum(target => target.Shielding);

    /// <summary>Allies this cast healed or shielded.</summary>
    public int AlliesReached => Targets.Count;

    /// <summary>
    /// The Stagger past which a cleanse outranks Oblivion, twice <see cref="StaggerRemoved"/>.
    /// </summary>
    public int? CleansePriorityStagger => StaggerRemoved is { } removed ? removed * 2 : null;

    /// <summary>
    /// Whether the tank held at least <see cref="CleansePriorityStagger"/> and a cleanse was ready to
    /// take it off.
    /// </summary>
    public bool AtCleansePriority =>
        CleansePriorityStagger is { } threshold && TankStagger is { } stagger && stagger >= threshold && CleanseAvailable;

    /// <summary>
    /// Whether a free cast went into Oblivion while the tank held at least
    /// <see cref="StaggerRemoved"/> and a cleanse was ready.
    /// </summary>
    public bool FreeCastAboveStaggerRemoved =>
        WasFree
        && !AtCleansePriority
        && StaggerRemoved is { } removed
        && TankStagger is { } stagger
        && stagger >= removed
        && CleanseAvailable;

    /// <summary>Whether this cast could be rated.</summary>
    public bool Rated => TankStagger is not null && StaggerRemoved is not null;
}

/// <summary>
/// Measures Oblivion: what each direct cast dealt and put on each ally, what Erasure's ticks added on
/// top, whether the cast was free, and the tank's pending Stagger at the moment it went out.
/// <para>
/// Erasure ticks under its own effect rather than under Oblivion, so its damage and the healing that
/// arrives with a tick are counted apart from the direct casts'. A direct cast takes the damage,
/// healing and shielding that follow it within <see cref="CastAttributionWindowMs"/> onto its own row;
/// the pull totals take every direct hit whether or not a cast row claimed it.
/// </para>
/// <para>
/// One cast shields several party members at once, so the shield ledger inherited from
/// <see cref="AbsorbAnalyzer"/> keeps a separate entry per ally and <see cref="Casts"/> regroups those
/// entries under the cast that applied them. A shield applied by a cast in an earlier pull counts in
/// the pull totals and in no cast's row.
/// </para>
/// <para>
/// The shielding is Oblivion's Embrace, so it exists only in a build with that talent.
/// <see cref="OblivionsEmbraceTalented"/> says whether the build has it, and every shield total reads
/// null rather than zero without it.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<StaggerTracker>]
[Dependency<FreeCastTracker>]
[Dependency<SpellUsable>]
public sealed partial class OblivionAnalyzer : AbsorbAnalyzer, IOblivionAnalyzer
{
    /// <summary>How long after a cast its damage, healing and shielding may arrive and still belong to it.</summary>
    public const int CastAttributionWindowMs = 500;

    /// <summary>
    /// The effect Erasure's damage over time ticks under. Codex <c>effect 2735</c>, named "Erasure",
    /// applied by <c>ability 1958</c>; neither carries a spell-registry member because neither appears
    /// in <c>data/spelldb.json</c>.
    /// </summary>
    private const int ErasureEffectId = 2735;

    private static readonly FSLID Erasure = FSLID.FromNative(SpellKind.Effect, ErasureEffectId);

    private readonly List<CastRecord> _casts = [];

    private long _directDamage;
    private long _directHealing;
    private long _erasureDamage;
    private long _erasureHealing;
    private int _erasureTicks;
    private int? _directDamageAt;
    private int? _erasureTickAt;

    private CastLedger Ledger => field ??= BuildLedger();

    /// <inheritdoc/>
    /// <remarks>
    /// Every Oblivion cast applies a fresh shield, so one arriving on an ally who still has one is a
    /// separate entry with its own absorb.
    /// </remarks>
    protected override bool ReapplicationOpensNewAbsorb => true;

    /// <summary>Every direct Oblivion cast this pull, in cast order.</summary>
    public IReadOnlyList<OblivionCast> Casts => Ledger.Casts;

    /// <summary>Direct Oblivion casts this pull.</summary>
    public int CastCount => _casts.Count;

    /// <summary>Whether the build has Oblivion's Embrace.</summary>
    public bool OblivionsEmbraceTalented =>
        Owner.SelectedCombatant.HasTalent(AeonaTalents.OblivionsEmbrace);

    /// <summary>Whether the build has Erasure.</summary>
    public bool ErasureTalented => Owner.SelectedCombatant.HasTalent(AeonaTalents.Erasure);

    /// <summary>Damage the direct casts dealt this pull.</summary>
    public long DirectDamage => _directDamage;

    /// <summary>
    /// <see cref="DirectDamage"/> averaged over the pull's direct casts. Null with no cast to average
    /// over.
    /// </summary>
    public double? DamagePerCast => _casts.Count > 0 ? (double)_directDamage / _casts.Count : null;

    /// <summary>Effective healing the direct casts put on the party this pull.</summary>
    public long Healing => _directHealing;

    /// <summary>
    /// <see cref="Healing"/> averaged over the pull's direct casts. Null with no cast to average over.
    /// </summary>
    public double? HealingPerCast => _casts.Count > 0 ? (double)_directHealing / _casts.Count : null;

    /// <summary>Erasure ticks this pull.</summary>
    public int ErasureTicks => _erasureTicks;

    /// <summary>Damage Erasure's ticks dealt this pull.</summary>
    public long ErasureDamage => _erasureDamage;

    /// <summary>Effective healing Erasure's ticks put on the party this pull.</summary>
    public long ErasureHealing => _erasureHealing;

    /// <summary>
    /// Absorb the Oblivion's Embrace shields applied this pull put on the party. Null without
    /// Oblivion's Embrace, where the shield does not exist.
    /// </summary>
    public long? ShieldApplied => OblivionsEmbraceTalented ? Ledger.Applied : null;

    /// <summary>
    /// <see cref="ShieldApplied"/> averaged over the pull's direct casts. Null without Oblivion's
    /// Embrace, or with no cast to average over.
    /// </summary>
    public double? ShieldAppliedPerCast =>
        OblivionsEmbraceTalented && _casts.Count > 0 ? (double)Ledger.Applied / _casts.Count : null;

    /// <summary>
    /// The most any Oblivion's Embrace shield absorbed from one incoming hit this pull. Null without
    /// Oblivion's Embrace.
    /// </summary>
    public long? LargestSingleAbsorb => OblivionsEmbraceTalented ? Ledger.LargestSingleAbsorb : null;

    /// <summary>Oblivion casts Fellowship logged as costing no resources.</summary>
    public int FreeCasts => Ledger.Casts.Count(cast => cast.WasFree);

    /// <summary>Free casts the player made this pull, whichever ability each went into.</summary>
    public int FreeCastsMade => FreeCastTracker.FreeCastsBetween(Pull.StartTime, Pull.EndTime).Count;

    /// <summary>
    /// Chances to make a free cast the pull offered: every Uchronia window and every Epoch Break window
    /// that opened inside it, whichever ability each was eventually spent on.
    /// </summary>
    public int FreeCastOpportunities => FreeCastTracker.OpportunitiesBetween(Pull.StartTime, Pull.EndTime);

    /// <summary>
    /// The Stagger removed from one ally by Amend Fate or Restore Continuity, the larger of what the
    /// two abilities take off. Null when the report holds no clean cleanse cast.
    /// </summary>
    public int? StaggerRemoved
    {
        get
        {
            var amendFate = StaggerTracker.StaggerRemoved(Spells.AmendFate.FSLID);
            var restoreContinuity = StaggerTracker.StaggerRemoved(Spells.RestoreContinuity.FSLID);

            return amendFate is { } amend && restoreContinuity is { } restore
                ? Math.Max(amend, restore)
                : amendFate ?? restoreContinuity;
        }
    }

    /// <summary>
    /// The Stagger past which a cleanse outranks Oblivion, twice <see cref="StaggerRemoved"/>.
    /// </summary>
    public int? CleansePriorityStagger => StaggerRemoved is { } removed ? removed * 2 : null;

    /// <summary>
    /// Oblivion casts while the tank held at least <see cref="CleansePriorityStagger"/> and a cleanse
    /// was ready. Read it against <see cref="CastsRated"/>.
    /// </summary>
    public int CastsAtCleansePriority => Ledger.Casts.Count(cast => cast.AtCleansePriority);

    /// <summary>
    /// Free casts spent on Oblivion while the tank held at least <see cref="StaggerRemoved"/> and a
    /// cleanse was ready.
    /// </summary>
    public int FreeCastsAboveStaggerRemoved => Ledger.Casts.Count(cast => cast.FreeCastAboveStaggerRemoved);

    /// <summary>Oblivion casts that could be rated.</summary>
    public int CastsRated => Ledger.Casts.Count(cast => cast.Rated);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Oblivion))]
    private void OnOblivionCast(CastEvent e) => RecordCast(e.Timestamp, e.TargetId);

    [On<FreeCastEvent>(By = Actor.Player, Spell = nameof(Spells.Oblivion))]
    private void OnFreeOblivionCast(FreeCastEvent e) => RecordCast(e.Timestamp, e.TargetId);

    [On<DamageEvent>(By = Actor.Player, Spells = [nameof(Spells.Oblivion), nameof(Spells.OblivionDamage)])]
    private void OnOblivionDamage(DamageEvent e)
    {
        _directDamageAt = e.Timestamp;
        _directDamage += e.Amount;

        if (CastAt(e.Timestamp) is { } cast) cast.Damage += e.Amount;
    }

    [On<DamageEvent>(By = Actor.Player)]
    private void OnErasureDamage(DamageEvent e)
    {
        if (e.Ability?.Id != Erasure) return;

        _erasureTicks++;
        _erasureDamage += e.Amount;
        _erasureTickAt = e.Timestamp;
    }

    [On<HealEvent>(By = Actor.Player, Spells = [nameof(Spells.Oblivion), nameof(Spells.OblivionDamage)])]
    private void OnOblivionHeal(HealEvent e)
    {
        if (_erasureTickAt == e.Timestamp && _directDamageAt != e.Timestamp)
        {
            _erasureHealing += e.Amount;
            return;
        }

        _directHealing += e.Amount;

        if (CastAt(e.Timestamp) is { } cast) cast.AddHealing(e.TargetId, e.Amount);
    }

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.OblivionAbsorbAbsorb))]
    private void OnShieldApplied(ApplyBuffEvent e) => OpenShield(e, e.Absorb ?? 0);

    [On<RefreshBuffEvent>(By = Actor.Player, Spell = nameof(Spells.OblivionAbsorbAbsorb))]
    private void OnShieldRefreshed(RefreshBuffEvent e) => OpenShield(e, e.Absorb ?? 0);

    [On<RemoveBuffEvent>(By = Actor.Player, Spell = nameof(Spells.OblivionAbsorbAbsorb))]
    private void OnShieldRemoved(RemoveBuffEvent e) => CloseShield(e);

    [On<AbsorbedEvent>(By = Actor.Player, Spell = nameof(Spells.OblivionAbsorbAbsorb))]
    private void OnShieldAbsorbed(AbsorbedEvent e) => RecordAbsorbed(e);

    /// <summary>
    /// Records the cast with the cleanse opportunity sampled now, because <see cref="SpellUsable"/>
    /// answers for the dispatch instant alone.
    /// </summary>
    private void RecordCast(int timestamp, int targetId)
    {
        var tankAlive = StaggerTracker.TankId is { } tank && StaggerTracker.IsAlive(tank, timestamp);
        var cleanseReady = SpellUsable.IsAvailable(Spells.AmendFate.FSLID)
            || SpellUsable.IsAvailable(Spells.RestoreContinuity.FSLID);

        _casts.Add(new CastRecord(timestamp, targetId, tankAlive && cleanseReady));
    }

    /// <summary>The cast <paramref name="timestamp"/> falls inside, or null when none does.</summary>
    private CastRecord? CastAt(int timestamp)
    {
        if (_casts.Count == 0) return null;

        var record = _casts[^1];
        return timestamp >= record.Timestamp && timestamp - record.Timestamp <= CastAttributionWindowMs
            ? record
            : null;
    }

    private CastLedger BuildLedger()
    {
        long applied = 0, largestSingleAbsorb = 0;
        var castIndex = 0;

        foreach (var shield in Absorbs)
        {
            applied += shield.Total;

            foreach (var hit in shield.Hits)
            {
                if (hit.Amount > largestSingleAbsorb) largestSingleAbsorb = hit.Amount;
            }

            while (castIndex + 1 < _casts.Count && _casts[castIndex + 1].Timestamp <= shield.Start)
                castIndex++;

            if (_casts.Count == 0
                || _casts[castIndex].Timestamp > shield.Start
                || shield.Start - _casts[castIndex].Timestamp > CastAttributionWindowMs)
            {
                continue;
            }

            var cast = _casts[castIndex];
            cast.AddShielding(shield.Target.ActorId, shield.Total);

            foreach (var hit in shield.Hits)
            {
                if (hit.Amount > cast.LargestSingleAbsorb) cast.LargestSingleAbsorb = hit.Amount;
            }
        }

        var tankId = StaggerTracker.TankId;
        var staggerRemoved = StaggerRemoved;
        var casts = new List<OblivionCast>(_casts.Count);

        foreach (var record in _casts)
        {
            casts.Add(new OblivionCast(
                record.Timestamp,
                record.TargetId,
                [.. record.Units.Select(record.ShareOf)],
                record.Damage,
                record.LargestSingleAbsorb,
                FreeCastTracker.FreeCastAt(record.Timestamp, Spells.Oblivion.FSLID)?.Source,
                TankStaggerAt(tankId, record.Timestamp),
                staggerRemoved,
                record.CleanseAvailable));
        }

        return new CastLedger(casts, applied, largestSingleAbsorb);
    }

    private int? TankStaggerAt(int? tankId, int timestamp)
    {
        if (tankId is not { } tank) return null;
        if (StaggerTracker.LatestBefore(tank, timestamp) is not { } snapshot) return null;

        return timestamp - snapshot.Timestamp <= StaggerTracker.StaggerMaxAgeMs ? snapshot.Amount : null;
    }

    private sealed class CastRecord(int timestamp, int targetId, bool cleanseAvailable)
    {
        private readonly List<int> _units = [];
        private readonly Dictionary<int, long> _healing = [];
        private readonly Dictionary<int, long> _shielding = [];

        public int Timestamp { get; } = timestamp;

        public int TargetId { get; } = targetId;

        public bool CleanseAvailable { get; } = cleanseAvailable;

        public long Damage { get; set; }

        public long LargestSingleAbsorb { get; set; }

        public IReadOnlyList<int> Units => _units;

        public void AddHealing(int unitId, long amount)
        {
            Track(unitId);
            _healing[unitId] = _healing.GetValueOrDefault(unitId) + amount;
        }

        public void AddShielding(int unitId, long amount)
        {
            Track(unitId);
            _shielding[unitId] = _shielding.GetValueOrDefault(unitId) + amount;
        }

        public OblivionTargetShare ShareOf(int unitId) =>
            new(unitId, _healing.GetValueOrDefault(unitId), _shielding.GetValueOrDefault(unitId));

        private void Track(int unitId)
        {
            if (!_units.Contains(unitId)) _units.Add(unitId);
        }
    }

    private sealed record CastLedger(
        List<OblivionCast> Casts,
        long Applied,
        long LargestSingleAbsorb);
}
