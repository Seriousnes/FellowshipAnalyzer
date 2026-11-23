using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// Registers the stat effects Aeona's own kit grants that the shared <see cref="StatBuffs"/> tables do
/// not list, so <see cref="StatTracker"/> tracks them while they are up on the player.
/// </summary>
/// <remarks>
/// Fleeting Hour (effect 2744) joins the Cooldown Recovery attribute with the value of its export modifier,
/// so it speeds only the cooldowns that scale off Cooldown Recovery.
/// </remarks>
[Dependency<StatTracker>]
public sealed partial class AeonaStatBuffs : Analyzer
{
    private static readonly CooldownModifier FleetingHourCooldownRecovery =
        CooldownRecoveryModifiers.Of(Spells.FleetingHourSelfBuff);

    [On<DungeonStartEvent>]
    private void OnDungeonStart(DungeonStartEvent e) =>
        StatTracker.AddCooldownBuff(
            Spells.FleetingHourSelfBuff.FSLID,
            new CooldownBuff(CooldownRecovery: FleetingHourCooldownRecovery));
}
