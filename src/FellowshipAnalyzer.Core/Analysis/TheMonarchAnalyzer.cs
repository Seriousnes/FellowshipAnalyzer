using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
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

    private CooldownModifier? _modifier;

    /// <summary>
    /// The Cooldown Acceleration the blessing gives MAJOR abilities at <paramref name="haste"/>, or 0 when the
    /// player has no tier of it.
    /// </summary>
    public double AccelerationAt(double haste)
    {
        var tier = Owner.SelectedCombatant.BlessingLevel(Blessing);
        if (tier <= 0) return 0.0;

        var perHaste = ByTier(GlyphTuning.TheMonarch.BonusIncreasePerHasteInPercentage, tier);
        var cap = perHaste * ByTier(GlyphTuning.TheMonarch.HasteThreshold, tier);
        return ByTier(GlyphTuning.TheMonarch.CooldownAcceleration, tier) + Math.Min(Math.Max(haste, 0.0) * perHaste, cap);
    }

    private static double ByTier(ReadOnlySpan<double> values, int tier) => values[Math.Min(tier, values.Length) - 1];

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
