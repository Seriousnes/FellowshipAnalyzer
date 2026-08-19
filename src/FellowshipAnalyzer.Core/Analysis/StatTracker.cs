using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using OneOf;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks the selected player's stats over the course of a dungeon across both channels every stat has:
/// a rating, which converts to a percentage through Fellowship's diminishing-returns curve, and a flat
/// percentage, which is added to the converted rating afterwards and never sees diminishing returns.
/// Monitors buff/debuff apply, remove, and stack changes to keep both channels up to date and fabricates
/// <see cref="ChangeStatsEvent"/> whenever either one moves.
/// Also tracks the two cooldown stat pools, Ability Cooldown Reduction and Cooldown Acceleration:
/// each starts from the gear-derived seed frozen on <see cref="CombatantStats"/> and accrues runtime
/// <see cref="CooldownModifier"/>s, fabricating <see cref="ChangeCooldownModifierEvent"/> on every change.
/// </summary>
/// <remarks>
/// Rating → percentage conversion uses Fellowship's piecewise diminishing-returns
/// formula (CombatMechanics.md). All secondary stats share the same curve.
/// Critical Strike has an additional 5% base chance added after DR.
/// Flat percentages are additive with the converted rating and with each other:
/// <c>effective = RatingToPercentage(rating) + Σ flat</c>. Run the <c>measure-haste-stacking</c> tool to
/// reproduce the tick-interval measurement that settles this against a multiplicative reading.
/// </remarks>
public sealed partial class StatTracker : Analyzer
{
    private PlayerStats _currentStats = new();
    private PlayerStats _pullStats = new();
    private readonly PlayerMultipliers _multipliers = new();

    private readonly Dictionary<int, StatBuff> _statBuffs = new(StatBuffs.Ratings);

    private readonly Dictionary<int, StatMultiplierBuff> _statMultiplierBuffs = new(StatBuffs.Multipliers);

    private readonly Dictionary<int, StatPercentageBuff> _percentageBuffs = new(StatBuffs.Percentages);

    private readonly Dictionary<int, CooldownBuff> _cooldownBuffs = new(StatBuffs.Cooldowns);

    private readonly Dictionary<int, int> _percentageStacks = [];

    private readonly Dictionary<int, PercentageAmounts> _percentageAmounts = [];

    private readonly List<CooldownModifier> _abilityCooldownReduction = [];

    private readonly List<CooldownModifier> _cooldownAcceleration = [];

    /// <summary>5% base critical strike chance, added after diminishing returns.</summary>
    public const double BaseCritChance = 0.05;

