using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Event Horizon: while the Event Horizon buff is on the player, the Cooldown Recovery attribute is
/// multiplied by 1 plus the player's Haste, compounding on every other Cooldown Recovery term. The
/// multiplier follows the player's Haste while the buff is on.
/// </summary>
[Before<SpellUsable>]
[Dependency<StatTracker>]
[Dependency<Haste>]
public sealed partial class EventHorizonAccelerationAnalyzer : Analyzer
{
    private CooldownModifier? _current;

    /// <summary>The Cooldown Recovery multiplier Event Horizon applies at the moment, or 1 outside the buff.</summary>
    public double CurrentMultiplier => _current?.Value ?? 1.0;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.EventHorizonBuff))]
    private void OnBuffApplied(ApplyBuffEvent e) => Set(Haste.Current, e);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.EventHorizonBuff))]
    private void OnBuffRemoved(RemoveBuffEvent e)
    {
        if (_current is null) return;

        StatTracker.RemoveCooldownModifier(CooldownPool.CooldownRecovery, _current, e);
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
        var next = new CooldownModifier(1.0 + Math.Max(haste, 0.0) * SpellTuning.EventHorizon.HasteToCooldownRecovery)
        {
            Op = ModifierOp.MultiplyCompound,
        };
        if (_current == next) return;

        if (_current is not null) StatTracker.RemoveCooldownModifier(CooldownPool.CooldownRecovery, _current, trigger);
        StatTracker.AddCooldownModifier(CooldownPool.CooldownRecovery, next, trigger);
        _current = next;
    }
}
