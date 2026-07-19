using FellowshipAnalyzer.Core.Common.Spells;
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
/// A single cooldown modifier: a fractional <paramref name="Value"/> applied within an optional
/// <paramref name="Scope"/>.
/// </summary>
/// <param name="Value">The modifier magnitude as a fraction, where 0.10 means 10%.</param>
/// <param name="Scope">
/// The abilities the modifier applies to, or <c>null</c> for an unscoped modifier that applies to every
/// ability.
/// </param>
public sealed record CooldownModifier(double Value, CooldownScope? Scope = null);

/// <summary>
/// An immutable collection of <see cref="CooldownModifier"/> entries whose <see cref="Total"/> resolves the
/// combined fraction that applies to a given ability.
/// </summary>
public sealed class CooldownModifierSet(params IReadOnlyList<CooldownModifier> entries)
{
    /// <summary>An empty set that contributes nothing to any ability.</summary>
    public static CooldownModifierSet Empty { get; } = new();

    /// <summary>The modifiers in this set.</summary>
    public IReadOnlyList<CooldownModifier> Entries => entries;

    /// <summary>
    /// The summed fraction that applies to <paramref name="ability"/>: every unscoped entry plus every entry
    /// whose scope <see cref="CooldownScope.Matches"/> the ability. A <c>null</c> ability accrues only the
    /// unscoped entries.
    /// </summary>
    public double Total(SpellbookAbility? ability)
    {
        var total = 0.0;
        foreach (var entry in entries)
        {
            if (entry.Scope is null || (ability is not null && entry.Scope.Matches(ability)))
                total += entry.Value;
        }
        return total;
    }
}
