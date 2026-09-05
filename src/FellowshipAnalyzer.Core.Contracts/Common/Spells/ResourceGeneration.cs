using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>How the magnitude on a <see cref="ResourceGeneration"/> is read.</summary>
public enum GenerationMeasure
{
    /// <summary>Whole units of the resource.</summary>
    Flat,
    /// <summary>A share of the resource's maximum pool, as a fraction of one.</summary>
    FractionOfMaximum,
    /// <summary>A fractional increase applied to the generation the rest of the kit states.</summary>
    Increase,
}

/// <summary>The occasion a <see cref="ResourceGeneration"/> pays out on.</summary>
public enum GenerationTrigger
{
    /// <summary>Once on the cast.</summary>
    PerCast,
    /// <summary>Once per damage event the spell produces.</summary>
    PerHit,
    /// <summary>Once, spread over the spell's full duration.</summary>
    OverDuration,
    /// <summary>Once per stack held.</summary>
    PerStack,
}

/// <summary>
/// The resource generation a spell's game-data description states: which pool it feeds, the stated
/// magnitude, the magnitude a critical strike states where the text names one, how the magnitude is
/// read, and the occasion it pays out on. The conditions a description attaches to a statement
/// ("below 50% Chrona", "on the empowered Time Shard") are the analyzer's, not this record's.
/// </summary>
public sealed record ResourceGeneration
{
    /// <summary>The resource pool this generation feeds.</summary>
    public ResourceTypes Resource { get; init; }

    /// <summary>The stated magnitude, read according to <see cref="Measure"/>.</summary>
    public double Amount { get; init; }

    /// <summary>The magnitude a critical strike states; <c>null</c> when the description states none.</summary>
    public double? CriticalAmount { get; init; }

    /// <summary>How <see cref="Amount"/> and <see cref="CriticalAmount"/> are read.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public GenerationMeasure Measure { get; init; }

    /// <summary>The occasion the amount pays out on; <c>null</c> for a statement that names none.</summary>
    public GenerationTrigger? Trigger { get; init; }
}
