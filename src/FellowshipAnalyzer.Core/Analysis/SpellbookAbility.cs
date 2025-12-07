namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Defines a spell's gameplay metadata for the spellbook.
/// This is the input definition — hero analyzers override <see cref="Abilities.Spellbook"/>
/// to provide these entries.
/// </summary>
public sealed record SpellbookAbility
{
    /// <summary>
    /// The primary spell ID. If an ability has multiple associated spell IDs
    /// (e.g. main-hand and off-hand), list the primary here and extras in
    /// <see cref="AdditionalSpellIds"/>.
    /// </summary>
    public required int Spell { get; init; }

    /// <summary>
    /// Additional spell IDs tied to this ability (e.g. off-hand hits, buff IDs).
    /// These share the same cooldown and are treated as the same ability.
    /// </summary>
    public int[]? AdditionalSpellIds { get; init; }

    /// <summary>
    /// The display name override. If null, the name is resolved from the
    /// <see cref="HeroAnalysisDefinition"/> or the combat log.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The category of the spell (Rotational, Cooldowns, Defensive, etc.).
    /// </summary>
    public required SpellCategory Category { get; init; }

    /// <summary>
    /// The base cooldown in seconds. For abilities whose cooldown scales with haste,
    /// use <see cref="CooldownWithHaste"/> instead.
    /// </summary>
    public double? Cooldown { get; init; }

    /// <summary>
    /// A function that returns the cooldown in seconds given the player's current
    /// haste multiplier (e.g. 1.2 for 20% haste). Use this for haste-scaling cooldowns.
    /// Takes precedence over <see cref="Cooldown"/> when set.
    /// </summary>
    public Func<double, double>? CooldownWithHaste { get; init; }

    /// <summary>
    /// The number of charges the ability has. Defaults to 1 (no extra charges).
    /// Only one charge recharges at a time.
    /// </summary>
    public int Charges { get; init; } = 1;

    /// <summary>
    /// GCD information. Null means the spell is off the GCD.
    /// </summary>
    public GcdInfo? Gcd { get; init; }

    /// <summary>
    /// Cast efficiency configuration for suggestions.
    /// </summary>
    public CastEfficiencyInfo? CastEfficiency { get; init; }

    /// <summary>
    /// Whether the ability is available to the player. Set to false for
    /// abilities gated behind talents the player doesn't have.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// The spell's range in yards/meters.
    /// </summary>
    public int? Range { get; init; }

    /// <summary>
    /// Whether the spell is a defensive ability.
    /// </summary>
    public bool IsDefensive { get; init; }

    /// <summary>
    /// Gets the effective cooldown in seconds, applying haste if a
    /// <see cref="CooldownWithHaste"/> function is provided.
    /// </summary>
    public double GetCooldown(double haste = 1.0)
    {
        if (CooldownWithHaste is not null)
        {
            return CooldownWithHaste(haste);
        }

        return Cooldown ?? 0;
    }
}

/// <summary>
/// GCD configuration for a spell.
/// </summary>
public sealed record GcdInfo
{
    /// <summary>
    /// The base GCD in milliseconds before haste reduction.
    /// Typically 1500ms for most abilities.
    /// </summary>
    public double? Base { get; init; }

    /// <summary>
    /// A fixed GCD in milliseconds that is not affected by haste.
    /// </summary>
    public double? Static { get; init; }

    /// <summary>
    /// The minimum GCD in milliseconds (floor after haste reduction).
    /// </summary>
    public double? Minimum { get; init; }
}

/// <summary>
/// Configuration for cast efficiency suggestions.
/// </summary>
public sealed record CastEfficiencyInfo
{
    /// <summary>
    /// Whether a suggestion should be shown when efficiency is below the threshold.
    /// </summary>
    public bool Suggestion { get; init; }

    /// <summary>
    /// The recommended cast efficiency as a ratio (0.0–1.0). Default is 0.8.
    /// </summary>
    public double RecommendedEfficiency { get; init; } = 0.8;

    /// <summary>
    /// The efficiency threshold for an average-severity issue.
    /// </summary>
    public double? AverageIssueEfficiency { get; init; }

    /// <summary>
    /// The efficiency threshold for a major-severity issue.
    /// </summary>
    public double? MajorIssueEfficiency { get; init; }
}
