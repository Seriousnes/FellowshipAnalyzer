using FellowshipAnalyzer.Core.Events;
using OneOf;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks the selected player's stat ratings over the course of a fight.
/// Monitors buff/debuff stack changes to keep rating totals up to date and
/// fabricates <see cref="ChangeStatsEvent"/> whenever stats change.
/// Also tracks the two cooldown stat pools, Ability Cooldown Reduction and Cooldown Acceleration:
/// each starts from the gear-derived seed frozen on <see cref="CombatantStats"/> and accrues runtime
/// <see cref="CooldownModifier"/>s, fabricating <see cref="ChangeCooldownModifierEvent"/> on every change.
/// </summary>
/// <remarks>
/// Rating → percentage conversion uses Fellowship's piecewise diminishing-returns
/// formula (CombatMechanics.md). All secondary stats share the same curve.
/// Critical Strike has an additional 5% base chance added after DR.
/// </remarks>
public sealed partial class StatTracker(Lazy<Combatants> combatants) : Analyzer
{
    private PlayerStats _currentStats = new();
    private PlayerStats _pullStats = new();
    private readonly PlayerMultipliers _multipliers = new();

    private readonly Dictionary<int, StatBuff> _statBuffs = [];

    private readonly Dictionary<int, StatMultiplierBuff> _statMultiplierBuffs = [];

    private readonly Dictionary<int, CooldownBuff> _cooldownBuffs = [];

    private readonly List<CooldownModifier> _abilityCooldownReduction = [];

    private readonly List<CooldownModifier> _cooldownAcceleration = [];

    /// <summary>5% base critical strike chance, added after diminishing returns.</summary>
    public const double BaseCritChance = 0.05;

    [On<FightStartEvent>]
    private void OnFightStart(FightStartEvent _)
    {
        var stats = _combatants.Selected.Stats;

        _pullStats = new PlayerStats
        {
            Intellect = stats.Intellect,
            Stamina = stats.Stamina,
            Armor = stats.Armor,
            Crit = stats.Crit,
            Haste = stats.Haste,
            Expertise = stats.Expertise,
            Spirit = stats.Spirit,
        };
        _currentStats = _pullStats.Clone();
    }

    /// <summary>
    /// Registers a stat rating buff so StatTracker will track it when active.
    /// </summary>
    public void Add(int spellId, StatBuff buff) => _statBuffs[spellId] = buff;

    /// <summary>
    /// Registers a stat multiplier buff (e.g., 5% increased Intellect).
    /// </summary>
    public void AddMultiplier(int spellId, StatMultiplierBuff multiplier) =>
        _statMultiplierBuffs[spellId] = multiplier;

    /// <summary>
    /// Registers a cooldown stat buff: while the buff is active on the player its modifiers join the
    /// tracked pools, and they leave when the buff drops. Unlike rating buffs, prepull applications
    /// still take effect because the gear-derived seed contains no temporary buff contributions.
    /// </summary>
    public void AddCooldownBuff(int spellId, CooldownBuff buff) => _cooldownBuffs[spellId] = buff;

    /// <summary>
    /// Total Ability Cooldown Reduction for <paramref name="ability"/> as a fraction (0.12 = 12%), summing
    /// the gear-derived seed frozen on <see cref="CombatantStats"/> and every tracked runtime modifier that
    /// applies to it, capped at 1.0 so a cooldown can be erased but never driven below zero. A <c>null</c>
    /// ability accrues only unscoped modifiers. ACR is snapshot semantics: <see cref="SpellUsable"/> reads
    /// this when a cooldown starts and never rescales one in flight.
    /// </summary>
    public double CurrentAbilityCooldownReduction(SpellbookAbility? ability) =>
        Math.Min(1.0, Owner.SelectedCombatant.Stats.AbilityCooldownReduction.Total(ability)
            + TotalOf(_abilityCooldownReduction, ability));

    /// <summary>
    /// Total Cooldown Acceleration for <paramref name="ability"/> as a fraction (0.10 = +10%), summing the
    /// gear-derived seed frozen on <see cref="CombatantStats"/> and every tracked runtime modifier that
    /// applies to it. Uncapped: each value is a term on the shared recovery pool
    /// <see cref="SpellUsable.EffectiveRate"/> divides by. A <c>null</c> ability accrues only unscoped
    /// modifiers.
    /// </summary>
    public double CurrentCooldownAcceleration(SpellbookAbility? ability) =>
        Owner.SelectedCombatant.Stats.CooldownAcceleration.Total(ability)
            + TotalOf(_cooldownAcceleration, ability);

