using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI.Components;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Models Chronoshift's cooldown recovery effect. While the player channels Chronoshift it adds a
/// Cooldown Acceleration modifier of 800% to the pool <see cref="StatTracker"/> tracks, taking an
/// ability with no haste contribution to 9× recovery. The channel lasts a fixed 3 seconds
/// (haste-independent) unless an <see cref="EndChannelEvent"/> cancels it early.
/// <para>
/// The window is driven by channel events. <see cref="OnBeginChannel"/> adds the modifier; when the log
/// recorded no channel end (a fully channelled cast leaves <see cref="BeginChannelEvent.EndChannel"/>
/// null) it fabricates an <see cref="EndChannelEvent"/> at the scheduled 3-second end so the close rides
/// a real, in-order event rather than a lazy per-event poll. <see cref="OnEndChannel"/> removes the
/// modifier for both the logged and the fabricated end. An unmatched or repeated begin cannot compound
/// the modifier because <see cref="CloseWindow"/> only removes while a window is open and a removal of an
/// absent modifier is a no-op.
/// </para>
/// </summary>
[ActiveWhen<HasChronoshiftGear>]
[After<SpellUsable>]
public sealed partial class ChronoshiftAnalyzer(
    Lazy<SpellUsable> spellUsable,
    Lazy<StatTracker> statTracker) : Analyzer
{
    private const double AddedRecovery = 8.0;

    private static readonly CooldownModifier AddedRecoveryModifier = new(AddedRecovery);

    private const int ChannelDurationMs = 3000;

    private readonly List<ChronoshiftWindow> _windows = [];
    private readonly Dictionary<int, int> _recoveryBySpell = [];

    private readonly Dictionary<int, int> _snapshot = [];
    private int? _scheduledEnd;
    private int _openTimestamp;

    /// <summary>All Chronoshift recovery windows observed for the selected player.</summary>
    public IReadOnlyList<ChronoshiftWindow> Windows => _windows;

    /// <summary>
    /// Bonus cooldown recovery Chronoshift granted to each ability over the encounter, in
    /// milliseconds, ordered by amount descending. This is the extra cooldown progress the added 800%
    /// bought beyond what the spell would have made over the same window without it. Because the pool
    /// is additive, that extra is 8× the time on cooldown whatever the ability's haste contribution.
    /// </summary>
    public IReadOnlyList<AbilityRecovery> RecoveryByAbility =>
        [.. _recoveryBySpell
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new AbilityRecovery(kv.Key, kv.Value))];

    /// <summary>Total bonus cooldown recovery Chronoshift granted across all abilities, in milliseconds.</summary>
    public int TotalRecoveredMs => _recoveryBySpell.Values.Sum();

    [On<BeginChannelEvent>(By = Actor.Player, Spell = nameof(Spells.Chronoshift))]
    private void OnBeginChannel(BeginChannelEvent e)
    {
        CloseWindow(e.Timestamp, e);

        _openTimestamp = e.Timestamp;
        _scheduledEnd = e.Timestamp + ChannelDurationMs;
        _windows.Add(new ChronoshiftWindow(e.Timestamp, _scheduledEnd.Value));

        _statTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, AddedRecoveryModifier, e);

        if (e.EndChannel is null)
            FabricateChannelEnd(e);
    }

    private void FabricateChannelEnd(BeginChannelEvent begin)
    {
        begin.EndChannel = Owner.EventEmitter.FabricateEvent(new EndChannelEvent
        {
            Timestamp = _scheduledEnd!.Value,
            SourceId = begin.SourceId,
            Ability = begin.Ability,
            Start = begin.Timestamp,
            Duration = ChannelDurationMs,
            BeginChannel = begin,
        }, begin);
    }

    [On<ChangeCooldownModifierEvent>]
    private void OnChangeCooldownModifier(ChangeCooldownModifierEvent e)
    {
        if (!e.Added || !ReferenceEquals(e.Modifier, AddedRecoveryModifier) || _scheduledEnd is null) return;

        foreach (var spellId in _spellUsable.GetSpellsOnCooldown())
            _snapshot[spellId] = _spellUsable.CooldownRemaining(spellId, e.Timestamp);
    }

    [On<EndChannelEvent>(By = Actor.Player, Spell = nameof(Spells.Chronoshift))]
    private void OnEndChannel(EndChannelEvent e) => CloseWindow(e.Timestamp, e);

    private void CloseWindow(int timestamp, Event? trigger)
    {
        if (_scheduledEnd is not int scheduledEnd) return;
        var closeAt = Math.Min(timestamp, scheduledEnd);
        _statTracker.RemoveCooldownModifier(CooldownPool.CooldownAcceleration, AddedRecoveryModifier, trigger);

        var wallclock = closeAt - _openTimestamp;
        foreach (var (spellId, remainingAtOpen) in _snapshot)
        {
            var onCooldown = Math.Min(wallclock, remainingAtOpen);
            var bonus = (int)(AddedRecovery * onCooldown);
            if (bonus > 0)
                _recoveryBySpell[spellId] = _recoveryBySpell.GetValueOrDefault(spellId) + bonus;
        }

        _snapshot.Clear();
        _scheduledEnd = null;
        _windows[^1] = _windows[^1] with { EndTimestamp = closeAt };
    }
}

/// <summary>A single Chronoshift channel window: the interval over which cooldown recovery was boosted.</summary>
public record ChronoshiftWindow(int BeginTimestamp, int EndTimestamp);

/// <summary>Bonus cooldown recovery Chronoshift granted to one ability over the encounter.</summary>
/// <param name="SpellId">The ability's FSLID.</param>
/// <param name="RecoveredMs">Bonus cooldown recovery in milliseconds.</param>
public readonly record struct AbilityRecovery(int SpellId, int RecoveredMs);
