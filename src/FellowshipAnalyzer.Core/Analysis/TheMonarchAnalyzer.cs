using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// The Monarch blessing: the player's MAJOR abilities gain Cooldown Acceleration, 3% / 5% / 8% / 12% by the
/// blessing's tier, plus 0.1% for every 1% Haste the player has, up to 5% more. The blessing leaves no trace
/// in the log, and its haste part moves with the player's haste, so the modifier joins the pool
/// <see cref="StatTracker"/> tracks at the dungeon start and is replaced whenever haste changes.
/// </summary>
[ActiveWhen<HasTheMonarch>]
[After<StatTracker>]
[Dependency<StatTracker>]
[Dependency<Haste>]
public sealed partial class TheMonarchAnalyzer : Analyzer
{
    /// <summary>The blessing's name, which identifies it across the gear slots it can be slotted into.</summary>
    public const string Blessing = "The Monarch";

    private const double AccelerationPerHaste = 0.1;
    private const double HasteAccelerationCap = 0.05;

    private CooldownModifier? _modifier;

    /// <summary>
    /// The Cooldown Acceleration the blessing gives MAJOR abilities at <paramref name="haste"/>, or 0 when the
    /// player has no tier of it.
    /// </summary>
    public double AccelerationAt(double haste) =>
        TierAcceleration is var tier and > 0
            ? tier + Math.Min(Math.Max(haste, 0.0) * AccelerationPerHaste, HasteAccelerationCap)
            : 0.0;

    private double TierAcceleration => Owner.SelectedCombatant.BlessingLevel(Blessing) switch
    {
        1 => 0.03,
        2 => 0.05,
        3 => 0.08,
        >= 4 => 0.12,
        _ => 0.0,
    };

    [On<DungeonStartEvent>]
    private void OnDungeonStart(DungeonStartEvent e) => Refresh(Haste.Current, e);

    [On<ChangeHasteEvent>]
    private void OnChangeHaste(ChangeHasteEvent e) => Refresh(e.NewHaste ?? Haste.Current, e);

    private void Refresh(double haste, Event trigger)
    {
        var acceleration = AccelerationAt(haste);
        if ((_modifier?.Value ?? 0.0) == acceleration) return;

        if (_modifier is not null)
            StatTracker.RemoveCooldownModifier(CooldownPool.CooldownAcceleration, _modifier, trigger);

        _modifier = acceleration > 0 ? new CooldownModifier(acceleration, new[] { AbilityCategory.Major }) : null;
        if (_modifier is not null)
            StatTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, _modifier, trigger);
    }
}