    /// <summary>
    /// Ability Cooldown Reduction for <paramref name="ability"/> from the gear-derived seed alone, capped
    /// at 1.0: the value in force at the pull before any runtime modifier was tracked.
    /// </summary>
    public double StartingAbilityCooldownReduction(SpellbookAbility? ability) =>
        Math.Min(1.0, Owner.SelectedCombatant.Stats.AbilityCooldownReduction.Total(ability));

    /// <summary>
    /// Cooldown Acceleration for <paramref name="ability"/> from the gear-derived seed alone: the value in
    /// force at the pull before any runtime modifier was tracked.
    /// </summary>
    public double StartingCooldownAcceleration(SpellbookAbility? ability) =>
        Owner.SelectedCombatant.Stats.CooldownAcceleration.Total(ability);

    /// <summary>
    /// Scales a cooldown duration by <paramref name="ability"/>'s current Ability Cooldown Reduction: at
    /// 10% ACR a 30s cooldown becomes 27s. This applies to both base cooldowns at cast and flat cooldown
    /// reductions, so at 10% ACR a 1000ms Rolling Flames reduction generates 900ms.
    /// </summary>
    public int ScaleByCooldownReduction(SpellbookAbility? ability, int milliseconds) =>
        (int)(milliseconds * (1.0 - CurrentAbilityCooldownReduction(ability)));

    /// <summary>
    /// Adds a runtime modifier to the given cooldown stat pool and fabricates a
    /// <see cref="ChangeCooldownModifierEvent"/> describing the change. The pool is mutated before the
    /// event is fabricated so subscribers observe the new totals. Pools are additive: each modifier
    /// contributes its value rather than multiplying with the others.
    /// </summary>
    public void AddCooldownModifier(CooldownPool pool, CooldownModifier modifier, Event? trigger = null, int? timestamp = null)
    {
        PoolOf(pool).Add(modifier);
        FabricateChangeCooldownModifier(pool, modifier, added: true, trigger, timestamp);
    }

    /// <summary>
    /// Removes one occurrence of a previously added runtime modifier from the given cooldown stat pool and
    /// fabricates a <see cref="ChangeCooldownModifierEvent"/> describing the change. A modifier that is not
    /// present is a no-op and fabricates nothing, so a stray or duplicate removal cannot drive a pool
    /// negative.
    /// </summary>
    public void RemoveCooldownModifier(CooldownPool pool, CooldownModifier modifier, Event? trigger = null, int? timestamp = null)
    {
        if (!PoolOf(pool).Remove(modifier)) return;
        FabricateChangeCooldownModifier(pool, modifier, added: false, trigger, timestamp);
    }

    private List<CooldownModifier> PoolOf(CooldownPool pool) => pool switch
    {
        CooldownPool.AbilityCooldownReduction => _abilityCooldownReduction,
        CooldownPool.CooldownAcceleration => _cooldownAcceleration,
        _ => throw new ArgumentOutOfRangeException(nameof(pool), pool, null),
    };

    private static double TotalOf(List<CooldownModifier> modifiers, SpellbookAbility? ability)
    {
        var total = 0.0;
        foreach (var modifier in modifiers)
        {
            if (modifier.Scope is null || (ability is not null && modifier.Scope.Matches(ability)))
                total += modifier.Value;
        }
        return total;
    }

    private void FabricateChangeCooldownModifier(CooldownPool pool, CooldownModifier modifier, bool added, Event? trigger, int? timestamp) =>
        Owner.EventEmitter.FabricateEvent(new ChangeCooldownModifierEvent
        {
            Timestamp = timestamp ?? trigger?.Timestamp ?? 0,
            SourceId = Owner.PlayerId,
            TargetId = Owner.PlayerId,
            Pool = pool,
            Modifier = modifier,
            Added = added,
        }, trigger);

    public double CurrentIntellect => _currentStats.Intellect;
    public double CurrentStamina => _currentStats.Stamina;
    public double CurrentArmor => _currentStats.Armor;
    public double CurrentCritRating => _currentStats.Crit;
    public double CurrentHasteRating => _currentStats.Haste;
    public double CurrentExpertiseRating => _currentStats.Expertise;
    public double CurrentSpiritRating => _currentStats.Spirit;

    public double StartingCritRating => _pullStats.Crit;
    public double StartingHasteRating => _pullStats.Haste;
    public double StartingExpertiseRating => _pullStats.Expertise;
    public double StartingSpiritRating => _pullStats.Spirit;
    public double StartingIntellect => _pullStats.Intellect;

