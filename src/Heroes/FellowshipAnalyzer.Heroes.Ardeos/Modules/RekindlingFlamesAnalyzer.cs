using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Tracks Rekindling Flames (Ardeos base trait): each enemy that dies while carrying open Engulfing
/// Flames damage-over-time windows reduces Engulfing Flames' remaining cooldown by
/// <see cref="Spell.CooldownReductionOnTargetDeath"/> seconds per open window. The request is applied
/// through <see cref="SpellUsable"/> so it flows onto the cooldown timeline; the portion that shortened
/// a running cooldown or returned a charge is effective, and any surplus beyond the spell's charge cap
/// (six windows request 60s against a two-charge, twenty-second spell) is wasted.
/// </summary>
/// <remarks>
/// Only enemies carry the player-applied Engulfing Flames DoT, so a player or pet death resolves to
/// zero open windows and short-circuits without an explicit hostility check. Open-window count is read
/// from the all-units aura registry at the death timestamp, matching how many independent DoT instances
/// were live on the dying unit.
/// </remarks>
[Uses<Combatants>]
public sealed partial class RekindlingFlamesAnalyzer : Analyzer
{
    /// <summary>Enemy deaths that carried at least one open Engulfing Flames DoT window.</summary>
    public int QualifyingDeaths { get; private set; }

    /// <summary>
    /// Total Engulfing Flames cooldown reduction the qualifying deaths generated, in milliseconds, after
    /// Ability Cooldown Reduction has scaled the flat per-window request.
    /// </summary>
    public int TotalGeneratedReductionMs { get; private set; }

    /// <summary>Generated reduction that shortened a running cooldown or returned a charge, in milliseconds.</summary>
    public int EffectiveReductionMs { get; private set; }

    /// <summary>Generated reduction that overflowed the charge cap and shortened nothing, in milliseconds.</summary>
    public int WastedReductionMs => TotalGeneratedReductionMs - EffectiveReductionMs;

    [On<DeathEvent>]
    private void OnDeath(DeathEvent e)
    {
        var windows = Combatants.AuraInstanceCount(e.TargetId, e.TargetInstance, Spells.EngulfingFlamesDot.FSLID, e.Timestamp);
        if (windows == 0)
            return;

        var perWindowMs = (int)((Spells.EngulfingFlames.CooldownReductionOnTargetDeath ?? 0) * 1000);
        var requestedMs = windows * perWindowMs;
        var reduction = Owner.SpellUsable!.ReduceCooldown(Spells.EngulfingFlames.Id, requestedMs, e.Timestamp);

        QualifyingDeaths++;
        TotalGeneratedReductionMs += reduction.GeneratedMs;
        EffectiveReductionMs += reduction.AppliedMs;
    }
}
