using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Resolves the always-on cooldown acceleration a legendary item grants through its built-in
/// "Strand of Eternity" effect, as a flat term on the shared cooldown-recovery pool
/// (<see cref="SpellUsable.EffectiveRate"/>).
/// </summary>
/// <remarks>
/// Every legendary carries Strand of Eternity (+10% cooldown acceleration) and only one legendary may
/// be equipped, so the contribution is a flat +10% whenever the player wears one. This is the same
/// additive pool haste and Chronoshift feed, so it composes additively rather than multiplying: it
/// takes a non-hasted ability from 1× to 1.1× recovery, and to 9.1× while Chronoshift channels.
///
/// Item effects are not in the generated gear pipeline yet, so the effect is recognised by the
/// legendary quality tier here (mirroring <see cref="GemPowers"/>). Gear is fixed for a fight, so the
/// value is resolved once at <see cref="FightStartEvent"/>; <see cref="SpellUsable"/> reads
/// <see cref="Current"/> when a cooldown starts and never rescales one in flight.
/// </remarks>
public sealed partial class GearCooldownRecovery(Lazy<Combatants> combatants) : Analyzer
{
    /// <summary>Quality tier of a legendary item; the top rarity, of which only one may be equipped.</summary>
    private const int LegendaryQuality = 6;

    /// <summary>The cooldown acceleration a legendary's Strand of Eternity grants, as a fraction (0.10 = +10%).</summary>
    private const double StrandOfEternityAcceleration = 0.10;

    /// <summary>
    /// Flat cooldown acceleration from equipped gear, as a fraction (0.10 = +10%). Consumed by
    /// <see cref="SpellUsable"/> as a constant term on its recovery pool.
    /// </summary>
    public double Current { get; private set; }

    [On<FightStartEvent>]
    private void OnFightStart(FightStartEvent _)
    {
        if (_combatants.Selected.Gear.Any(item => item.Quality >= LegendaryQuality))
            Current = StrandOfEternityAcceleration;
    }
}
