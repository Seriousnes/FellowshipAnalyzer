using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks the selected player's total effective haste percentage throughout a fight,
/// combining rating-based haste (from <see cref="StatTracker"/>) with
/// percentage-based haste buffs (e.g., speed cooldowns).
/// Fabricates <see cref="ChangeHasteEvent"/> whenever effective haste changes.
/// </summary>
/// <remarks>
/// Haste sources combine multiplicatively:
/// <c>total = base × (1 + buff1) × (1 + buff2) × …</c>
/// expressed via the additive helper formula: <c>AddHaste(base, gain) = base * (1 + gain) + gain</c>.
///
/// Haste time-scaling: <c>effective_duration = base_duration × 100 / (100 + hastePercent)</c>
/// where <c>hastePercent = Current × 100</c> (e.g., 30 for 30% haste).
/// </remarks>
[After<StatTracker>]
public sealed partial class Haste(Lazy<StatTracker> statTracker) : Analyzer
{
    /// <summary>
    /// Current total effective haste percentage as a decimal fraction (0.30 = 30%).
    /// Includes both rating-based haste (via <see cref="StatTracker"/>) and any
    /// active percentage haste buffs.
    /// </summary>
    public double Current { get; private set; }

    private readonly Dictionary<int, HasteBuff> _hasteBuffs = new()
    {
        { Spells.EventHorizonBuff.Guid, new(Haste: 0.3) },
        { Spells.SkystridersGraceBuff.Guid, new(Haste: 0.3) },
        { Spells.WrathOfWinterBuff.Guid, new(Haste: 0.3)  },
    };

    [On<FightStartEvent>]
    private void OnFightStart(FightStartEvent e)
    {
        Current = _statTracker.CurrentHastePercentage;
        TriggerChangeHaste(e, null, Current);
    }

    /// <summary>
    /// Registers a flat percentage haste buff (e.g., 0.30 for 30% haste).
    /// </summary>
    public void AddHasteBuff(int spellId, double hastePercentage) =>
        _hasteBuffs[spellId] = new HasteBuff(hastePercentage);

    /// <summary>
    /// Registers a per-stack percentage haste buff (e.g., 0.04 per stack).
    /// </summary>
    public void AddHasteBuffPerStack(int spellId, double hastePerStack) =>
        _hasteBuffs[spellId] = new HasteBuff(HastePerStack: hastePerStack);

    /// <summary>
    /// Registers a haste buff with full control over both flat and per-stack values.
    /// </summary>
    public void AddHasteBuff(int spellId, HasteBuff buff) =>
        _hasteBuffs[spellId] = buff;

    /// <summary>
    /// Adds a haste percentage multiplicatively:
    /// <c>result = baseHaste * (1 + hasteGain) + hasteGain</c>.
    /// Negative gains remove haste instead.
    /// </summary>
    public static double AddHaste(double baseHaste, double hasteGain)
    {
        if (hasteGain < 0)
            return (baseHaste - Math.Abs(hasteGain)) / (1.0 + Math.Abs(hasteGain));
        return baseHaste * (1.0 + hasteGain) + hasteGain;
    }

    /// <summary>
    /// Removes a previously added haste percentage:
    /// <c>result = (baseHaste - hasteLoss) / (1 + hasteLoss)</c>.
    /// Negative losses add haste instead.
    /// </summary>
    public static double RemoveHaste(double baseHaste, double hasteLoss)
    {
        if (hasteLoss < 0)
            return baseHaste * (1.0 + Math.Abs(hasteLoss)) + Math.Abs(hasteLoss);
        return (baseHaste - hasteLoss) / (1.0 + hasteLoss);
    }

    /// <summary>
    /// Scales a base duration by the current haste percentage.
    /// <c>effective = baseDurationMs × 100 / (100 + hastePercent)</c>.
    /// </summary>
    public int ScaleDuration(int baseDurationMs) =>
        (int)(baseDurationMs * 100.0 / (100.0 + Current * 100.0));

    [On<ApplyBuffEvent>(To = Actor.Player)]
    private void OnApplyBuff(ApplyBuffEvent e) => ApplyActiveBuff(e.Ability.Guid, e);

    [On<RemoveBuffEvent>(To = Actor.Player)]
    private void OnRemoveBuff(RemoveBuffEvent e) => RemoveActiveBuff(e.Ability.Guid, e);

