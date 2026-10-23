using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.UI;

using OneOf;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// The set of abilities a <see cref="CooldownModifier"/> applies to: an explicit list of spells, one or
/// more Season 3 <see cref="AbilityCategory"/> values, or an arbitrary predicate over a
/// <see cref="SpellbookAbility"/>. A modifier with no scope applies to every ability.
/// </summary>
[GenerateOneOf]
public partial class CooldownScope : OneOfBase<Spell[], AbilityCategory[], Func<SpellbookAbility, bool>>
{
    /// <summary>
    /// Whether this scope covers <paramref name="ability"/>. A spell list matches when any listed spell's
    /// <see cref="Spell.FSLID"/> equals the ability's <see cref="SpellbookAbility.PrimarySpell"/> or any of
    /// its <see cref="SpellbookAbility.AdditionalSpells"/>. A category list matches when the ability's
    /// <see cref="SpellbookAbility.AbilityCategory"/> is set and contained. A predicate matches when it
    /// returns true.
    /// </summary>
    public bool Matches(SpellbookAbility ability) => Match(
        spells => spells.Any(spell =>
            spell.FSLID.Equals(ability.PrimarySpell.FSLID)
            || (ability.AdditionalSpells?.Any(additional => additional.FSLID.Equals(spell.FSLID)) ?? false)),
        categories => ability.AbilityCategory is { } category && categories.Contains(category),
        predicate => predicate(ability));
}

/// <summary>
/// The two cooldown stat pools <see cref="StatTracker"/> tracks: Ability Cooldown Reduction
/// (<c>effective = base * (1 - acr)</c>, fixed at cast time) and Cooldown Acceleration (terms on the
/// shared recovery pool <see cref="SpellUsable.EffectiveRate"/> divides by, dynamic semantics).
/// </summary>
public enum CooldownPool
{
    /// <summary>A fraction fixed at cast time, applied as <c>effective = base * (1 - acr)</c>.</summary>
    AbilityCooldownReduction,

    /// <summary>A dynamic term folded into the shared recovery rate that <see cref="SpellUsable.EffectiveRate"/> divides by.</summary>
    CooldownAcceleration,
}

/// <summary>
/// A single cooldown modifier: a fractional <paramref name="Value"/> applied within an optional
/// <paramref name="Scope"/>.
/// </summary>
/// <param name="Value">The modifier magnitude as a fraction, where 0.10 means 10%.</param>
/// <param name="Scope">
/// The abilities the modifier applies to, or <c>null</c> for an unscoped modifier that applies to every
/// ability.
/// </param>
public sealed record CooldownModifier(double Value, CooldownScope? Scope = null)
{
    /// <summary>
    /// The ability whose own effect grants the modifier, or <c>null</c> for a modifier no ability grants.
    /// A Weapon ability's Cooldown Acceleration takes only the modifiers its own effects grant
    /// (Chronoshift's 8× while channelling Chronoshift).
    /// </summary>
    public Spell? GrantedBy { get; init; }

    /// <summary>Whether the modifier's <see cref="Scope"/> covers <paramref name="ability"/>. An unscoped modifier covers every ability.</summary>
    public bool Covers(SpellbookAbility? ability) =>
        Scope is null || (ability is not null && Scope.Matches(ability));

    /// <summary>
    /// Whether the modifier is a term of <paramref name="ability"/>'s Cooldown Acceleration. A Weapon ability takes
    /// only a modifier its own effect grants; every other ability takes the modifiers that
    /// <see cref="Covers"/> it.
    /// </summary>
    public bool Accelerates(SpellbookAbility? ability) =>
        ability?.AbilityCategory == AbilityCategory.Weapon
            ? GrantedBy is { } grantedBy && IsOwnSpell(ability, grantedBy)
            : Covers(ability);

    private static bool IsOwnSpell(SpellbookAbility ability, Spell spell) =>
        spell.FSLID.Equals(ability.PrimarySpell.FSLID)
        || (ability.AdditionalSpells?.Any(additional => additional.FSLID.Equals(spell.FSLID)) ?? false);
}

/// <summary>
/// An immutable collection of <see cref="CooldownModifier"/> entries whose <see cref="Total"/> resolves the
/// combined fraction that applies to a given ability.
/// </summary>
public sealed class CooldownModifierSet : List<CooldownModifier>
{
    /// <summary>An empty set that contributes nothing to any ability.</summary>
    public static CooldownModifierSet Empty => [];

    /// <summary>
    /// The summed fraction that applies to <paramref name="ability"/>: every unscoped entry plus every entry
    /// whose scope <see cref="CooldownScope.Matches"/> the ability.
    /// </summary>
    /// <remarks>
    /// A <c>null</c> ability accrues only the unscoped entries.
    /// </remarks>
    public double Total(SpellbookAbility? ability) => this.Where(entry => entry.Covers(ability)).Sum(entry => entry.Value);

    /// <summary>
    /// The summed Cooldown Acceleration for <paramref name="ability"/>: every entry that
    /// <see cref="CooldownModifier.Accelerates"/> it.
    /// </summary>
    public double AccelerationTotal(SpellbookAbility? ability) => this.Where(entry => entry.Accelerates(ability)).Sum(entry => entry.Value);
}
