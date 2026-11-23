using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;
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
/// The three cooldown pools <see cref="StatTracker"/> tracks: Ability Cooldown Reduction
/// (<c>effective = base * (1 - acr)</c>, fixed at cast time), the Cooldown Recovery attribute and Cooldown
/// Acceleration terms (both parts of the recovery rate <see cref="SpellUsable.EffectiveRate"/> divides by,
/// dynamic semantics).
/// </summary>
public enum CooldownPool
{
    /// <summary>A fraction fixed at cast time, applied as <c>effective = base * (1 - acr)</c>.</summary>
    AbilityCooldownReduction,

    /// <summary>
    /// A modifier of the Cooldown Recovery attribute, which starts at 1 and folds each modifier under its
    /// <see cref="CooldownModifier.Op"/>. The modifier is never scoped.
    /// </summary>
    CooldownRecovery,

    /// <summary>A term added to the recovery rate of each ability the modifier's scope covers.</summary>
    CooldownAcceleration,
}

/// <summary>
/// A single cooldown modifier: a <paramref name="Value"/> applied within an optional
/// <paramref name="Scope"/>.
/// </summary>
/// <param name="Value">
/// The modifier magnitude. For <see cref="CooldownPool.AbilityCooldownReduction"/> and
/// <see cref="CooldownPool.CooldownAcceleration"/> a fraction, where 0.10 means 10%. For
/// <see cref="CooldownPool.CooldownRecovery"/> the export's modifier value, where 8 multiplies the attribute's
/// multiplicative factor by 8.
/// </param>
/// <param name="Scope">
/// The abilities the modifier applies to, or <c>null</c> for an unscoped modifier that applies to every
/// ability.
/// </param>
public sealed record CooldownModifier(double Value, CooldownScope? Scope = null)
{
    /// <summary>
    /// How a <see cref="CooldownPool.CooldownRecovery"/> modifier combines with the attribute's other terms.
    /// The other pools do not read it.
    /// </summary>
    public ModifierOp Op { get; init; } = ModifierOp.Multiplicitive;

    /// <summary>Whether the modifier's <see cref="Scope"/> covers <paramref name="ability"/>. An unscoped modifier covers every ability.</summary>
    public bool Covers(SpellbookAbility? ability) =>
        Scope is null || (ability is not null && Scope.Matches(ability));
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
    /// The summed Cooldown Acceleration for <paramref name="ability"/>: every entry whose
    /// <see cref="CooldownModifier.Covers"/> the ability.
    /// </summary>
    public double AccelerationTotal(SpellbookAbility? ability) => Total(ability);
}
