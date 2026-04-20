using FellowshipAnalyzer.Core.Events;
using OneOf;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks the selected player's stat ratings over the course of a fight.
/// Monitors buff/debuff stack changes to keep rating totals up to date and
/// fabricates <see cref="ChangeStatsEvent"/> whenever stats change.
/// </summary>
/// <remarks>
/// Rating → percentage conversion uses Fellowship's piecewise diminishing-returns
/// formula (CombatMechanics.md). All secondary stats share the same curve.
/// Critical Strike has an additional 5% base chance added after DR.
/// </remarks>
public sealed class StatTracker : Analyzer
{
    private PlayerStats _currentStats = new();
    private PlayerStats _pullStats = new();
    private readonly PlayerMultipliers _multipliers = new();

    // Registered rating buffs: spellId → StatBuff
    private readonly Dictionary<int, StatBuff> _statBuffs = [];

    // Registered multiplier buffs: spellId → StatMultiplierBuff
    private readonly Dictionary<int, StatMultiplierBuff> _statMultiplierBuffs = [];

    // --- Base stat values ---
    /// <summary>5% base critical strike chance, added after diminishing returns.</summary>
    public const double BaseCritChance = 0.05;

    public override void Initialize()
    {
        var combatant = Owner.SelectedCombatant;
        if (combatant is null) return;

        _pullStats = new PlayerStats
        {
            Intellect = combatant.Intellect,
            Stamina   = combatant.Stamina,
            Armor     = combatant.Armor,
            Crit      = combatant.Crit,
            Haste     = combatant.Haste,
            Expertise = combatant.Expertise,
            Spirit    = combatant.Spirit,
        };
        _currentStats = _pullStats.Clone();

        AddEventListener(Events.ApplyBuff.To(SELECTED_PLAYER),          OnApplyBuff);
        AddEventListener(Events.RemoveBuff.To(SELECTED_PLAYER),         OnRemoveBuff);
        AddEventListener(Events.ApplyBuffStack.To(SELECTED_PLAYER),     OnApplyBuffStack);
        AddEventListener(Events.RemoveBuffStack.To(SELECTED_PLAYER),    OnRemoveBuffStack);
        AddEventListener(Events.ApplyDebuff.To(SELECTED_PLAYER),        OnApplyDebuff);
        AddEventListener(Events.RemoveDebuff.To(SELECTED_PLAYER),       OnRemoveDebuff);
        AddEventListener(Events.ApplyDebuffStack.To(SELECTED_PLAYER),   OnApplyDebuffStack);
        AddEventListener(Events.RemoveDebuffStack.To(SELECTED_PLAYER),  OnRemoveDebuffStack);
        AddEventListener(Events.Cast.By(SELECTED_PLAYER),               OnCast);
        AddEventListener(Events.Heal.To(SELECTED_PLAYER),               OnHeal);
    }

    // -------------------------------------------------------------------------
    // Registration API (for hero modules)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers a stat rating buff so StatTracker will track it when active.
    /// </summary>
    public void Add(int spellId, StatBuff buff) => _statBuffs[spellId] = buff;

    /// <summary>
    /// Registers a stat multiplier buff (e.g., 5% increased Intellect).
    /// </summary>
    public void AddMultiplier(int spellId, StatMultiplierBuff multiplier) =>
        _statMultiplierBuffs[spellId] = multiplier;

    // -------------------------------------------------------------------------
    // Rating accessors
    // -------------------------------------------------------------------------

    public double CurrentIntellect    => _currentStats.Intellect;
    public double CurrentStamina      => _currentStats.Stamina;
    public double CurrentArmor        => _currentStats.Armor;
    public double CurrentCritRating   => _currentStats.Crit;
    public double CurrentHasteRating  => _currentStats.Haste;
    public double CurrentExpertiseRating => _currentStats.Expertise;
    public double CurrentSpiritRating => _currentStats.Spirit;

