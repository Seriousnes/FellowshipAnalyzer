using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI.Components;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Models Chronoshift's Cooldown Recovery effect. While the player channels Chronoshift, every
/// cooldown recovers 9× faster (800% increased recovery). The channel lasts a fixed 3 seconds
/// (haste-independent) unless an <see cref="EndChannelEvent"/> cancels it early. The recovery
/// modifier is set on <see cref="SpellUsable"/> for the channel window and cleared when it ends;
/// because it is <i>set</i> rather than stacked, an unmatched begin/end (Fellowship logs the
/// channel end for only a minority of channels) cannot compound the modifier.
/// </summary>
[ActiveWhen<HasChronoshiftGear>]
public sealed partial class ChronoshiftAnalyzer(Lazy<SpellUsable> spellUsable) : Analyzer
{
    /// <summary>800% increased cooldown recovery = 9× total recovery rate.</summary>
    private const double RecoveryRate = 9.0;

    /// <summary>Bonus recovery beyond the natural 1× rate: 9× total − 1× natural = 8× bonus.</summary>
    private const double BonusRate = RecoveryRate - 1.0;

    /// <summary>Chronoshift channels a fixed 3 seconds unless an EndChannel cancels it early.</summary>
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
    /// milliseconds, ordered by amount descending. This is the extra recovery from the 9× rate
    /// beyond what the spell's natural cooldown would have progressed over the same window.
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
        CloseWindow(e.Timestamp);

        _snapshot.Clear();
        foreach (var spellId in _spellUsable.GetSpellsOnCooldown())
            _snapshot[spellId] = _spellUsable.CooldownRemaining(spellId, e.Timestamp);

        _openTimestamp = e.Timestamp;
        _spellUsable.SetCooldownRecoveryRate(RecoveryRate, e.Timestamp);
        _scheduledEnd = e.Timestamp + ChannelDurationMs;
        _windows.Add(new ChronoshiftWindow(e.Timestamp, _scheduledEnd.Value));
    }

    [On<EndChannelEvent>(By = Actor.Player, Spell = nameof(Spells.Chronoshift))]
    private void OnEndChannel(EndChannelEvent e) => CloseWindow(e.Timestamp);

    [On<Event>]
    private void OnAnyEvent(Event e)
    {
        if (_scheduledEnd is int end && e.Timestamp >= end)
            CloseWindow(end);
    }

    /// <summary>
    /// Closes the active recovery window at the earlier of <paramref name="timestamp"/> and the
    /// scheduled 3-second end, clearing the recovery modifier and attributing the bonus recovery
    /// each on-cooldown ability received to it. A no-op when no window is open, so a stray or
    /// duplicate close cannot drive the recovery rate below 1.
    /// </summary>
    private void CloseWindow(int timestamp)
    {
        if (_scheduledEnd is not int scheduledEnd) return;
        var closeAt = Math.Min(timestamp, scheduledEnd);
        _spellUsable.SetCooldownRecoveryRate(1.0, closeAt);

        var wallclock = closeAt - _openTimestamp;
        foreach (var (spellId, remainingAtOpen) in _snapshot)
        {
            var onCooldown = Math.Min(wallclock, remainingAtOpen / RecoveryRate);
            var bonus = (int)(BonusRate * onCooldown);
            if (bonus > 0)
                _recoveryBySpell[spellId] = _recoveryBySpell.GetValueOrDefault(spellId) + bonus;
        }

        _snapshot.Clear();
        _scheduledEnd = null;
        _windows[^1] = _windows[^1] with { EndTimestamp = closeAt };
    }

    public override Type? StatisticsComponentType => typeof(ChronoshiftStatistics);
}

/// <summary>A single Chronoshift channel window: the interval over which cooldown recovery was boosted.</summary>
public record ChronoshiftWindow(int BeginTimestamp, int EndTimestamp);

/// <summary>Bonus cooldown recovery Chronoshift granted to one ability over the encounter.</summary>
/// <param name="SpellId">The ability's FSLID.</param>
/// <param name="RecoveredMs">Bonus cooldown recovery in milliseconds.</param>
public readonly record struct AbilityRecovery(int SpellId, int RecoveredMs);
