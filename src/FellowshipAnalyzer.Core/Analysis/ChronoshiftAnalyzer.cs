using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks cooldown recovery applied by Asha's Chronoshift Spire during each channel window.
/// Chronoshift grants 800% increased cooldown recovery (9× total rate). The rate is applied
/// continuously across the channel window via <see cref="SpellUsable"/>'s rate API, which:
/// <list type="bullet">
///   <item><description>Rescales every in-flight cooldown by 9× when the channel begins.</description></item>
///   <item><description>Makes any cooldown started during the channel start 9× shorter automatically.</description></item>
///   <item><description>Lets cooldowns that complete mid-channel fire <see cref="UpdateSpellUsableEvent"/>
///     at their true historical expiry rather than at channel end.</description></item>
/// </list>
/// Per-window applied/wasted CDR is reconstructed from the observed
/// <see cref="UpdateSpellUsableEvent"/>s between channel begin and end.
/// </summary>
[ActiveWhen<HasChronoshiftGear>]
public sealed partial class ChronoshiftAnalyzer(Lazy<SpellUsable> spellUsable) : Analyzer
{
    private const double ChronoshiftRate = 9.0;

    /// <summary>
    /// Bonus CDR per real millisecond of channel: 800% increased = 9× total rate; natural recovery (1×)
    /// is already counted by the spell's own cooldown timer, so the "bonus" portion is 8× per ms.
    /// </summary>
    private const int CdrBonusPerMs = 8;

    private readonly List<ChronoshiftWindow> _windows = [];

    private readonly Dictionary<int, int> _windowStartExpectedEnds = [];
    private readonly Dictionary<int, int> _windowAppliedBySpell = [];
    private bool _windowActive;

    /// <summary>All Chronoshift channel windows observed for the selected player.</summary>
    public IReadOnlyList<ChronoshiftWindow> Windows => _windows;

    /// <summary>Aggregate CDR applied per spell ID across all Chronoshift windows.</summary>
    public IReadOnlyDictionary<int, int> TotalAppliedBySpell => AggregateBySpell(static record => record.Applied);

    /// <summary>Aggregate CDR wasted per spell ID across all Chronoshift windows.</summary>
    public IReadOnlyDictionary<int, int> TotalWastedBySpell => AggregateBySpell(static record => record.Wasted);

    private Dictionary<int, int> AggregateBySpell(Func<SpellCdrRecord, int> amount)
    {
        var totals = new Dictionary<int, int>();
        foreach (var window in _windows)
        {
            foreach (var (spellId, record) in window.CdrBySpell)
                totals[spellId] = totals.GetValueOrDefault(spellId) + amount(record);
        }
        return totals;
    }

    [On<BeginChannelEvent>(By = Actor.Player, Spell = nameof(Spells.Chronoshift))]
    private void OnBeginChannel(BeginChannelEvent e)
    {
        _windows.Add(new ChronoshiftWindow(e.Timestamp));

        _windowStartExpectedEnds.Clear();
        _windowAppliedBySpell.Clear();
        foreach (var spellId in _spellUsable.GetSpellsOnCooldown())
            _windowStartExpectedEnds[spellId] = e.Timestamp + _spellUsable.CooldownRemaining(spellId, e.Timestamp);
        _windowActive = true;

        _spellUsable.ApplyCooldownRateChangeToAll(ChronoshiftRate, e.Timestamp);
    }

    [On<EndChannelEvent>(By = Actor.Player, Spell = nameof(Spells.Chronoshift))]
    private void OnEndChannel(EndChannelEvent e)
    {
        if (_windows.Count == 0) return;

        _spellUsable.RemoveCooldownRateChangeFromAll(ChronoshiftRate, e.Timestamp);
        _windowActive = false;

        var channelDuration = e.Timestamp - e.BeginChannel.Timestamp;
        var totalCdrAvailable = channelDuration * CdrBonusPerMs;

        var totalApplied = 0;
        foreach (var amount in _windowAppliedBySpell.Values)
            totalApplied += amount;
        var wasted = Math.Max(0, totalCdrAvailable - totalApplied);

        var cdrBySpell = new Dictionary<int, SpellCdrRecord>(_windowAppliedBySpell.Count);
        foreach (var (spellId, applied) in _windowAppliedBySpell)
            cdrBySpell[spellId] = new SpellCdrRecord(spellId, Applied: applied, Wasted: 0);

        if (cdrBySpell.Count > 0 && wasted > 0)
        {
            cdrBySpell[0] = new SpellCdrRecord(SpellId: 0, Applied: 0, Wasted: wasted);
        }

        _windows[^1] = _windows[^1] with
        {
            EndTimestamp = e.Timestamp,
            ChannelDuration = channelDuration,
            TotalCdrAvailable = totalCdrAvailable,
            CdrBySpell = cdrBySpell,
        };
    }

    [On<UpdateSpellUsableEvent>]
    private void OnUpdateSpellUsable(UpdateSpellUsableEvent e)
    {
        if (!_windowActive) return;

        var spellId = e.Ability.FSLID;
        switch (e.UpdateType)
        {
            case UpdateSpellUsableType.ChangeCooldownRate:
                if (_windowStartExpectedEnds.TryGetValue(spellId, out var initialEnd))
                {
                    var applied = Math.Max(0, initialEnd - e.ExpectedRechargeTimestamp);
                    _windowAppliedBySpell[spellId] = applied;
                }
                break;

            case UpdateSpellUsableType.EndCooldown:
            case UpdateSpellUsableType.RestoreCharge:
                if (_windowStartExpectedEnds.TryGetValue(spellId, out var initialEnd2))
                {
                    var applied = Math.Max(0, initialEnd2 - e.Timestamp);
                    _windowAppliedBySpell[spellId] = applied;
                }
                break;
        }
    }
}

/// <summary>
/// Represents a single Chronoshift channel window and the CDR it distributed.
/// <see cref="CdrBySpell"/> is populated when the channel ends; it is empty for
/// windows that have not yet completed (in-progress or no EndChannel received).
/// </summary>
public record ChronoshiftWindow(
    int BeginTimestamp,
    int EndTimestamp = 0,
    int ChannelDuration = 0,
    int TotalCdrAvailable = 0,
    IReadOnlyDictionary<int, SpellCdrRecord>? CdrBySpell = null)
{
    public IReadOnlyDictionary<int, SpellCdrRecord> CdrBySpell { get; init; } =
        CdrBySpell ?? new Dictionary<int, SpellCdrRecord>();
}

/// <summary>
/// Per-spell CDR record for a single Chronoshift window.
/// </summary>
/// <param name="SpellId">The spell that received cooldown reduction.</param>
/// <param name="Applied">Milliseconds of CDR that actually reduced the remaining cooldown.</param>
/// <param name="Wasted">Milliseconds of CDR that exceeded the remaining cooldown and had no effect.</param>
public record SpellCdrRecord(int SpellId, int Applied, int Wasted);
