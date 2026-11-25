namespace FellowshipAnalyzer.Core.Game;

/// <summary>
/// One modifier an effect applies to a <see cref="StatAttribute"/>, as the game-data export states it.
/// </summary>
/// <param name="Attribute">Attribute the modifier changes.</param>
/// <param name="Op">How the modifier combines with the attribute's other terms.</param>
/// <param name="Value">Modifier value; <see langword="null"/> when the export states none.</param>
/// <param name="Ranks">Modifier value at each rank; <see langword="null"/> when the export states none.</param>
/// <param name="Coefficient">Coefficient the export states; <see langword="null"/> when it states none.</param>
public sealed record AttributeModifier(StatAttribute Attribute, ModifierOp Op, double? Value, double[]? Ranks, double? Coefficient);