    public double StartingCritRating      => _pullStats.Crit;
    public double StartingHasteRating     => _pullStats.Haste;
    public double StartingExpertiseRating => _pullStats.Expertise;
    public double StartingSpiritRating    => _pullStats.Spirit;
    public double StartingIntellect       => _pullStats.Intellect;

    // -------------------------------------------------------------------------
    // Rating → percentage conversion  (Fellowship piecewise DR)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Converts a stat rating to a percentage using Fellowship's piecewise
    /// diminishing-returns formula. Returns a decimal fraction (0.30 = 30%).
    /// Source: CombatMechanics.md / FellowBIS stats guide.
    /// </summary>
    public static double RatingToPercentage(double rating)
    {
        if (rating <= 0) return 0;

        const double t1 = 589, t2 = 898, t3 = 1242, t4 = 1647;
        const double r1 = 0.017, r2 = 0.01615, r3 = 0.014535, r4 = 0.01235475, r5 = 0.009901;

        double pct;
        if (rating <= t1)
            pct = rating * r1;
        else if (rating <= t2)
            pct = t1 * r1 + (rating - t1) * r2;
        else if (rating <= t3)
            pct = t1 * r1 + (t2 - t1) * r2 + (rating - t2) * r3;
        else if (rating <= t4)
            pct = t1 * r1 + (t2 - t1) * r2 + (t3 - t2) * r3 + (rating - t3) * r4;
        else
            pct = t1 * r1 + (t2 - t1) * r2 + (t3 - t2) * r3 + (t4 - t3) * r4 + (rating - t4) * r5;

        return pct / 100.0;
    }

    /// <summary>
    /// Returns the crit percentage for a given rating.
    /// When <paramref name="withBase"/> is true, includes the 5% base crit chance.
    /// </summary>
    public double CritPercentage(double rating, bool withBase = false) =>
        (withBase ? BaseCritChance : 0.0) + RatingToPercentage(rating);

    public double HastePercentage(double rating)     => RatingToPercentage(rating);
    public double ExpertisePercentage(double rating) => RatingToPercentage(rating);
    public double SpiritPercentage(double rating)    => RatingToPercentage(rating);

    // -------------------------------------------------------------------------
    // Current percentage getters
    // -------------------------------------------------------------------------

    public double CurrentCritPercentage      => CritPercentage(CurrentCritRating, withBase: true);
    public double CurrentHastePercentage     => HastePercentage(CurrentHasteRating);
    public double CurrentExpertisePercentage => ExpertisePercentage(CurrentExpertiseRating);
    public double CurrentSpiritPercentage    => SpiritPercentage(CurrentSpiritRating);

    // -------------------------------------------------------------------------
    // External stat forcing (for non-standard buffs)
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Event handlers
    // -------------------------------------------------------------------------

    private void OnApplyBuff(ApplyBuffEvent e)         => HandleBuffGain(e.Ability.Guid, e.Prepull.GetValueOrDefault(), e);
    private void OnRemoveBuff(RemoveBuffEvent e)        => HandleBuffLoss(e.Ability.Guid, e);
    private void OnApplyBuffStack(ApplyBuffStackEvent e)    => HandleBuffGain(e.Ability.Guid, isPrepull: false, e);
    private void OnRemoveBuffStack(RemoveBuffStackEvent e)  => HandleBuffLoss(e.Ability.Guid, e);
    private void OnApplyDebuff(ApplyDebuffEvent e)      => HandleBuffGain(e.Ability.Guid, e.Prepull.GetValueOrDefault(), e);
    private void OnRemoveDebuff(RemoveDebuffEvent e)    => HandleBuffLoss(e.Ability.Guid, e);
    private void OnApplyDebuffStack(ApplyDebuffStackEvent e)   => HandleBuffGain(e.Ability.Guid, isPrepull: false, e);
    private void OnRemoveDebuffStack(RemoveDebuffStackEvent e) => HandleBuffLoss(e.Ability.Guid, e);

