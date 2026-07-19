using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI.Components;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Models Chronoshift's cooldown recovery effect. While the player channels Chronoshift it adds 800%
/// cooldown recovery to <see cref="SpellUsable"/>'s shared recovery pool, taking an ability with no
/// haste contribution to 9× recovery. The channel lasts a fixed 3 seconds (haste-independent) unless
/// an <see cref="EndChannelEvent"/> cancels it early. The added recovery is set for the channel window
/// and cleared when it ends; because it is <i>set</i> rather than stacked, an unmatched begin/end
/// (Fellowship logs the channel end for only a minority of channels) cannot compound it.
/// </summary>
[ActiveWhen<HasChronoshiftGear>]
public sealed partial class ChronoshiftAnalyzer(Lazy<SpellUsable> spellUsable) : Analyzer
{
    /// <summary>
    /// 800% added cooldown recovery per the spell description, a term on the shared pool rather than a
    /// standalone multiplier, taking an ability with no haste contribution to 9× recovery. Matches
    /// <c>Channel.WhileActiveAddedCooldownRecovery</c> in the <c>gear_data.json</c> export.
    /// </summary>
    private const double AddedRecovery = 8.0;

    /// <summary>Chronoshift channels a fixed 3 seconds unless an EndChannel cancels it early.</summary>
    private const int ChannelDurationMs = 3000;

    private readonly List<ChronoshiftWindow> _windows = [];
    private readonly Dictionary<int, int> _recoveryBySpell = [];

    /// <summary>
    /// Cooldown remaining per spell at the open of the current window, captured <i>after</i> the added
    /// recovery is applied, so it is already the boosted-rate wallclock time each spell needs to finish.
    /// </summary>
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
        CloseWindow(e.Timestamp);

        _openTimestamp = e.Timestamp;
        _spellUsable.SetAddedCooldownRecovery(AddedRecovery, e.Timestamp);

        foreach (var spellId in _spellUsable.GetSpellsOnCooldown())
            _snapshot[spellId] = _spellUsable.CooldownRemaining(spellId, e.Timestamp);

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
    /// scheduled 3-second end, clearing the added recovery and attributing the bonus recovery each
    /// on-cooldown ability received to it. A no-op when no window is open, so a stray or duplicate
    /// close cannot drive the pool negative.
    /// </summary>
    private void CloseWindow(int timestamp)
    {
        if (_scheduledEnd is not int scheduledEnd) return;
        var closeAt = Math.Min(timestamp, scheduledEnd);
        _spellUsable.SetAddedCooldownRecovery(0.0, closeAt);

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

    public override Type? StatisticsComponentType => typeof(ChronoshiftStatistics);
}

/// <summary>A single Chronoshift channel window: the interval over which cooldown recovery was boosted.</summary>
public record ChronoshiftWindow(int BeginTimestamp, int EndTimestamp);

/// <summary>Bonus cooldown recovery Chronoshift granted to one ability over the encounter.</summary>
/// <param name="SpellId">The ability's FSLID.</param>
/// <param name="RecoveredMs">Bonus cooldown recovery in milliseconds.</param>
public readonly record struct AbilityRecovery(int SpellId, int RecoveredMs);
