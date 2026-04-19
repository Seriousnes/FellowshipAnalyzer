using OneOf;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A cooldown value — either a fixed number of seconds, or a function of the
/// player's haste multiplier (e.g. 1.2 for 20% haste) that returns seconds.
/// </summary>
[GenerateOneOf]
public partial class CooldownValue : OneOfBase<double, Func<double, double>>;

/// <summary>
/// A GCD sub-value — either a fixed number of milliseconds, or a function of
/// the player's <see cref="Combatant"/> that returns milliseconds.
/// </summary>
[GenerateOneOf]
public partial class GcdValue : OneOfBase<double, Func<Combatant, double>>;

/// <summary>
/// A charges value — either a fixed count, or a function of the player's
/// <see cref="Combatant"/> that returns the count.
/// </summary>
[GenerateOneOf]
public partial class ChargesValue : OneOfBase<int, Func<Combatant, int>>;

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
    /// The cooldown in seconds. Can be a fixed value or a function of the
    /// player's haste multiplier (e.g. 1.2 for 20% haste).
    /// </summary>
    public CooldownValue? Cooldown { get; init; }

    /// <summary>
    /// The number of charges the ability has. Defaults to 1 (no extra charges).
    /// Only one charge recharges at a time. Can be a fixed value or a function
    /// of the player's <see cref="Combatant"/>.
    /// </summary>
    public ChargesValue Charges { get; init; } = 1;

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
    /// Controls the display order of this spell's lane on the cooldown timeline.
    /// Lower values appear first. Null means the lane is ordered by cast frequency.
    /// </summary>
    public int? TimelineSortIndex { get; init; }

    /// <summary>
    /// A buff spell ID that, when active, marks a window during which this ability
    /// can be cast. Used by the timeline to shade "castable" windows.
    /// </summary>
    public int? TimelineCastableBuff { get; init; }

    /// <summary>
    /// When true, casting this ability does not interrupt or cancel another in-progress
    /// cast (e.g. Ice Blitz, which can be used mid-cast without cancelling it).
    /// Used by <see cref="CastLinkNormalizer"/> to avoid false-positive cancelled cast detection.
    /// </summary>
    public bool CastableWhileCasting { get; init; }

    /// <summary>
    /// Gets the effective cooldown in seconds, resolving haste-scaling functions
    /// when present.
    /// </summary>
    public double GetCooldown(double haste = 1.0) =>
        Cooldown?.Match(value => value, withHaste => withHaste(haste)) ?? 0;

    /// <summary>
    /// Gets the effective charge count, resolving combatant-dependent functions
    /// when present.
    /// </summary>
    public int GetCharges(Combatant? combatant = null) =>
        Charges.Match(value => value, func => combatant is not null ? func(combatant) : 1);
}

/// <summary>
/// GCD configuration for a spell. Each sub-value can be a fixed number of
/// milliseconds or a function of the player's <see cref="Combatant"/>.
/// </summary>
public sealed record GcdInfo
{
    /// <summary>
    /// The base GCD in milliseconds before haste reduction.
    /// Typically 1500ms for most abilities.
    /// </summary>
    public GcdValue? Base { get; init; }

    /// <summary>
    /// A fixed GCD in milliseconds that is not affected by haste.
    /// </summary>
    public GcdValue? Static { get; init; }

    /// <summary>
    /// The minimum GCD in milliseconds (floor after haste reduction).
    /// </summary>
    public GcdValue? Minimum { get; init; }
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