    private void OnCast(CastEvent e)   => ValidateIntellect(e.SpellPower, e);
    private void OnHeal(HealEvent e)   => ValidateIntellect(e.SpellPower, e);

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private void HandleBuffGain(int spellId, bool isPrepull, Event trigger)
    {
        if (_statBuffs.TryGetValue(spellId, out var ratingBuff))
        {
            // Rating buffs are already factored into combatantinfo for prepull buffs.
            if (isPrepull) return;

            var before = _currentStats.ToStats();
            ApplyRatingBuff(ratingBuff, 1.0);
            var after = _currentStats.ToStats();
            FabricateChangeStats(trigger, before, after - before, after);
        }

        if (_statMultiplierBuffs.TryGetValue(spellId, out var multBuff))
        {
            if (isPrepull)
            {
                // Multiplied values are already in combatantinfo. Update the multiplier
                // state so future rating buffs scale correctly, but don't change current stats.
                UpdateMultiplierState(multBuff, isGaining: true);
                return;
            }
            ApplyMultiplierBuff(multBuff, isGaining: true, trigger);
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
    }

    /// <summary>
    /// Cast and Heal events include the caster's spell power (= intellect).
    /// Use this to self-correct the tracked intellect value if it drifts.
    /// </summary>
    private void ValidateIntellect(int spellPower, Event trigger)
    {
        if (spellPower <= 0) return; // Physical events carry 0

        var tracked = (int)_currentStats.Intellect;
        if (spellPower == tracked) return;

        var delta = spellPower - tracked;
        var before = _currentStats.ToStats();
        _currentStats.Intellect = spellPower;
        var after = _currentStats.ToStats();
        FabricateChangeStats(trigger, before, new Stats { Intellect = delta }, after);
    }

    private void ApplyRatingBuff(StatBuff buff, double factor, bool withMultipliers = true)
    {
        _currentStats.Intellect += ResolveBuffVal(buff, buff.Intellect) * factor * (withMultipliers ? _multipliers.Intellect : 1.0);
        _currentStats.Stamina   += ResolveBuffVal(buff, buff.Stamina)   * factor * (withMultipliers ? _multipliers.Stamina   : 1.0);
        _currentStats.Armor     += ResolveBuffVal(buff, buff.Armor)     * factor * (withMultipliers ? _multipliers.Armor     : 1.0);
        _currentStats.Crit      += ResolveBuffVal(buff, buff.Crit)      * factor * (withMultipliers ? _multipliers.Crit      : 1.0);
        _currentStats.Haste     += ResolveBuffVal(buff, buff.Haste)     * factor * (withMultipliers ? _multipliers.Haste     : 1.0);
        _currentStats.Expertise += ResolveBuffVal(buff, buff.Expertise) * factor * (withMultipliers ? _multipliers.Expertise : 1.0);
        _currentStats.Spirit    += ResolveBuffVal(buff, buff.Spirit)    * factor * (withMultipliers ? _multipliers.Spirit    : 1.0);
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

    /// <summary>
    /// Updates <see cref="_multipliers"/> only — used when the multiplied values are
    /// already reflected in combatantinfo (prepull buff scenario).
    /// </summary>
    private void UpdateMultiplierState(StatMultiplierBuff buff, bool isGaining)
    {
        double Factor(double m) => isGaining ? m : 1.0 / m;

        if (buff.Intellect is double iMult) _multipliers.Intellect *= Factor(iMult);
        if (buff.Stamina   is double sMult) _multipliers.Stamina   *= Factor(sMult);
        if (buff.Armor     is double aMult) _multipliers.Armor     *= Factor(aMult);
        if (buff.Crit      is double cMult) _multipliers.Crit      *= Factor(cMult);
        if (buff.Haste     is double hMult) _multipliers.Haste     *= Factor(hMult);
        if (buff.Expertise is double eMult) _multipliers.Expertise *= Factor(eMult);
        if (buff.Spirit    is double spMult) _multipliers.Spirit   *= Factor(spMult);
    }

    private void ScaleStatsByMultiplier(StatMultiplierBuff buff, bool isGaining)
    {
        double Factor(double m) => isGaining ? m : 1.0 / m;

        if (buff.Intellect is double iMult) _currentStats.Intellect *= Factor(iMult);
        if (buff.Stamina   is double sMult) _currentStats.Stamina   *= Factor(sMult);
        if (buff.Armor     is double aMult) _currentStats.Armor     *= Factor(aMult);
        if (buff.Crit      is double cMult) _currentStats.Crit      *= Factor(cMult);
        if (buff.Haste     is double hMult) _currentStats.Haste     *= Factor(hMult);
        if (buff.Expertise is double eMult) _currentStats.Expertise *= Factor(eMult);
        if (buff.Spirit    is double spMult) _currentStats.Spirit   *= Factor(spMult);
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
                    item = Owner.SelectedCombatant?.GetItem(itemId);
                return func(Owner.SelectedCombatant!, item);
            });
    }

