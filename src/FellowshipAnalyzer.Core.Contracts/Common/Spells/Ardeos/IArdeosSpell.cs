namespace FellowshipAnalyzer.Core.Common.Spells.Ardeos;

/// <summary>
/// Cost metadata for Ardeos hero spells. Implemented by <see cref="Spell"/> so that any spell
/// can be tested via <c>spell is IArdeosSpell a</c> and interrogated for Ardeos-specific costs.
/// Null on a cost property means this spell does not spend that resource. 
/// </summary>
public interface IArdeosSpell
{
    /// <summary>
    /// The amount of Ember (Primary resource) spent when casting this spell, or <c>null</c>
    /// if the spell does not consume Embers.
    /// </summary>
    int? EmberCost { get; }
}