    /// <summary>
    /// Converts a stat rating to a percentage using Fellowship's Season 3 diminishing-returns
    /// formula. The raw percentage is <c>rating × 0.16</c>; diminishing returns then apply a
    /// per-band multiplier to each 5-point band of that raw percentage (no reduction below 10%,
    /// then ×0.98, ×0.96, ×0.94, and ×0.92 for everything past 25%). Returns a decimal fraction
    /// (0.30 = 30%). Flat percentage bonuses from static effects (e.g. +5% haste) are additive
    /// after this and are not modelled here.
    /// </summary>
    public static double RatingToPercentage(double rating)
    {
        if (rating <= 0) return 0;

        var raw = rating * 0.16;
        var pct =
            Math.Min(raw, 10.0)
            + Math.Clamp(raw - 10.0, 0.0, 5.0) * 0.98
            + Math.Clamp(raw - 15.0, 0.0, 5.0) * 0.96
            + Math.Clamp(raw - 20.0, 0.0, 5.0) * 0.94
            + Math.Max(raw - 25.0, 0.0) * 0.92;

        return pct / 100.0;
    }

    /// <summary>
    /// Returns the crit percentage for a given rating.
    /// When <paramref name="withBase"/> is true, includes the 5% base crit chance.
    /// </summary>
    public double CritPercentage(double rating, bool withBase = false) =>
        (withBase ? BaseCritChance : 0.0) + RatingToPercentage(rating);

    public double HastePercentage(double rating) => RatingToPercentage(rating);
    public double ExpertisePercentage(double rating) => RatingToPercentage(rating);
    public double SpiritPercentage(double rating) => RatingToPercentage(rating);

    public double CurrentCritPercentage => CritPercentage(CurrentCritRating, withBase: true);
    public double CurrentHastePercentage => HastePercentage(CurrentHasteRating);
    public double CurrentExpertisePercentage => ExpertisePercentage(CurrentExpertiseRating);
    public double CurrentSpiritPercentage => SpiritPercentage(CurrentSpiritRating);

    /// <summary>
    /// Forces an immediate stat change. Use only for buffs that cannot be described
    /// by the standard <see cref="StatBuff"/> registration mechanism.
    /// </summary>
    public void ForceChangeStats(StatBuff change, Event? trigger)
    {
        var before = _currentStats.ToStats();
        ApplyRatingBuff(change, 1.0, withMultipliers: false);
        var after = _currentStats.ToStats();
        FabricateChangeStats(trigger, before, after - before, after);
    }

    [On<ApplyBuffEvent>(To = Actor.Player)]
    private void OnApplyBuff(ApplyBuffEvent e) => HandleBuffGain(e.Ability.FSLID, e.Prepull.GetValueOrDefault(), e);

    [On<RemoveBuffEvent>(To = Actor.Player)]
    private void OnRemoveBuff(RemoveBuffEvent e) => HandleBuffLoss(e.Ability.FSLID, e);

    [On<ApplyBuffStackEvent>(To = Actor.Player)]
    private void OnApplyBuffStack(ApplyBuffStackEvent e) => HandleBuffGain(e.Ability.FSLID, isPrepull: false, e);

    [On<RemoveBuffStackEvent>(To = Actor.Player)]
    private void OnRemoveBuffStack(RemoveBuffStackEvent e) => HandleBuffLoss(e.Ability.FSLID, e);

    [On<ApplyDebuffEvent>(To = Actor.Player)]
    private void OnApplyDebuff(ApplyDebuffEvent e) => HandleBuffGain(e.Ability.FSLID, e.Prepull.GetValueOrDefault(), e);

    [On<RemoveDebuffEvent>(To = Actor.Player)]
    private void OnRemoveDebuff(RemoveDebuffEvent e) => HandleBuffLoss(e.Ability.FSLID, e);

    [On<ApplyDebuffStackEvent>(To = Actor.Player)]
    private void OnApplyDebuffStack(ApplyDebuffStackEvent e) => HandleBuffGain(e.Ability.FSLID, isPrepull: false, e);

    [On<RemoveDebuffStackEvent>(To = Actor.Player)]
    private void OnRemoveDebuffStack(RemoveDebuffStackEvent e) => HandleBuffLoss(e.Ability.FSLID, e);

