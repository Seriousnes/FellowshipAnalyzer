namespace FellowshipAnalyzer.Core.Common.Spells.Elarion;

public interface IElarionSpell
{
    /// <summary>
    /// The amount of Focus spent when casting this spell, or <c>null</c> if the spell does not consume Focus.
    /// </summary>
    int? FocusCost { get; }
}
