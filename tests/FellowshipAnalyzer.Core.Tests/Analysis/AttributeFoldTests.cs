using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Game;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed class AttributeFoldTests
{
    [Fact]
    public void EmptyFold_AppliesAsTheIdentity() =>
        Assert.Equal(1.2, new AttributeFold().Apply(1.2), precision: 9);

    [Fact]
    public void Additive_AddsToTheBase()
    {
        var fold = new AttributeFold();
        fold.Add(ModifierOp.Additive, 0.3);

        Assert.Equal(1.5, fold.Apply(1.2), precision: 9);
    }

    [Fact]
    public void Multiplicitive_SharesOneFactor()
    {
        var fold = new AttributeFold();
        fold.Add(ModifierOp.Multiplicitive, 1.15);
        fold.Add(ModifierOp.Multiplicitive, 1.2);

        Assert.Equal(1.35, fold.Apply(1.0), precision: 9);
    }

    [Fact]
    public void MultiplyCompound_EachMultipliesTheResult()
    {
        var fold = new AttributeFold();
        fold.Add(ModifierOp.MultiplyCompound, 1.05);
        fold.Add(ModifierOp.MultiplyCompound, 1.05);

        Assert.Equal(1.1025, fold.Apply(1.0), precision: 9);
    }

    [Fact]
    public void EveryOperation_FollowsTheFoldOrder()
    {
        var fold = new AttributeFold();
        fold.Add(ModifierOp.Additive, 0.3);
        fold.Add(ModifierOp.Multiplicitive, 1.2);
        fold.Add(ModifierOp.MultiplyCompound, 1.05);
        fold.Add(ModifierOp.AddFinal, 0.1);

        Assert.Equal((1.2 + 0.3) * 1.2 * 1.05 + 0.1, fold.Apply(1.2), precision: 9);
        Assert.Equal(1.99, fold.Apply(1.2), precision: 9);
    }

    [Fact]
    public void AdditiveStacks_MultiplyTheValue()
    {
        var fold = new AttributeFold();
        fold.Add(ModifierOp.Additive, 0.006, stacks: 4);

        Assert.Equal(1.024, fold.Apply(1.0), precision: 9);
    }

    [Fact]
    public void MultiplicitiveStacks_MultiplyThePartAboveOne()
    {
        var fold = new AttributeFold();
        fold.Add(ModifierOp.Multiplicitive, 1.1, stacks: 3);

        Assert.Equal(1.3, fold.Apply(1.0), precision: 9);
    }

    [Fact]
    public void MultiplyCompoundStacks_RaiseTheValueToTheStackCount()
    {
        var fold = new AttributeFold();
        fold.Add(ModifierOp.MultiplyCompound, 1.1, stacks: 2);

        Assert.Equal(1.21, fold.Apply(1.0), precision: 9);
    }

    [Theory]
    [InlineData(ModifierOp.Additive, 0.3)]
    [InlineData(ModifierOp.Multiplicitive, 1.2)]
    [InlineData(ModifierOp.MultiplyCompound, 1.05)]
    [InlineData(ModifierOp.AddFinal, 0.1)]
    public void Remove_UndoesAdd(ModifierOp op, double value)
    {
        var fold = new AttributeFold();
        fold.Add(op, value, stacks: 2);
        fold.Remove(op, value, stacks: 2);

        Assert.Equal(1.2, fold.Apply(1.2), precision: 9);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Remove_MultiplyCompound_WithANonPositiveValue_Throws(double value)
    {
        var fold = new AttributeFold();

        Assert.Throws<ArgumentOutOfRangeException>(() => fold.Remove(ModifierOp.MultiplyCompound, value));
    }
}
