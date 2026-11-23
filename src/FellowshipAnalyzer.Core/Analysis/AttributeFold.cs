using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// The modifiers of one game attribute, combined as the game combines them. <see cref="ModifierOp.Additive"/>
/// values add to the base, <see cref="ModifierOp.Multiplicitive"/> values add their part above 1 to one shared
/// factor, <see cref="ModifierOp.MultiplyCompound"/> values each multiply the result, and
/// <see cref="ModifierOp.AddFinal"/> values add after every multiplication. A default-constructed fold has no
/// modifiers and applies as the identity.
/// </summary>
public struct AttributeFold
{
    private double _additive;
    private double _increase;
    private double _compoundExcess;
    private double _addFinal;

    /// <summary>Adds a modifier of <paramref name="op"/> with <paramref name="value"/>, counted <paramref name="stacks"/> times.</summary>
    public void Add(ModifierOp op, double value, int stacks = 1)
    {
        switch (op)
        {
            case ModifierOp.Additive:
                _additive += value * stacks;
                break;
            case ModifierOp.Multiplicitive:
                _increase += (value - 1.0) * stacks;
                break;
            case ModifierOp.MultiplyCompound:
                _compoundExcess = (1.0 + _compoundExcess) * Math.Pow(value, stacks) - 1.0;
                break;
            case ModifierOp.AddFinal:
                _addFinal += value * stacks;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(op), op, null);
        }
    }

    /// <summary>Takes back a modifier previously added with the same arguments.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="op"/> is <see cref="ModifierOp.MultiplyCompound"/> and <paramref name="value"/> is not positive.</exception>
    public void Remove(ModifierOp op, double value, int stacks = 1)
    {
        switch (op)
        {
            case ModifierOp.Additive:
                _additive -= value * stacks;
                break;
            case ModifierOp.Multiplicitive:
                _increase -= (value - 1.0) * stacks;
                break;
            case ModifierOp.MultiplyCompound:
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
                _compoundExcess = (1.0 + _compoundExcess) / Math.Pow(value, stacks) - 1.0;
                break;
            case ModifierOp.AddFinal:
                _addFinal -= value * stacks;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(op), op, null);
        }
    }

    /// <summary>The attribute's value for <paramref name="baseValue"/>: <c>(base + additive) × (1 + increase) × compound + addFinal</c>.</summary>
    public readonly double Apply(double baseValue) =>
        (baseValue + _additive) * (1.0 + _increase) * (1.0 + _compoundExcess) + _addFinal;
}
