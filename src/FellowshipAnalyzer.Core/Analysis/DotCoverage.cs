using FellowshipAnalyzer.Core.Common;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// What one <see cref="Dot"/> had on the enemies read at a single instant: how many carried it, how
/// many instances were active across them, and how many stacks those instances summed to.
/// </summary>
public sealed record DotCoverage
{
    /// <summary>The effect this reading is for.</summary>
    public required Dot Dot { get; init; }

    /// <summary>How many enemies carried the effect. A reading taken on one enemy is 0 or 1.</summary>
    public required int Targets { get; init; }

    /// <summary>Concurrently-open instances of the effect summed across <see cref="Targets"/>.</summary>
    public required int Instances { get; init; }

    /// <summary>Stacks summed across every open instance.</summary>
    public required int Stacks { get; init; }

    /// <summary>Whether the effect was active anywhere in this reading.</summary>
    public bool Active => Instances > 0;

    /// <summary>
    /// The count worth showing beside the effect's name, or <c>null</c> when presence is the whole
    /// story. Only <see cref="StackMethod.ConcurrentInstances"/> and <see cref="StackMethod.Stacking"/>
    /// carry a number a reader can act on.
    /// </summary>
    public int? Magnitude => Dot.StackMethod switch
    {
        StackMethod.ConcurrentInstances => Instances,
        StackMethod.Stacking => Stacks,
        _ => null,
    };
}
