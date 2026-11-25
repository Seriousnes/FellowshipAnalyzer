using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Xavian;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using XavianTalents = FellowshipAnalyzer.Core.Common.Spells.XavianTalents;

namespace FellowshipAnalyzer.Heroes.Xavian.Modules;

/// <summary>Span during which Solar Shield was active on at least one target of the player.</summary>
public sealed record InvictusWindow(int BeginTimestamp, int EndTimestamp);

/// <summary>
/// Invictus: while the player's Solar Shield is active on anyone, Cooldown Recovery is increased by 1% for every 3%
/// Haste. The bonus joins <see cref="StatTracker"/>'s Cooldown Recovery pool as an <see cref="ModifierOp.AddFinal"/>
/// term of the export's coefficient times the player's Haste, and is replaced whenever Haste changes.
/// </summary>
[RequiresTalent(XavianTalents.Invictus)]
[Dependency<StatTracker>]
[Dependency<Haste>]
[Before<SpellUsable>]
public sealed partial class InvictusAnalyzer : Analyzer
{
    private static readonly double Coefficient = ReadCoefficient();

    private readonly HashSet<(int TargetId, int? TargetInstance)> _shielded = [];
    private readonly List<InvictusWindow> _closedWindows = [];

    private CooldownModifier? _modifier;
    private int _openedAt;

    /// <summary>Every span Solar Shield was active on the player or an ally, in the order the spans began. An open span ends with the dungeon.</summary>
    public IReadOnlyList<InvictusWindow> Windows =>
        _shielded.Count > 0
            ? [.. _closedWindows, new InvictusWindow(_openedAt, Math.Max(_openedAt, Owner.DungeonEndTime))]
            : _closedWindows;

    /// <summary>Milliseconds Solar Shield was active on at least one target.</summary>
    public int UptimeMs => Windows.Sum(window => window.EndTimestamp - window.BeginTimestamp);

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.SolarShieldAbsorb))]
    private void OnApplied(ApplyBuffEvent e)
    {
        var wasEmpty = _shielded.Count == 0;
        _shielded.Add((e.TargetId, e.TargetInstance));
        if (!wasEmpty) return;

        _openedAt = e.Timestamp;
        Replace(Haste.Current, e);
    }

    [On<RemoveBuffEvent>(By = Actor.Player, Spell = nameof(Spells.SolarShieldAbsorb))]
    private void OnRemoved(RemoveBuffEvent e)
    {
        if (!_shielded.Remove((e.TargetId, e.TargetInstance)) || _shielded.Count > 0) return;

        _closedWindows.Add(new InvictusWindow(_openedAt, Math.Max(_openedAt, e.Timestamp)));
        Revoke(e);
    }

    [On<ChangeHasteEvent>]
    private void OnChangeHaste(ChangeHasteEvent e)
    {
        if (_shielded.Count == 0) return;

        Replace(e.NewHaste ?? Haste.Current, e);
    }

    private void Replace(double haste, Event trigger)
    {
        Revoke(trigger);

        _modifier = new CooldownModifier(Coefficient * Math.Max(haste, 0.0)) { Op = ModifierOp.AddFinal };
        StatTracker.AddCooldownModifier(CooldownPool.CooldownRecovery, _modifier, trigger, trigger.Timestamp);
    }

    private void Revoke(Event trigger)
    {
        if (_modifier is not { } active) return;

        StatTracker.RemoveCooldownModifier(CooldownPool.CooldownRecovery, active, trigger, trigger.Timestamp);
        _modifier = null;
    }

    private static double ReadCoefficient()
    {
        foreach (var modifier in EffectData.ModifiersOf(Spells.InvictusCooldownRecovery.Id))
        {
            if (modifier.Attribute == StatAttribute.CooldownRecovery && modifier.Coefficient is { } coefficient)
                return coefficient;
        }

        throw new InvalidOperationException($"The export gives effect {Spells.InvictusCooldownRecovery.Id} no Cooldown Recovery coefficient.");
    }
}
