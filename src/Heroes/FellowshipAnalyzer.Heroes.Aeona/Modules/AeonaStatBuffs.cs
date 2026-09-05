using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// Registers the stat effects Aeona's own kit grants that the shared <see cref="StatBuffs"/> tables do
/// not carry, so <see cref="StatTracker"/> tracks them while they are up on the player.
/// </summary>
/// <remarks>
/// Fleeting Hour (effect 2744) grants +50% Cooldown Acceleration for its 15 second duration, which
/// every other off-cooldown metric in the hero has to see or it reads availability too late.
/// </remarks>
[Dependency<StatTracker>]
public sealed partial class AeonaStatBuffs : Analyzer
{
    private const double FleetingHourCooldownAcceleration = 0.5;

    [On<DungeonStartEvent>]
    private void OnDungeonStart(DungeonStartEvent e) =>
        StatTracker.AddCooldownBuff(
            Spells.FleetingHourSelfBuff.FSLID,
            new CooldownBuff(CooldownAcceleration: new CooldownModifier(FleetingHourCooldownAcceleration)));
}
