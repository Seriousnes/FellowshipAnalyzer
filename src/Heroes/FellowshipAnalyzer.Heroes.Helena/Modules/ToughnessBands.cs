namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>
/// The five Toughness bands, named for the <c>LowerThresholdLevel</c> keys in the Season 3
/// <c>Constants.Toughness</c> block. Membership is the player's Toughness as a share of that event's
/// own maximum, which moves with Strength and secondary stats, so a band is always resolved from the
/// snapshot's own <c>max</c> rather than a fixed number.
/// </summary>
public enum ToughnessBand
{
    /// <summary>Below the first threshold, granting no physical damage reduction.</summary>
    Depleted,

    /// <summary>At or above 0.1% of maximum Toughness, granting 5% physical damage reduction.</summary>
    Level1,

    /// <summary>At or above 25% of maximum Toughness, granting 15% physical damage reduction.</summary>
    Level2,

    /// <summary>At or above 50% of maximum Toughness, granting 25% physical damage reduction.</summary>
    Level3,

    /// <summary>At or above 75% of maximum Toughness, granting 35% physical damage reduction.</summary>
    Level4,
}

/// <summary>
/// The Season 3 <c>Constants.Toughness</c> thresholds and the physical damage reduction each band
/// grants, plus the strength scalers each generation source contributes.
/// </summary>
public static class ToughnessBands
{
    /// <summary><c>MaxToughnessStrengthScaler</c>: maximum Toughness is this multiple of Strength before the secondary-stat modifier.</summary>
    public const double MaxToughnessStrengthScaler = 7.7;

    /// <summary>Every band from <see cref="ToughnessBand.Depleted"/> upwards.</summary>
    public static IReadOnlyList<ToughnessBand> All { get; } =
        [ToughnessBand.Depleted, ToughnessBand.Level1, ToughnessBand.Level2, ToughnessBand.Level3, ToughnessBand.Level4];

    /// <summary>Resolves the band a Toughness share of maximum falls into.</summary>
    public static ToughnessBand For(double shareOfMaximum) => shareOfMaximum switch
    {
        >= 0.75 => ToughnessBand.Level4,
        >= 0.50 => ToughnessBand.Level3,
        >= 0.25 => ToughnessBand.Level2,
        >= 0.001 => ToughnessBand.Level1,
        _ => ToughnessBand.Depleted,
    };

    /// <summary>The share of maximum Toughness at which <paramref name="band"/> begins.</summary>
    public static double LowerThreshold(ToughnessBand band) => band switch
    {
        ToughnessBand.Level4 => 0.75,
        ToughnessBand.Level3 => 0.50,
        ToughnessBand.Level2 => 0.25,
        ToughnessBand.Level1 => 0.001,
        _ => 0,
    };

    /// <summary>The physical damage reduction <paramref name="band"/> grants, as a fraction.</summary>
    public static double DamageReduction(ToughnessBand band) => band switch
    {
        ToughnessBand.Level4 => 0.35,
        ToughnessBand.Level3 => 0.25,
        ToughnessBand.Level2 => 0.15,
        ToughnessBand.Level1 => 0.05,
        _ => 0,
    };

    /// <summary>
    /// The nominal share of maximum Toughness a generation source restores, taken from its
    /// <c>StrengthScaler</c> over <see cref="MaxToughnessStrengthScaler"/>. Nominal because maximum
    /// Toughness also carries <c>MaxToughnessTotalSecondaryStatModifier</c>, so the realised share is
    /// lower the more secondary stats the player has; the value is for display, never for
    /// reconstructing the resource.
    /// </summary>
    public static double NominalGeneration(double strengthScaler) => strengthScaler / MaxToughnessStrengthScaler;
}
