namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// A snapshot of a player's combat stats, as reported by <see cref="ChangeStatsEvent"/>. Each stat has two
/// independent channels: a rating, which converts to a percentage through Fellowship's diminishing-returns
/// curve, and an <c>Additional</c> flat percentage, which is added to the converted rating afterwards and is
/// never subject to diminishing returns.
/// </summary>
public class Stats
{
    /// <summary>
    /// Main stat rating: Strength, Agility, or Intellect, whichever the hero scales from. A combatantinfo
    /// carries all three slots but populates only the hero's own, so they collapse to one channel here.
    /// </summary>
    public double? MainStat { get; set; }
    /// <summary>Stamina, the stat driving maximum health.</summary>
    public double? Stamina { get; set; }
    /// <summary>Armor, reducing incoming physical damage.</summary>
    public double? Armor { get; set; }
    /// <summary>Critical strike rating.</summary>
    public double? Crit { get; set; }
    /// <summary>Haste rating, reducing cast times, GCD, and cooldowns.</summary>
    public double? Haste { get; set; }
    /// <summary>Expertise rating, increasing damage, healing, and shields.</summary>
    public double? Expertise { get; set; }
    /// <summary>Spirit rating, driving ultimate resource generation and hero-specific procs.</summary>
    public double? Spirit { get; set; }

    /// <summary>Flat critical strike chance added after the rating conversion, as a fraction (0.04 = 4%).</summary>
    public double? AdditionalCrit { get; set; }
    /// <summary>Flat haste added after the rating conversion, as a fraction (0.30 = 30%).</summary>
    public double? AdditionalHaste { get; set; }
    /// <summary>Flat expertise added after the rating conversion, as a fraction (0.20 = 20%).</summary>
    public double? AdditionalExpertise { get; set; }
    /// <summary>Flat spirit added after the rating conversion, as a fraction (0.20 = 20%).</summary>
    public double? AdditionalSpirit { get; set; }
    /// <summary>Flat critical strike power added to the base critical multiplier, as a fraction (0.20 = 20%).</summary>
    public double? AdditionalCritPower { get; set; }
    /// <summary>Flat movement speed added to the base movement rate, as a fraction (0.15 = 15%).</summary>
    public double? AdditionalMoveSpeed { get; set; }
    /// <summary>Flat reduction of incoming damage, as a fraction (0.05 = 5% less damage taken).</summary>
    public double? AdditionalDamageReduction { get; set; }

    /// <summary>Computes the per-stat difference between two snapshots, treating missing values as zero.</summary>
    public static Stats operator -(Stats a, Stats b) => new()
    {
        MainStat = (a.MainStat ?? 0) - (b.MainStat ?? 0),
        Stamina = (a.Stamina ?? 0) - (b.Stamina ?? 0),
        Armor = (a.Armor ?? 0) - (b.Armor ?? 0),
        Crit = (a.Crit ?? 0) - (b.Crit ?? 0),
        Haste = (a.Haste ?? 0) - (b.Haste ?? 0),
        Expertise = (a.Expertise ?? 0) - (b.Expertise ?? 0),
        Spirit = (a.Spirit ?? 0) - (b.Spirit ?? 0),
        AdditionalCrit = (a.AdditionalCrit ?? 0) - (b.AdditionalCrit ?? 0),
        AdditionalHaste = (a.AdditionalHaste ?? 0) - (b.AdditionalHaste ?? 0),
        AdditionalExpertise = (a.AdditionalExpertise ?? 0) - (b.AdditionalExpertise ?? 0),
        AdditionalSpirit = (a.AdditionalSpirit ?? 0) - (b.AdditionalSpirit ?? 0),
        AdditionalCritPower = (a.AdditionalCritPower ?? 0) - (b.AdditionalCritPower ?? 0),
        AdditionalMoveSpeed = (a.AdditionalMoveSpeed ?? 0) - (b.AdditionalMoveSpeed ?? 0),
        AdditionalDamageReduction = (a.AdditionalDamageReduction ?? 0) - (b.AdditionalDamageReduction ?? 0),
    };
}
