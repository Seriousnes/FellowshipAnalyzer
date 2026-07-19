using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Resolves the selected player's gem power totals into the rank effects they have unlocked.
/// </summary>
/// <remarks>
/// Each gem grants ten ranks, unlocked at rising gem-power thresholds and reported per gem as a single
/// total on <see cref="CombatantInfoEvent"/>. A "- II" rank <i>replaces</i> the rank it upgrades rather
/// than stacking with it, so only the highest unlocked rank of an effect applies. Gem power is fixed for
/// a fight, so every value here is resolved once at <see cref="FightStartEvent"/>.
///
/// Thresholds and magnitudes come from <c>external/fs_tc_uploads/s3/gear_data.json</c> -&gt; <c>Gems</c>,
/// which the spelldb pipeline does not read, so they are transcribed here. Only the cooldown ranks are
/// modelled; the remaining ranks (stat bonuses, absorbs, procs) belong here as they are needed.
/// </remarks>
public sealed partial class GemPowers : Analyzer
{
    /// <summary>Emerald "Blessing of the Commander": Ability Cooldown Reduction.</summary>
    private static readonly GemRank[] BlessingOfTheCommander =
    [
        new(RequiredGemPower: 450, Magnitude: 0.04),
        new(RequiredGemPower: 1500, Magnitude: 0.12),
    ];

    /// <summary>Diamond "Blessing of the Artisan": Relic Cooldown Reduction.</summary>
    private static readonly GemRank[] BlessingOfTheArtisan =
    [
        new(RequiredGemPower: 450, Magnitude: 0.08),
        new(RequiredGemPower: 1500, Magnitude: 0.24),
    ];

    /// <summary>
    /// Ability Cooldown Reduction granted by Emerald power, as a fraction (0.12 = 12%). Consumed by
    /// <see cref="CooldownReduction"/>.
    /// </summary>
    public double AbilityCooldownReduction { get; private set; }

    /// <summary>
    /// Relic Cooldown Reduction granted by Diamond power, as a fraction (0.24 = 24%). Relics are an
    /// equipment slot whose abilities the pipeline does not model yet, so nothing consumes this.
    /// </summary>
    public double RelicCooldownReduction { get; private set; }

    [On<FightStartEvent>]
    private void OnFightStart(FightStartEvent _)
    {
        var combatant = Owner.SelectedCombatant;
        AbilityCooldownReduction = Unlocked(BlessingOfTheCommander, combatant.Emerald);
        RelicCooldownReduction = Unlocked(BlessingOfTheArtisan, combatant.Diamond);
    }

    /// <summary>
    /// The magnitude of the highest-threshold rank that <paramref name="gemPower"/> unlocks, or 0 when it
    /// unlocks none. Magnitudes are never summed, because a higher rank replaces the one it upgrades.
    /// </summary>
    private static double Unlocked(GemRank[] ranks, int gemPower)
    {
        var threshold = 0;
        var magnitude = 0.0;

        foreach (var rank in ranks)
        {
            if (gemPower >= rank.RequiredGemPower && rank.RequiredGemPower >= threshold)
                (threshold, magnitude) = (rank.RequiredGemPower, rank.Magnitude);
        }

        return magnitude;
    }
}

/// <summary>One rank of a gem power.</summary>
/// <param name="RequiredGemPower">Gem power at which the rank unlocks.</param>
/// <param name="Magnitude">The rank's effect size, as a fraction where it is a percentage.</param>
public readonly record struct GemRank(int RequiredGemPower, double Magnitude);