    [On<DungeonStartEvent>]
    private void OnDungeonStart(DungeonStartEvent e)
    {
        var combatant = Owner.SelectedCombatant;
        var stats = combatant.Stats;

        _pullStats = new PlayerStats
        {
            MainStat = Math.Max(stats.Strength, Math.Max(stats.Agility, stats.Intellect)),
            Stamina = stats.Stamina,
            Armor = stats.Armor,
            Crit = stats.Crit,
            Haste = stats.Haste,
            Expertise = stats.Expertise,
            Spirit = stats.Spirit,
        };
        _currentStats = _pullStats.Clone();

        foreach (var aura in combatant.Info.Auras)
            SetPercentageStacks(aura.Ability, Math.Max(aura.Stacks, 1), e);
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
    /// Registers a flat percentage stat buff (e.g., 30% haste). Its values are added to the
    /// rating-derived percentage rather than multiplied with it.
    /// </summary>
    /// <remarks>
    /// Stack counts are absolute, taken from what the log reports rather than accumulated from deltas, so a
    /// stack removal that leaves a positive count on a buff never seen applied is read as a window that was
    /// already open, and the remaining stacks start contributing from there. A removal that leaves no stacks
    /// contributes nothing, so a stray removal cannot drive a stat negative.
    /// </remarks>
    public void AddPercentageBuff(int spellId, StatPercentageBuff buff) =>
        _percentageBuffs[spellId] = buff;

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

    /// <summary>
    /// The player's current main stat rating, including every tracked buff applied so far this pull. A
    /// combatantinfo carries all three primary slots but populates only the hero's own, so Strength,
    /// Agility, and Intellect collapse to this one channel.
    /// </summary>
    public double CurrentMainStat => _currentStats.MainStat;
    /// <summary>The player's current Stamina rating, including every tracked buff applied so far this pull.</summary>
    public double CurrentStamina => _currentStats.Stamina;
    /// <summary>The player's current Armor rating, including every tracked buff applied so far this pull.</summary>
    public double CurrentArmor => _currentStats.Armor;
    /// <summary>The player's current Critical Strike rating, including every tracked buff applied so far this pull.</summary>
    public double CurrentCritRating => _currentStats.Crit;
    /// <summary>The player's current Haste rating, including every tracked buff applied so far this pull.</summary>
    public double CurrentHasteRating => _currentStats.Haste;
    /// <summary>The player's current Expertise rating, including every tracked buff applied so far this pull.</summary>
    public double CurrentExpertiseRating => _currentStats.Expertise;
    /// <summary>The player's current Spirit rating, including every tracked buff applied so far this pull.</summary>
    public double CurrentSpiritRating => _currentStats.Spirit;

    /// <summary>The player's Critical Strike rating at pull start, before any tracked buff was applied.</summary>
    public double StartingCritRating => _pullStats.Crit;
    /// <summary>The player's Haste rating at pull start, before any tracked buff was applied.</summary>
    public double StartingHasteRating => _pullStats.Haste;
    /// <summary>The player's Expertise rating at pull start, before any tracked buff was applied.</summary>
    public double StartingExpertiseRating => _pullStats.Expertise;
    /// <summary>The player's Spirit rating at pull start, before any tracked buff was applied.</summary>
    public double StartingSpiritRating => _pullStats.Spirit;
    /// <summary>The player's main stat rating at pull start, before any tracked buff was applied.</summary>
    public double StartingMainStat => _pullStats.MainStat;

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

    /// <summary>Converts a Haste rating to a percentage using <see cref="RatingToPercentage"/>.</summary>
    public double HastePercentage(double rating) => RatingToPercentage(rating);
    /// <summary>Converts an Expertise rating to a percentage using <see cref="RatingToPercentage"/>.</summary>
    public double ExpertisePercentage(double rating) => RatingToPercentage(rating);
    /// <summary>Converts a Spirit rating to a percentage using <see cref="RatingToPercentage"/>.</summary>
    public double SpiritPercentage(double rating) => RatingToPercentage(rating);

    /// <summary>The player's current Critical Strike chance: the converted rating, the 5% base chance, and every active flat percentage.</summary>
    public double CurrentCritPercentage => CritPercentage(CurrentCritRating, withBase: true) + _currentStats.AdditionalCrit;
    /// <summary>The player's current Haste percentage: the converted rating plus every active flat percentage.</summary>
    public double CurrentHastePercentage => HastePercentage(CurrentHasteRating) + _currentStats.AdditionalHaste;
    /// <summary>The player's current Expertise percentage: the converted rating plus every active flat percentage.</summary>
    public double CurrentExpertisePercentage => ExpertisePercentage(CurrentExpertiseRating) + _currentStats.AdditionalExpertise;
    /// <summary>The player's current Spirit percentage: the converted rating plus every active flat percentage.</summary>
    public double CurrentSpiritPercentage => SpiritPercentage(CurrentSpiritRating) + _currentStats.AdditionalSpirit;

    /// <summary>The flat Critical Strike chance active flat-percentage effects contribute, as a fraction, excluding the rating and the base chance.</summary>
    public double AdditionalCrit => _currentStats.AdditionalCrit;
    /// <summary>The flat Haste active flat-percentage effects contribute, as a fraction, excluding the rating.</summary>
    public double AdditionalHaste => _currentStats.AdditionalHaste;
    /// <summary>The flat Expertise active flat-percentage effects contribute, as a fraction, excluding the rating.</summary>
    public double AdditionalExpertise => _currentStats.AdditionalExpertise;
    /// <summary>The flat Spirit active flat-percentage effects contribute, as a fraction, excluding the rating.</summary>
    public double AdditionalSpirit => _currentStats.AdditionalSpirit;

    /// <summary>
    /// The player's current Critical Strike power: the 2.0 base critical multiplier every hero shares plus
    /// every active flat percentage. A critical hit deals this multiple of a normal one.
    /// </summary>
    public double CurrentCritPower => BaseCritPower + _currentStats.AdditionalCritPower;

    /// <summary>The 2.0 base critical multiplier every hero starts from, from each hero's <c>CritMultiplier</c> attribute.</summary>
    public const double BaseCritPower = 2.0;

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
    private void OnApplyBuff(ApplyBuffEvent e)
    {
        HandleBuffGain(e.Ability.FSLID, e.Prepull.GetValueOrDefault(), e);
        SetPercentageStacks(e.Ability.FSLID, stacks: 1, e);
    }

    [On<RemoveBuffEvent>(To = Actor.Player)]
    private void OnRemoveBuff(RemoveBuffEvent e)
    {
        HandleBuffLoss(e.Ability.FSLID, e);
        SetPercentageStacks(e.Ability.FSLID, stacks: 0, e);
    }

    [On<ApplyBuffStackEvent>(To = Actor.Player)]
    private void OnApplyBuffStack(ApplyBuffStackEvent e)
    {
        HandleBuffGain(e.Ability.FSLID, isPrepull: false, e);
        SetPercentageStacks(e.Ability.FSLID, e.Stack, e);
    }

    [On<RemoveBuffStackEvent>(To = Actor.Player)]
    private void OnRemoveBuffStack(RemoveBuffStackEvent e)
    {
        HandleBuffLoss(e.Ability.FSLID, e);
        SetPercentageStacks(e.Ability.FSLID, e.Stack, e);
    }

    [On<ApplyDebuffEvent>(To = Actor.Player)]
    private void OnApplyDebuff(ApplyDebuffEvent e)
    {
        HandleBuffGain(e.Ability.FSLID, e.Prepull.GetValueOrDefault(), e);
        SetPercentageStacks(e.Ability.FSLID, stacks: 1, e);
    }

    [On<RemoveDebuffEvent>(To = Actor.Player)]
    private void OnRemoveDebuff(RemoveDebuffEvent e)
    {
        HandleBuffLoss(e.Ability.FSLID, e);
        SetPercentageStacks(e.Ability.FSLID, stacks: 0, e);
    }

    [On<ApplyDebuffStackEvent>(To = Actor.Player)]
    private void OnApplyDebuffStack(ApplyDebuffStackEvent e)
    {
        HandleBuffGain(e.Ability.FSLID, isPrepull: false, e);
        SetPercentageStacks(e.Ability.FSLID, e.Stack, e);
    }

    [On<RemoveDebuffStackEvent>(To = Actor.Player)]
    private void OnRemoveDebuffStack(RemoveDebuffStackEvent e)
    {
        HandleBuffLoss(e.Ability.FSLID, e);
        SetPercentageStacks(e.Ability.FSLID, e.Stack, e);
    }

    private void SetPercentageStacks(int spellId, int stacks, Event trigger)
    {
        if (!_percentageBuffs.TryGetValue(spellId, out var buff)) return;

        var previous = _percentageStacks.GetValueOrDefault(spellId);
        var next = buff.PerStack ? Math.Max(stacks, 0) : stacks > 0 ? 1 : 0;
        if (next == previous) return;

        if (previous == 0)
            _percentageAmounts[spellId] = ResolvePercentages(buff, trigger);

        var amounts = _percentageAmounts[spellId];

        _percentageStacks[spellId] = next;
        if (next == 0)
        {
            _percentageStacks.Remove(spellId);
            _percentageAmounts.Remove(spellId);
        }

        var before = _currentStats.ToStats();
        ApplyPercentages(amounts, next - previous);
        var after = _currentStats.ToStats();
        FabricateChangeStats(trigger, before, after - before, after);
    }

    private PercentageAmounts ResolvePercentages(StatPercentageBuff buff, Event trigger) => new(
        ResolveBuffVal(buff.ItemId, buff.Crit, trigger),
        ResolveBuffVal(buff.ItemId, buff.Haste, trigger),
        ResolveBuffVal(buff.ItemId, buff.Expertise, trigger),
        ResolveBuffVal(buff.ItemId, buff.Spirit, trigger),
        ResolveBuffVal(buff.ItemId, buff.CritPower, trigger));

    private void ApplyPercentages(PercentageAmounts amounts, int stackDelta)
    {
        _currentStats.AdditionalCrit += amounts.Crit * stackDelta;
        _currentStats.AdditionalHaste += amounts.Haste * stackDelta;
        _currentStats.AdditionalExpertise += amounts.Expertise * stackDelta;
        _currentStats.AdditionalSpirit += amounts.Spirit * stackDelta;
        _currentStats.AdditionalCritPower += amounts.CritPower * stackDelta;
    }

    private readonly record struct PercentageAmounts(
        double Crit,
        double Haste,
        double Expertise,
        double Spirit,
        double CritPower);

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
        _currentStats.MainStat += ResolveBuffVal(buff.ItemId, buff.MainStat) * factor * (withMultipliers ? _multipliers.MainStat : 1.0);
        _currentStats.Stamina += ResolveBuffVal(buff.ItemId, buff.Stamina) * factor * (withMultipliers ? _multipliers.Stamina : 1.0);
        _currentStats.Armor += ResolveBuffVal(buff.ItemId, buff.Armor) * factor * (withMultipliers ? _multipliers.Armor : 1.0);
        _currentStats.Crit += ResolveBuffVal(buff.ItemId, buff.Crit) * factor * (withMultipliers ? _multipliers.Crit : 1.0);
        _currentStats.Haste += ResolveBuffVal(buff.ItemId, buff.Haste) * factor * (withMultipliers ? _multipliers.Haste : 1.0);
        _currentStats.Expertise += ResolveBuffVal(buff.ItemId, buff.Expertise) * factor * (withMultipliers ? _multipliers.Expertise : 1.0);
        _currentStats.Spirit += ResolveBuffVal(buff.ItemId, buff.Spirit) * factor * (withMultipliers ? _multipliers.Spirit : 1.0);
    }

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