    [On<ApplyDebuffEvent>(To = Actor.Player)]
    private void OnApplyDebuff(ApplyDebuffEvent e) => ApplyActiveBuff(e.Ability.Guid, e);

    [On<RemoveDebuffEvent>(To = Actor.Player)]
    private void OnRemoveDebuff(RemoveDebuffEvent e) => RemoveActiveBuff(e.Ability.Guid, e);

    [On<ApplyBuffStackEvent>(To = Actor.Player)]
    private void OnApplyBuffStack(ApplyBuffStackEvent e) => ChangeStack(e.Ability.Guid, +1, e);

    [On<RemoveBuffStackEvent>(To = Actor.Player)]
    private void OnRemoveBuffStack(RemoveBuffStackEvent e) => ChangeStack(e.Ability.Guid, -1, e);

    [On<ApplyDebuffStackEvent>(To = Actor.Player)]
    private void OnApplyDebuffStack(ApplyDebuffStackEvent e) => ChangeStack(e.Ability.Guid, +1, e);

    [On<RemoveDebuffStackEvent>(To = Actor.Player)]
    private void OnRemoveDebuffStack(RemoveDebuffStackEvent e) => ChangeStack(e.Ability.Guid, -1, e);

    private void ApplyActiveBuff(int spellId, Event trigger)
    {
        if (!_hasteBuffs.TryGetValue(spellId, out var buff)) return;
        var gain = buff.Haste;
        if (gain is null) return;
        SetHaste(trigger, AddHaste(Current, gain.Value));
    }

    private void RemoveActiveBuff(int spellId, Event trigger)
    {
        if (!_hasteBuffs.TryGetValue(spellId, out var buff)) return;
        var loss = buff.Haste;
        if (loss is null) return;
        SetHaste(trigger, RemoveHaste(Current, loss.Value));
    }

    private void ChangeStack(int spellId, int stackDelta, Event trigger)
    {
        if (!_hasteBuffs.TryGetValue(spellId, out var buff)) return;
        if (buff.HastePerStack is not double perStack) return;

        if (stackDelta > 0)
            SetHaste(trigger, AddHaste(Current, perStack));
        else
            SetHaste(trigger, RemoveHaste(Current, perStack));
    }

    [On<ChangeStatsEvent>(To = Actor.Player)]
    private void OnChangeStats(ChangeStatsEvent e)
    {
        if (e.Delta.Haste is null or 0) return;

        var ratingHasteBefore = _statTracker.HastePercentage(e.Before.Haste ?? 0);
        var withoutRatingHaste = RemoveHaste(Current, ratingHasteBefore);

        var ratingHasteAfter = _statTracker.HastePercentage(e.After.Haste ?? 0);
        var newTotal = AddHaste(withoutRatingHaste, ratingHasteAfter);

        SetHaste(e, newTotal);
    }

    private void SetHaste(Event? trigger, double newHaste)
    {
        if (double.IsNaN(newHaste) || double.IsInfinity(newHaste)) return;

        var oldHaste = Current;
        Current = newHaste;
        TriggerChangeHaste(trigger, oldHaste, Current);
    }

    private void TriggerChangeHaste(Event? trigger, double? oldHaste, double newHaste) =>
        Owner.EventEmitter.FabricateEvent(new ChangeHasteEvent
        {
            Timestamp = trigger?.Timestamp ?? 0,
            SourceId = Owner.PlayerId,
            TargetId = Owner.PlayerId,
            OldHaste = oldHaste,
            NewHaste = newHaste,
            Before = new Stats { Haste = oldHaste.HasValue ? oldHaste.Value * 100 : null },
            After = new Stats { Haste = newHaste * 100 },
            Delta = new Stats { Haste = oldHaste.HasValue ? (newHaste - oldHaste.Value) * 100 : null },
        }, trigger);
}

/// <summary>
/// Describes a percentage-based haste buff.
/// Use <see cref="Haste"/> for a flat haste percentage, <see cref="HastePerStack"/> for stacking buffs.
/// </summary>
/// <param name="Haste">Flat haste percentage per application (e.g., 0.30 for 30% haste).</param>
/// <param name="HastePerStack">Haste percentage added per stack (e.g., 0.04 for 4% per stack).</param>
public sealed record HasteBuff(double? Haste = null, double? HastePerStack = null);
