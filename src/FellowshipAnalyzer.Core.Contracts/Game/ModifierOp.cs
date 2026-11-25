namespace FellowshipAnalyzer.Core.Game;

/// <summary>
/// How an effect's attribute modifier combines with the attribute's other terms. The member names are
/// the game-data export's <c>op</c> strings, including its spelling of <see cref="Multiplicitive"/>.
/// </summary>
public enum ModifierOp
{
    /// <summary>The value adds to the attribute's additive terms.</summary>
    Additive,
    /// <summary>The value multiplies the attribute's multiplicative terms.</summary>
    Multiplicitive,
    /// <summary>The value multiplies the attribute's running product.</summary>
    MultiplyCompound,
    /// <summary>The value applies after every other term.</summary>
    AddFinal,
}