    private void HandleBuffGain(int spellId, bool isPrepull, Event trigger)
    {
        if (!isPrepull && _statBuffs.TryGetValue(spellId, out var ratingBuff))
        {
            var before = _currentStats.ToStats();
            ApplyRatingBuff(ratingBuff, 1.0);
            var after = _currentStats.ToStats();
            FabricateChangeStats(trigger, before, after - before, after);
        }

        if (_statMultiplierBuffs.TryGetValue(spellId, out var multBuff))
        {
            if (isPrepull)
            {
                UpdateMultiplierState(multBuff, isGaining: true);
            }
            else
            {
                ApplyMultiplierBuff(multBuff, isGaining: true, trigger);
            }
        }

        if (_cooldownBuffs.TryGetValue(spellId, out var cooldownBuff))
        {
            if (cooldownBuff.AbilityCooldownReduction is { } acr)
                AddCooldownModifier(CooldownPool.AbilityCooldownReduction, acr, trigger);
            if (cooldownBuff.CooldownAcceleration is { } cda)
                AddCooldownModifier(CooldownPool.CooldownAcceleration, cda, trigger);
        }
    }

    private void HandleBuffLoss(int spellId, Event trigger)
    {
        if (_statBuffs.TryGetValue(spellId, out var ratingBuff))
        {
            var before = _currentStats.ToStats();
            ApplyRatingBuff(ratingBuff, -1.0);
            var after = _currentStats.ToStats();
            FabricateChangeStats(trigger, before, after - before, after);
        }

        if (_statMultiplierBuffs.TryGetValue(spellId, out var multBuff))
        {
            ApplyMultiplierBuff(multBuff, isGaining: false, trigger);
        }

        if (_cooldownBuffs.TryGetValue(spellId, out var cooldownBuff))
        {
            if (cooldownBuff.AbilityCooldownReduction is { } acr)
                RemoveCooldownModifier(CooldownPool.AbilityCooldownReduction, acr, trigger);
            if (cooldownBuff.CooldownAcceleration is { } cda)
                RemoveCooldownModifier(CooldownPool.CooldownAcceleration, cda, trigger);
        }
    }

    private void ApplyRatingBuff(StatBuff buff, double factor, bool withMultipliers = true)
    {
        _currentStats.Intellect += ResolveBuffVal(buff, buff.Intellect) * factor * (withMultipliers ? _multipliers.Intellect : 1.0);
        _currentStats.Stamina += ResolveBuffVal(buff, buff.Stamina) * factor * (withMultipliers ? _multipliers.Stamina : 1.0);
        _currentStats.Armor += ResolveBuffVal(buff, buff.Armor) * factor * (withMultipliers ? _multipliers.Armor : 1.0);
        _currentStats.Crit += ResolveBuffVal(buff, buff.Crit) * factor * (withMultipliers ? _multipliers.Crit : 1.0);
        _currentStats.Haste += ResolveBuffVal(buff, buff.Haste) * factor * (withMultipliers ? _multipliers.Haste : 1.0);
        _currentStats.Expertise += ResolveBuffVal(buff, buff.Expertise) * factor * (withMultipliers ? _multipliers.Expertise : 1.0);
        _currentStats.Spirit += ResolveBuffVal(buff, buff.Spirit) * factor * (withMultipliers ? _multipliers.Spirit : 1.0);
    }

    /// <summary>
    /// Updates the multiplier state AND scales current stats (for buffs gained/lost mid-fight).
    /// </summary>
    private void ApplyMultiplierBuff(StatMultiplierBuff buff, bool isGaining, Event trigger)
    {
        var before = _currentStats.ToStats();
        UpdateMultiplierState(buff, isGaining);
        ScaleStatsByMultiplier(buff, isGaining);
        var after = _currentStats.ToStats();
        FabricateChangeStats(trigger, before, after - before, after);
    }

    private void UpdateMultiplierState(StatMultiplierBuff buff, bool isGaining)
    {
        double Factor(double m) => isGaining ? m : 1.0 / m;

        if (buff.Intellect is double iMult) _multipliers.Intellect *= Factor(iMult);
        if (buff.Stamina is double sMult) _multipliers.Stamina *= Factor(sMult);
        if (buff.Armor is double aMult) _multipliers.Armor *= Factor(aMult);
        if (buff.Crit is double cMult) _multipliers.Crit *= Factor(cMult);
        if (buff.Haste is double hMult) _multipliers.Haste *= Factor(hMult);
        if (buff.Expertise is double eMult) _multipliers.Expertise *= Factor(eMult);
        if (buff.Spirit is double spMult) _multipliers.Spirit *= Factor(spMult);
    }

