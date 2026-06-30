using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.SpellData.Model;

/// <summary>
/// A curated spell: a real Core <see cref="Spell"/> wrapped with the curation metadata
/// (its <c>spelldb.json</c> scope and member key) and per-field <see cref="Provenance"/>.
/// </summary>
public record CuratedSpell(string Scope, string Member, Spell Spell, Provenance Provenance)
{
    /// <summary>The full FSL guid of the wrapped spell.</summary>
    public int Guid => Spell.Guid;

    /// <summary>The FSL id-range category of the wrapped spell.</summary>
    public SpellKind Kind => Spell switch
    {
        Effect => SpellKind.Effect,
        Talent => SpellKind.Talent,
        Weapon => SpellKind.Weapon,
        _ => SpellKind.Ability,
    };
}
