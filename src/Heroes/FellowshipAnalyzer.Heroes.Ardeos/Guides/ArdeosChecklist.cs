using FellowshipAnalyzer.Core.UI.Guides;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

namespace FellowshipAnalyzer.Heroes.Ardeos.Guides;

/// <summary>
/// Turns a damage-over-time coverage snapshot into the checklist a guide shows beside a cast, so every
/// Ardeos guide reads the same effect the same way: the ability's own name and icon, a count only where
/// the effect's magnitude means one, and the effects always in <see cref="ArdeosDots.All"/> order.
/// </summary>
internal static class ArdeosChecklist
{
    /// <param name="note">Trailing context for the row, such as how far the effect had spread.</param>
    public static AuraCheckItem Item(ArdeosDotCoverage entry, string? note = null) =>
        new(entry.Dot.Cast, entry.Active, entry.Magnitude, CountTooltip(entry), note);

    public static IEnumerable<AuraCheckItem> Items(IEnumerable<ArdeosDotCoverage> coverage) =>
        coverage.Select(entry => Item(entry));

    private static string? CountTooltip(ArdeosDotCoverage entry) => entry.Dot.Magnitude switch
    {
        DotMagnitude.Instances => $"{entry.Instances} concurrent applications",
        DotMagnitude.Stacks => $"{entry.Stacks} stacks",
        _ => null,
    };
}
