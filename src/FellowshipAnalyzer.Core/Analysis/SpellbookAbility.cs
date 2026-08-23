using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.UI;

using OneOf;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A GCD sub-value — either a fixed number of milliseconds, or a function of
/// the player's <see cref="FullCombatant"/> that returns milliseconds.
/// </summary>
[GenerateOneOf]
public partial class GcdValue : OneOfBase<double, Func<FullCombatant, double>>;

/// <summary>
/// Defines a spell's gameplay metadata for the spellbook.
/// This is the input definition — hero analyzers override <see cref="Abilities.Spellbook"/>
/// to provide these entries.
/// </summary>
public sealed record SpellbookAbility
{
    /// <summary>
    /// The primary spell. If an ability has multiple associated spells
    /// (e.g. main-hand and off-hand), list the primary here and extras in
    /// <see cref="AdditionalSpells"/>.
    /// </summary>
    public required Spell PrimarySpell { get; init; }

    /// <summary>
    /// Additional spells tied to this ability (e.g. off-hand hits, buff IDs).
    /// These share the same cooldown and are treated as the same ability.
    /// </summary>
    public Spell[]? AdditionalSpells { get; init; }

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
    /// The game's Season 3 ability-category classification for this ability, read from
    /// <see cref="PrimarySpell"/> (sourced from the game-data export). Distinct from <see cref="Category"/>,
    /// which is the tool's internal analysis grouping. <c>null</c> means unclassified, which matches no
    /// <see cref="CooldownScope"/> category scope.
    /// </summary>
    public AbilityCategory? AbilityCategory => PrimarySpell.AbilityCategory;

    /// <summary>
    /// When true, the cooldown is reduced by haste using <c>Cooldown / (1 + haste)</c>.
    /// </summary>
    public bool CooldownReducedByHaste { get; init; }

    /// <summary>
    /// The number of charges the ability has, read from <see cref="PrimarySpell"/>.
    /// </summary>
    public int Charges => PrimarySpell.Charges;

    /// <summary>
    /// The cast time in seconds for a casted ability, read from <see cref="PrimarySpell"/>.
    /// </summary>
    public double? CastDuration => PrimarySpell.CastDuration;

    /// <summary>
    /// The total channel time in seconds for a channeled ability, read from <see cref="PrimarySpell"/>.
    /// </summary>
    public double? ChannelDuration => PrimarySpell.ChannelDuration;

    /// <summary>
    /// The interval in seconds between channel ticks, read from <see cref="PrimarySpell"/>.
    /// </summary>
    public double? ChannelTickInterval => PrimarySpell.ChannelTickInterval;

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
    /// The spell's range in yards/meters, read from <see cref="PrimarySpell"/>.
    /// </summary>
    public int? Range => PrimarySpell.Range;

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
    /// Gets the effective cooldown in seconds from <see cref="PrimarySpell"/>, applying haste
    /// reduction when <see cref="CooldownReducedByHaste"/> is set.
    /// </summary>
    /// <param name="haste">Player haste as a fraction (0 = none, 1.0 = 100%). At 0 there is no reduction.</param>
    public double GetCooldown(double haste = 0.0) =>
        PrimarySpell.Cooldown is not { } cd ? 0 : CooldownReducedByHaste ? cd / (1 + haste) : cd;
}

/// <summary>
/// GCD configuration for a spell. Each sub-value can be a fixed number of
/// milliseconds or a function of the player's <see cref="FullCombatant"/>.
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

    /// <summary>
    /// When the ability has to be cast, which decides how much of a hold on it a cooldown graph
    /// marks. <c>null</c> takes the default from <see cref="SpellbookAbility.Charges"/>: an ability
    /// with charges stops recharging once the last one is back, so it defaults to
    /// <see cref="CooldownUsage.OnCooldown"/>, and a single-charge ability defaults to
    /// <see cref="CooldownUsage.BeforeAUseIsLost"/>.
    /// </summary>
    public CooldownUsage? Usage { get; init; }
}

/// <summary>When an ability has to be cast, which decides how much of a hold on it is marked.</summary>
public enum CooldownUsage
{
    /// <summary>Cast it the moment it comes off cooldown, so every stretch in hand is marked.</summary>
    OnCooldown,

    /// <summary>
    /// Hold it as the pull demands, up to the point the hold runs a full recharge and another use
    /// fits inside it, which is where the mark starts.
    /// </summary>
    BeforeAUseIsLost,

    /// <summary>Cast it when it is needed, so no stretch in hand is marked.</summary>
    AsNeeded,
}
