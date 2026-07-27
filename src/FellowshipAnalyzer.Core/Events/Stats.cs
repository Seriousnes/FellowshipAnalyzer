namespace FellowshipAnalyzer.Core.Events;

/// <summary>A snapshot of a player's derived combat stats, as reported by <see cref="ChangeStatsEvent"/>.</summary>
public class Stats
{
    /// <summary>Intellect, the primary stat driving spell power.</summary>
    public double? Intellect { get; set; }
    /// <summary>Stamina, the stat driving maximum health.</summary>
    public double? Stamina { get; set; }
    /// <summary>Armor, reducing incoming physical damage.</summary>
    public double? Armor { get; set; }
    /// <summary>Critical strike rating.</summary>
    public double? Crit { get; set; }
    /// <summary>Haste rating, reducing cast times, GCD, and cooldowns.</summary>
    public double? Haste { get; set; }
    /// <summary>Expertise rating.</summary>
    public double? Expertise { get; set; }
    /// <summary>Spirit, driving resource or healing regeneration depending on hero.</summary>
    public double? Spirit { get; set; }

    /// <summary>Computes the per-stat difference between two snapshots, treating missing values as zero.</summary>
    public static Stats operator -(Stats a, Stats b) => new()
    {
        Intellect = (a.Intellect ?? 0) - (b.Intellect ?? 0),
        Stamina = (a.Stamina ?? 0) - (b.Stamina ?? 0),
        Armor = (a.Armor ?? 0) - (b.Armor ?? 0),
        Crit = (a.Crit ?? 0) - (b.Crit ?? 0),
        Haste = (a.Haste ?? 0) - (b.Haste ?? 0),
        Expertise = (a.Expertise ?? 0) - (b.Expertise ?? 0),
        Spirit = (a.Spirit ?? 0) - (b.Spirit ?? 0),
    };
}
