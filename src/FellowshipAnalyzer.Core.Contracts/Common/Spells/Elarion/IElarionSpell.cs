namespace FellowshipAnalyzer.Core.Common.Spells.Elarion;

/// <summary>Marks a spell as usable by Elarion, exposing its Focus cost alongside the shared <see cref="Spell"/> facts.</summary>
public interface IElarionSpell
{
    /// <summary>
    /// The amount of Focus spent when casting this spell, or <c>null</c> if the spell does not consume Focus.
    /// </summary>
    int? FocusCost { get; }
}
