using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Core.UI.Components;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Models the cooldown recovery Glorious Purpose grants. The Fated Strike weapon's active applies the
/// buff, multiplying Cooldown Recovery by 3, so it joins the pool <see cref="StatTracker"/> tracks as a
/// modifier of 3.0. Fated Strike's own cooldown scales off Ability Cooldown Reduction alone, so it recovers
/// at the normal rate and takes no bonus recovery.
/// <para>
/// The logged <see cref="ApplyBuffEvent"/> opens a window and adds the modifier. The logged
/// <see cref="RemoveBuffEvent"/> closes it at that event's timestamp and removes the modifier. A
/// <see cref="RefreshBuffEvent"/> inside an open window changes nothing, and a refresh with no window open
/// opens one. A window with no logged removal lasts to the end of the dungeon.
/// </para>
/// </summary>
[After<SpellUsable>]
public sealed partial class GloriousPurposeAnalyzer(
    Lazy<Abilities> abilities,
    Lazy<SpellUsable> spellUsable,
    Lazy<StatTracker> statTracker) : Analyzer
{
    private static readonly double RecoveryMultiplier =
        EffectModifierValue.Of(Items.GloriousPurpose.Id, StatAttribute.CooldownRecovery);

    private static readonly double AddedRecovery = RecoveryMultiplier - 1.0;

    private static readonly CooldownModifier AddedRecoveryModifier = new(RecoveryMultiplier);

    private readonly List<GloriousPurposeWindow> _windows = [];
    private readonly Dictionary<int, int> _closedRecoveryBySpell = [];

    private readonly Dictionary<int, int> _snapshot = [];
    private bool _isOpen;
    private int _openTimestamp;

    /// <summary>All Glorious Purpose recovery windows for the selected player.</summary>
    public List<GloriousPurposeWindow> Windows => _windows;

    /// <summary>The number of times the Fated Strike weapon's active was cast over the encounter.</summary>
    public int Activations { get; private set; }

    /// <summary>
    /// Bonus cooldown recovery Glorious Purpose granted to each ability over the encounter, in
    /// milliseconds, ordered by amount descending. This is the extra cooldown progress the added 200%
    /// produced beyond what the spell would have made over the same window without it. Only an ability
    /// whose time rate scales off Cooldown Recovery is credited, and its extra is 2× the time on cooldown
    /// whatever the ability's haste contribution.
    /// </summary>
    public List<AbilityRecovery> RecoveryByAbility =>
        [.. RecoveryBySpell()
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new AbilityRecovery(kv.Key, kv.Value))];

    /// <summary>Total bonus cooldown recovery Glorious Purpose granted across all abilities, in milliseconds.</summary>
    public int TotalRecoveredMs => RecoveryBySpell().Values.Sum();

    /// <summary>Total time the buff was active across every window, in milliseconds.</summary>
    public int TotalUptimeMs => _windows.Sum(window => window.EndTimestamp - window.BeginTimestamp);

    [On<CastEvent>(By = Actor.Player)]
    private void OnFatedStrikeCast(CastEvent castEvent)
    {
        if (castEvent.Ability.FSLID == Items.FatedStrike.FSLID)
            Activations++;
    }

    [On<ApplyBuffEvent>(To = Actor.Player)]
    private void OnApply(ApplyBuffEvent buffEvent)
    {
        if (IsGloriousPurpose(buffEvent))
            OpenWindowIfClosed(buffEvent);
    }

    [On<RefreshBuffEvent>(To = Actor.Player)]
    private void OnRefresh(RefreshBuffEvent buffEvent)
    {
        if (IsGloriousPurpose(buffEvent))
            OpenWindowIfClosed(buffEvent);
    }

    [On<RemoveBuffEvent>(To = Actor.Player)]
    private void OnRemove(RemoveBuffEvent buffEvent)
    {
        if (IsGloriousPurpose(buffEvent))
            CloseWindow(buffEvent.Timestamp, buffEvent);
    }

    /// <remarks>
    /// The id is matched here rather than through the <c>[On]</c> attribute's <c>Spell</c> filter because
    /// <see cref="Items"/> is emitted by the spell-registry generator, and source generators do not see
    /// each other's output within one compilation pass. Hero projects reference Core as compiled metadata
    /// and can use the filter; Core itself cannot.
    /// </remarks>
    private static bool IsGloriousPurpose(BuffEvent buffEvent) =>
        buffEvent.Ability.FSLID == Items.GloriousPurpose.FSLID;

    private void OpenWindowIfClosed(BuffEvent buffEvent)
    {
        if (_isOpen) return;

        _isOpen = true;
        _openTimestamp = buffEvent.Timestamp;
        _windows.Add(new GloriousPurposeWindow(buffEvent.Timestamp, Owner.DungeonEndTime));

        _statTracker.AddCooldownModifier(CooldownPool.CooldownRecovery, AddedRecoveryModifier, buffEvent);
    }

    [On<ChangeCooldownModifierEvent>]
    private void OnChangeCooldownModifier(ChangeCooldownModifierEvent e)
    {
        if (!e.Added || !ReferenceEquals(e.Modifier, AddedRecoveryModifier) || !_isOpen) return;

        foreach (var spellId in _spellUsable.GetSpellsOnCooldown())
            if (_abilities.GetAbility(spellId)?.CooldownScaling is { TimeRateCooldownRecovery: true })
                _snapshot[spellId] = _spellUsable.CooldownRemaining(spellId, e.Timestamp);
    }

    private void CloseWindow(int timestamp, Event? trigger)
    {
        if (!_isOpen) return;

        _statTracker.RemoveCooldownModifier(CooldownPool.CooldownRecovery, AddedRecoveryModifier, trigger);

        foreach (var (spellId, bonus) in BonusRecovery(timestamp))
            _closedRecoveryBySpell[spellId] = _closedRecoveryBySpell.GetValueOrDefault(spellId) + bonus;

        _snapshot.Clear();
        _isOpen = false;
        _windows[^1] = _windows[^1] with { EndTimestamp = timestamp };
    }

    private Dictionary<int, int> RecoveryBySpell()
    {
        var recovery = new Dictionary<int, int>(_closedRecoveryBySpell);
        if (_isOpen)
            foreach (var (spellId, bonus) in BonusRecovery(Owner.DungeonEndTime))
                recovery[spellId] = recovery.GetValueOrDefault(spellId) + bonus;

        return recovery;
    }

    private IEnumerable<(int SpellId, int Bonus)> BonusRecovery(int closeAt)
    {
        var wallclock = closeAt - _openTimestamp;
        foreach (var (spellId, remainingAtOpen) in _snapshot)
        {
            var bonus = (int)(AddedRecovery * Math.Min(wallclock, remainingAtOpen));
            if (bonus > 0)
                yield return (spellId, bonus);
        }
    }

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Items;
}

/// <summary>A single Glorious Purpose window: the interval over which cooldown recovery was boosted.</summary>
public record GloriousPurposeWindow(int BeginTimestamp, int EndTimestamp);