    private void ScaleStatsByMultiplier(StatMultiplierBuff buff, bool isGaining)
    {
        double Factor(double m) => isGaining ? m : 1.0 / m;

        if (buff.Intellect is double iMult) _currentStats.Intellect *= Factor(iMult);
        if (buff.Stamina is double sMult) _currentStats.Stamina *= Factor(sMult);
        if (buff.Armor is double aMult) _currentStats.Armor *= Factor(aMult);
        if (buff.Crit is double cMult) _currentStats.Crit *= Factor(cMult);
        if (buff.Haste is double hMult) _currentStats.Haste *= Factor(hMult);
        if (buff.Expertise is double eMult) _currentStats.Expertise *= Factor(eMult);
        if (buff.Spirit is double spMult) _currentStats.Spirit *= Factor(spMult);
    }

    private double ResolveBuffVal(StatBuff buffObj, BuffVal? buffVal)
    {
        if (buffVal is null) return 0.0;
        return buffVal.Match(
            value => value,
            func =>
            {
                Item? item = null;
                if (buffObj.ItemId is int itemId)
                    item = _combatants.Selected.GetItem(itemId);
                return func(_combatants.Selected, item);
            });
    }

    private void FabricateChangeStats(Event? trigger, Stats before, Stats delta, Stats after) =>
        Owner.EventEmitter.FabricateEvent(new ChangeStatsEvent
        {
            Timestamp = trigger?.Timestamp ?? 0,
            SourceId = Owner.PlayerId,
            TargetId = Owner.PlayerId,
            Before = before,
            Delta = delta,
            After = after,
        }, trigger);
}

// =============================================================================
// Supporting types
// =============================================================================

/// <summary>
/// A buff value that is either a fixed rating amount or a function that
/// derives the amount from the combatant (and optionally an item).
/// </summary>
[GenerateOneOf]
public partial class BuffVal : OneOfBase<double, Func<Combatant, Item?, double>>;

/// <summary>
/// Describes a stat rating buff. Unset fields contribute 0 to each stat.
/// Set <see cref="ItemId"/> when any stat value is item-level-dependent.
/// </summary>
public sealed class StatBuff
{
    public BuffVal? Intellect { get; init; }
    public BuffVal? Stamina { get; init; }
    public BuffVal? Armor { get; init; }
    public BuffVal? Crit { get; init; }
    public BuffVal? Haste { get; init; }
    public BuffVal? Expertise { get; init; }
    public BuffVal? Spirit { get; init; }

    /// <summary>Item ID to pass to function-based <see cref="BuffVal"/> callbacks.</summary>
    public int? ItemId { get; init; }
}

/// <summary>
/// Describes a cooldown stat buff: modifiers that join the tracked pools while the buff is active on
/// the player and leave when it drops. Unset pools are not modified.
/// </summary>
/// <param name="AbilityCooldownReduction">Modifier added to the Ability Cooldown Reduction pool.</param>
/// <param name="CooldownAcceleration">Modifier added to the Cooldown Acceleration pool.</param>
public sealed record CooldownBuff(
    CooldownModifier? AbilityCooldownReduction = null,
    CooldownModifier? CooldownAcceleration = null);

/// <summary>
/// Describes a multiplicative stat buff (e.g., 1.05 = +5%).
/// Unset fields are not modified. Values must be &gt; 0.
/// </summary>
public sealed class StatMultiplierBuff
{
    public double? Intellect { get; init; }
    public double? Stamina { get; init; }
    public double? Armor { get; init; }
    public double? Crit { get; init; }
    public double? Haste { get; init; }
    public double? Expertise { get; init; }
    public double? Spirit { get; init; }
}

/// <summary>Mutable snapshot of a player's stat ratings.</summary>
internal sealed class PlayerStats
{
    public double Intellect { get; set; }
    public double Stamina { get; set; }
    public double Armor { get; set; }
    public double Crit { get; set; }
    public double Haste { get; set; }
    public double Expertise { get; set; }
    public double Spirit { get; set; }

    public PlayerStats Clone() => (PlayerStats)MemberwiseClone();

    public Stats ToStats() => new()
    {
        Intellect = Intellect,
        Stamina = Stamina,
        Armor = Armor,
        Crit = Crit,
        Haste = Haste,
        Expertise = Expertise,
        Spirit = Spirit,
    };
}

/// <summary>Per-stat multiplier state. All values default to 1.0 (no multiplier).</summary>
internal sealed class PlayerMultipliers
{
    public double Intellect { get; set; } = 1.0;
    public double Stamina { get; set; } = 1.0;
    public double Armor { get; set; } = 1.0;
    public double Crit { get; set; } = 1.0;
    public double Haste { get; set; } = 1.0;
    public double Expertise { get; set; } = 1.0;
    public double Spirit { get; set; } = 1.0;
}
