namespace FellowshipAnalyzer.Core.UI;

/// <summary>
/// Sections rendered on the Statistics tab, in declaration order.
/// Add a new section by inserting a value at the desired position — the renderer
/// in <c>Report.razor</c> picks it up automatically.
/// </summary>
public enum StatisticCategory
{
    /// <summary>Default section for statistics not tied to a more specific category.</summary>
    General,

    /// <summary>Statistics about generation and spending of a tracked resource (orbs, mana, charges, energy).</summary>
    Resources,

    /// <summary>Statistics about cooldown usage and availability.</summary>
    Cooldowns,

    /// <summary>Statistics about a specific talent choice.</summary>
    Talents,

    /// <summary>Statistics about gear, trinkets, or other equipped items.</summary>
    Items,

    /// <summary>Statistics that do not fit any of the other sections.</summary>
    Other,
}
