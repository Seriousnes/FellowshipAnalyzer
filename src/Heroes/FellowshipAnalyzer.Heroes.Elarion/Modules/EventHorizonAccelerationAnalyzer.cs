using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Event Horizon: while the Event Horizon buff is on the player, Cooldown Acceleration increases by an
/// amount equal to the player's Haste. The term follows every Haste change inside the buff.
/// </summary>
[Before<SpellUsable>]
[Dependency<StatTracker>]
[Dependency<Haste>]
public sealed partial class EventHorizonAccelerationAnalyzer : Analyzer
{
    private CooldownModifier? _current;

    /// <summary>The Cooldown Acceleration term Event Horizon adds at the moment, or 0 outside the buff.</summary>
    public double CurrentAcceleration => _current?.Value ?? 0.0;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.EventHorizonBuff))]
    private void OnBuffApplied(ApplyBuffEvent e) => Set(Haste.Current, e);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.EventHorizonBuff))]
    private void OnBuffRemoved(RemoveBuffEvent e)
    {
        if (_current is null) return;

        StatTracker.RemoveCooldownModifier(CooldownPool.CooldownAcceleration, _current, e);
        _current = null;
    }

    [On<ChangeHasteEvent>]
    private void OnHasteChanged(ChangeHasteEvent e)
    {
        if (_current is null) return;

        Set(e.NewHaste ?? Haste.Current, e);
    }

    private void Set(double haste, Event trigger)
    {
        var next = new CooldownModifier(Math.Max(haste, 0.0));
        if (_current == next) return;

        if (_current is not null) StatTracker.RemoveCooldownModifier(CooldownPool.CooldownAcceleration, _current, trigger);
        StatTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, next, trigger);
        _current = next;
    }
}