        if (buff.MainStat is double mMult) _multipliers.MainStat *= Factor(mMult);
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

        if (buff.MainStat is double mMult) _currentStats.MainStat *= Factor(mMult);
        if (buff.Stamina is double sMult) _currentStats.Stamina *= Factor(sMult);
        if (buff.Armor is double aMult) _currentStats.Armor *= Factor(aMult);
        if (buff.Crit is double cMult) _currentStats.Crit *= Factor(cMult);
        if (buff.Haste is double hMult) _currentStats.Haste *= Factor(hMult);
        if (buff.Expertise is double eMult) _currentStats.Expertise *= Factor(eMult);
        if (buff.Spirit is double spMult) _currentStats.Spirit *= Factor(spMult);
    }

    private double ResolveBuffVal(int? itemId, BuffVal? buffVal, Event? trigger = null)
    {
        if (buffVal is null) return 0.0;
        return buffVal.Match(
            value => value,
            func =>
            {
                var combatant = Owner.SelectedCombatant;
                var item = itemId is int id ? combatant.GetItem(id) : null;
                return func(new StatBuffContext(combatant, item, trigger));
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

/// <summary>
/// A buff value that is either a fixed amount or a function that derives the amount from the
/// player's gear and the event that applied the buff.
/// </summary>
[GenerateOneOf]
public partial class BuffVal : OneOfBase<double, Func<StatBuffContext, double>>;

/// <summary>
/// What a function-valued <see cref="BuffVal"/> reads to size its contribution: the player's gear and
/// talents, the item named by <see cref="StatBuff.ItemId"/> or <see cref="StatPercentageBuff.ItemId"/>,
/// and the event that applied the buff. Effects whose magnitude depends on the player's state at the
/// moment of application, such as a blessing that scales with current Spirit, read it off
/// <see cref="Trigger"/>'s resource snapshot.
/// </summary>
/// <param name="Combatant">The selected player.</param>
/// <param name="Item">The equipped item the buff is attached to, or <c>null</c> when it names none.</param>
/// <param name="Trigger">The event that applied the buff, or <c>null</c> for a forced change.</param>
public readonly record struct StatBuffContext(FullCombatant Combatant, Item? Item, Event? Trigger)
{
    /// <summary>
    /// The player's own resource snapshot at <see cref="Trigger"/>, taken from whichever side of the
    /// event the player is on, or <c>null</c> when the event carries none.
    /// </summary>
    public ActorResources? PlayerResources =>
        Trigger is null ? null
        : Trigger is IHasTargetEvent target && target.TargetId == Combatant.Id ? Trigger.TargetResources ?? Trigger.SourceResources
        : Trigger.SourceResources ?? Trigger.TargetResources;

    /// <summary>
    /// The player's amount of <paramref name="resourceType"/> at <see cref="Trigger"/> as a fraction of
    /// its maximum, or 0 when the event carries no snapshot of it.
    /// </summary>
    public double ResourceFraction(ResourceTypes resourceType)
    {
        var resource = PlayerResources?.Resources.FirstOrDefault(r => r.Type == resourceType);
        return resource is { Max: > 0 } ? (double)resource.Amount / resource.Max : 0.0;
    }
}

/// <summary>
/// Describes a stat rating buff. Unset fields contribute 0 to each stat.
/// Set <see cref="ItemId"/> when any stat value is item-level-dependent.
/// </summary>
public sealed class StatBuff
{
    /// <summary>Main stat rating contributed while this buff is active, in whichever primary stat the hero scales from.</summary>
    public BuffVal? MainStat { get; init; }
    /// <summary>Stamina rating contributed while this buff is active.</summary>
    public BuffVal? Stamina { get; init; }
    /// <summary>Armor rating contributed while this buff is active.</summary>
    public BuffVal? Armor { get; init; }
    /// <summary>Critical Strike rating contributed while this buff is active.</summary>
    public BuffVal? Crit { get; init; }
    /// <summary>Haste rating contributed while this buff is active.</summary>
    public BuffVal? Haste { get; init; }
    /// <summary>Expertise rating contributed while this buff is active.</summary>
    public BuffVal? Expertise { get; init; }
    /// <summary>Spirit rating contributed while this buff is active.</summary>
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
    /// <summary>Multiplier applied to the hero's main stat while this buff is active.</summary>
    public double? MainStat { get; init; }
    /// <summary>Multiplier applied to Stamina while this buff is active.</summary>
    public double? Stamina { get; init; }
    /// <summary>Multiplier applied to Armor while this buff is active.</summary>
    public double? Armor { get; init; }
    /// <summary>Multiplier applied to Critical Strike rating while this buff is active.</summary>
    public double? Crit { get; init; }
    /// <summary>Multiplier applied to Haste rating while this buff is active.</summary>
    public double? Haste { get; init; }
    /// <summary>Multiplier applied to Expertise rating while this buff is active.</summary>
    public double? Expertise { get; init; }
    /// <summary>Multiplier applied to Spirit rating while this buff is active.</summary>
    public double? Spirit { get; init; }
}

/// <summary>
/// Describes a flat percentage stat buff. Every value is a fraction (0.30 = 30%) added to the
/// rating-derived percentage rather than multiplied with it, and unset fields contribute 0.
/// Set <see cref="PerStack"/> when the effect scales with its stack count, and <see cref="ItemId"/>
/// when any value is item-level-dependent.
/// </summary>
public sealed class StatPercentageBuff
{
    /// <summary>Flat critical strike chance contributed while this buff is active.</summary>
    public BuffVal? Crit { get; init; }
    /// <summary>Flat haste contributed while this buff is active.</summary>
    public BuffVal? Haste { get; init; }
    /// <summary>Flat expertise contributed while this buff is active.</summary>
    public BuffVal? Expertise { get; init; }
    /// <summary>Flat spirit contributed while this buff is active.</summary>
    public BuffVal? Spirit { get; init; }
    /// <summary>Flat critical strike power contributed while this buff is active.</summary>
    public BuffVal? CritPower { get; init; }

    /// <summary>
    /// Whether each value is contributed once per stack. When set, the tracked contribution follows the
    /// stack count the log reports; when unset, the buff contributes its values once while it is active
    /// regardless of how many stacks it carries.
    /// </summary>
    public bool PerStack { get; init; }

    /// <summary>Item ID to pass to function-based <see cref="BuffVal"/> callbacks.</summary>
    public int? ItemId { get; init; }
}

internal sealed class PlayerStats
{
    public double MainStat { get; set; }
    public double Stamina { get; set; }
    public double Armor { get; set; }
    public double Crit { get; set; }
    public double Haste { get; set; }
    public double Expertise { get; set; }
    public double Spirit { get; set; }

    public double AdditionalCrit { get; set; }
    public double AdditionalHaste { get; set; }
    public double AdditionalExpertise { get; set; }
    public double AdditionalSpirit { get; set; }
    public double AdditionalCritPower { get; set; }

    public PlayerStats Clone() => (PlayerStats)MemberwiseClone();

    public Stats ToStats() => new()
    {
        MainStat = MainStat,
        Stamina = Stamina,
        Armor = Armor,
        Crit = Crit,
        Haste = Haste,
        Expertise = Expertise,
        Spirit = Spirit,
        AdditionalCrit = AdditionalCrit,
        AdditionalHaste = AdditionalHaste,
        AdditionalExpertise = AdditionalExpertise,
        AdditionalSpirit = AdditionalSpirit,
        AdditionalCritPower = AdditionalCritPower,
    };
}

internal sealed class PlayerMultipliers
{
    public double MainStat { get; set; } = 1.0;
    public double Stamina { get; set; } = 1.0;
    public double Armor { get; set; } = 1.0;
    public double Crit { get; set; } = 1.0;
    public double Haste { get; set; } = 1.0;
    public double Expertise { get; set; } = 1.0;
    public double Spirit { get; set; } = 1.0;
}
