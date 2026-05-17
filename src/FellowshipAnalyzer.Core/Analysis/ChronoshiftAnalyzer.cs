using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks cooldown recovery applied by Asha's Chronoshift Spire during each channel window.
/// Chronoshift grants 800% increased cooldown recovery (9× total = 8× bonus beyond natural recovery).
/// The bonus CDR is applied to every spell on cooldown at channel end, with multi-charge spells
/// handled by properly restoring each charge in sequence rather than capping at one charge.
/// </summary>
public sealed class ChronoshiftAnalyzer : Analyzer
{
    /// <summary>
    /// Bonus CDR per real millisecond of channel.
    /// 800% increased = 9× total rate; natural recovery (1×) already advances via SpellUsable,
    /// so only the 8× bonus needs to be applied explicitly.
    /// </summary>
    private const int CdrBonusPerMs = 8;

    private SpellUsable _spellUsable = null!;
    private readonly List<ChronoshiftWindow> _windows = [];

    /// <summary>All Chronoshift channel windows observed for the selected player.</summary>
    public IReadOnlyList<ChronoshiftWindow> Windows => _windows;

    /// <summary>Aggregate CDR applied per spell ID across all Chronoshift windows.</summary>
    public IReadOnlyDictionary<int, int> TotalAppliedBySpell { get; private set; } =
        new Dictionary<int, int>();

    /// <summary>Aggregate CDR wasted per spell ID across all Chronoshift windows.</summary>
    public IReadOnlyDictionary<int, int> TotalWastedBySpell { get; private set; } =
        new Dictionary<int, int>();

    public override void Initialize()
    {
        Active = Owner.SelectedCombatant?.HasGear(Items.AshasChronoshiftSpire.Id) ?? false;
        if (!Active) return;

        _spellUsable = Owner.GetModule<SpellUsable>()!;

        AddEventListener(
            Events.BeginChannel.By(SELECTED_PLAYER).Spell(Spells.Chronoshift),
            OnBeginChannel);

        AddEventListener(
            Events.EndChannel.By(SELECTED_PLAYER).Spell(Spells.Chronoshift),
            OnEndChannel);
    }

    private void OnBeginChannel(BeginChannelEvent e)
    {
        _windows.Add(new ChronoshiftWindow(e.Timestamp));
    }

    private void OnEndChannel(EndChannelEvent e)
    {
        if (_windows.Count == 0) return;

        var channelDuration = e.Timestamp - e.BeginChannel.Timestamp;
        var totalCdrAvailable = channelDuration * CdrBonusPerMs;

        var spellsOnCooldown = _spellUsable.GetSpellsOnCooldown().ToList();
        var cdrBySpell = new Dictionary<int, SpellCdrRecord>(spellsOnCooldown.Count);

        foreach (var spellId in spellsOnCooldown)
        {
            var applied = _spellUsable.ReduceCooldown(spellId, totalCdrAvailable, e.Timestamp);
            cdrBySpell[spellId] = new SpellCdrRecord(spellId, Applied: applied, Wasted: totalCdrAvailable - applied);
        }

        _windows[^1] = _windows[^1] with
        {
            EndTimestamp = e.Timestamp,
            ChannelDuration = channelDuration,
            TotalCdrAvailable = totalCdrAvailable,
            CdrBySpell = cdrBySpell,
        };
    }

    public override void Complete()
    {
        var applied = new Dictionary<int, int>();
        var wasted = new Dictionary<int, int>();

        foreach (var window in _windows)
        {
            foreach (var (spellId, record) in window.CdrBySpell)
            {
                applied[spellId] = applied.GetValueOrDefault(spellId) + record.Applied;
                wasted[spellId] = wasted.GetValueOrDefault(spellId) + record.Wasted;
            }
        }

        TotalAppliedBySpell = applied;
        TotalWastedBySpell = wasted;
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