    private void FabricateChangeStats(Event? trigger, Stats before, Stats delta, Stats after) =>
        Owner.EventEmitter.FabricateEvent(new ChangeStatsEvent
        {
            Timestamp = trigger?.Timestamp ?? 0,
            SourceId  = Owner.PlayerId,
            TargetId  = Owner.PlayerId,
            Before    = before,
            Delta     = delta,
            After     = after,
        }, trigger);
}

// =============================================================================
// Supporting types
// =============================================================================

/// <summary>
/// A buff value that is either a fixed rating amount or a function that
/// derives the amount from the combatant (and optionally an item).
/// </summary>
[OneOf.GenerateOneOf]
public partial class BuffVal : OneOf.OneOfBase<double, Func<Combatant, Item?, double>>;

/// <summary>
/// Describes a stat rating buff. Unset fields contribute 0 to each stat.
/// Set <see cref="ItemId"/> when any stat value is item-level-dependent.
/// </summary>
public sealed class StatBuff
{
    public BuffVal? Intellect { get; init; }
    public BuffVal? Stamina   { get; init; }
    public BuffVal? Armor     { get; init; }
    public BuffVal? Crit      { get; init; }
    public BuffVal? Haste     { get; init; }
    public BuffVal? Expertise { get; init; }
    public BuffVal? Spirit    { get; init; }

    /// <summary>Item ID to pass to function-based <see cref="BuffVal"/> callbacks.</summary>
    public int? ItemId { get; init; }
}

/// <summary>
/// Describes a multiplicative stat buff (e.g., 1.05 = +5%).
/// Unset fields are not modified. Values must be &gt; 0.
/// </summary>
public sealed class StatMultiplierBuff
{
    public double? Intellect { get; init; }
    public double? Stamina   { get; init; }
    public double? Armor     { get; init; }
    public double? Crit      { get; init; }
    public double? Haste     { get; init; }
    public double? Expertise { get; init; }
    public double? Spirit    { get; init; }
}

/// <summary>Mutable snapshot of a player's stat ratings.</summary>
internal sealed class PlayerStats
{
    public double Intellect { get; set; }
    public double Stamina   { get; set; }
    public double Armor     { get; set; }
    public double Crit      { get; set; }
    public double Haste     { get; set; }
    public double Expertise { get; set; }
    public double Spirit    { get; set; }

    public PlayerStats Clone() => (PlayerStats)MemberwiseClone();

    public Stats ToStats() => new()
    {
        Intellect = Intellect,
        Stamina   = Stamina,
        Armor     = Armor,
        Crit      = Crit,
        Haste     = Haste,
        Expertise = Expertise,
        Spirit    = Spirit,
    };
}

/// <summary>Per-stat multiplier state. All values default to 1.0 (no multiplier).</summary>
internal sealed class PlayerMultipliers
{
    public double Intellect { get; set; } = 1.0;
    public double Stamina   { get; set; } = 1.0;
    public double Armor     { get; set; } = 1.0;
    public double Crit      { get; set; } = 1.0;
    public double Haste     { get; set; } = 1.0;
    public double Expertise { get; set; } = 1.0;
    public double Spirit    { get; set; } = 1.0;
}
