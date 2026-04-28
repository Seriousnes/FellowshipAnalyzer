namespace FellowshipAnalyzer.Core.Common.Spells.Rime;

/// <summary>
/// Cost metadata for Rime hero spells. Implemented by <see cref="Spell"/> so that any spell
/// can be tested via <c>spell is IRimeSpell r</c> and interrogated for Rime-specific costs.
/// Null on a cost property means this spell does not spend that resource.
/// </summary>
public interface IRimeSpell
{
    /// <summary>
    /// The number of Winter Orbs spent when casting this spell, or <c>null</c> if the spell
    /// does not consume Winter Orbs.
    /// </summary>
    int? WinterOrbCost { get; }

    /// <summary>
    /// The amount of Anima (Primary resource) spent when casting this spell, or <c>null</c>
    /// if the spell does not consume Anima.
    /// </summary>
    int? AnimaCost { get; }
}
