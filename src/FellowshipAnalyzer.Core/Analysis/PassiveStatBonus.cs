namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// An always-on stat contribution that no apply or remove event marks, which a source hands to
/// <see cref="StatTracker.SetPassiveBonus"/>. Flat percentages are fractions (0.04 = 4%) added after the
/// rating conversion; ratings are added before it; Ability Cooldown Reduction joins that pool unscoped.
/// Every value is on top of what the combatantinfo already reports.
/// </summary>
public sealed record PassiveStatBonus
{
    /// <summary>A contribution of nothing, which withdraws a source's previous contribution.</summary>
    public static PassiveStatBonus None { get; } = new();

    /// <summary>Flat critical strike chance.</summary>
    public double Crit { get; init; }

    /// <summary>Flat haste.</summary>
    public double Haste { get; init; }

    /// <summary>Flat expertise.</summary>
    public double Expertise { get; init; }

    /// <summary>Flat spirit.</summary>
    public double Spirit { get; init; }

    /// <summary>Critical strike rating.</summary>
    public double CritRating { get; init; }

    /// <summary>Haste rating.</summary>
    public double HasteRating { get; init; }

    /// <summary>Expertise rating.</summary>
    public double ExpertiseRating { get; init; }

    /// <summary>Spirit rating.</summary>
    public double SpiritRating { get; init; }

    /// <summary>Ability Cooldown Reduction, as a fraction of every ability's cooldown.</summary>
    public double AbilityCooldownReduction { get; init; }

    /// <summary>Whether any stat, as opposed to cooldown, value is set.</summary>
    public bool HasStats =>
        Crit != 0 || Haste != 0 || Expertise != 0 || Spirit != 0
        || CritRating != 0 || HasteRating != 0 || ExpertiseRating != 0 || SpiritRating != 0;

    /// <summary>Adds two contributions value by value.</summary>
    public static PassiveStatBonus operator +(PassiveStatBonus a, PassiveStatBonus b) => new()
    {
        Crit = a.Crit + b.Crit,
        Haste = a.Haste + b.Haste,
        Expertise = a.Expertise + b.Expertise,
        Spirit = a.Spirit + b.Spirit,
        CritRating = a.CritRating + b.CritRating,
        HasteRating = a.HasteRating + b.HasteRating,
        ExpertiseRating = a.ExpertiseRating + b.ExpertiseRating,
        SpiritRating = a.SpiritRating + b.SpiritRating,
        AbilityCooldownReduction = a.AbilityCooldownReduction + b.AbilityCooldownReduction,
    };

    /// <summary>Subtracts one contribution from another value by value.</summary>
    public static PassiveStatBonus operator -(PassiveStatBonus a, PassiveStatBonus b) => new()
    {
        Crit = a.Crit - b.Crit,
        Haste = a.Haste - b.Haste,
        Expertise = a.Expertise - b.Expertise,
        Spirit = a.Spirit - b.Spirit,
        CritRating = a.CritRating - b.CritRating,
        HasteRating = a.HasteRating - b.HasteRating,
        ExpertiseRating = a.ExpertiseRating - b.ExpertiseRating,
        SpiritRating = a.SpiritRating - b.SpiritRating,
        AbilityCooldownReduction = a.AbilityCooldownReduction - b.AbilityCooldownReduction,
    };
}
