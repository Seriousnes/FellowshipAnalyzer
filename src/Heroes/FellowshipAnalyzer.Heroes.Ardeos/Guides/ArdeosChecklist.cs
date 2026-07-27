using FellowshipAnalyzer.Core.UI.Guides;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

namespace FellowshipAnalyzer.Heroes.Ardeos.Guides;

internal static class ArdeosChecklist
{
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
