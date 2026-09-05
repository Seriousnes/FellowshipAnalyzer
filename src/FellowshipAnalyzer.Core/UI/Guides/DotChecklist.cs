using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;

namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// Turns <see cref="DotCoverage"/> into <see cref="Checklist"/> lines.
/// </summary>
public static class DotChecklist
{
    /// <summary>
    /// One line for <paramref name="coverage"/>, named by the ability that applies the effect where
    /// there is one and by the effect itself where several abilities apply it.
    /// </summary>
    /// <param name="coverage">The coverage the line reports.</param>
    /// <param name="note">A short trailing qualifier, for example how many enemies had it active.</param>
    public static CheckItem Item(DotCoverage coverage, string? note = null) => new()
    {
        Label = coverage.Dot.Cast ?? coverage.Dot.Effect,
        Pass = coverage.Active,
        Count = coverage.Magnitude,
        CountTooltip = CountTooltip(coverage),
        Note = note,
        Title = coverage.Active ? "Active at the cast" : "Missing at the cast",
    };

    /// <summary>One line per coverage, in the order given.</summary>
    public static IEnumerable<CheckItem> Items(IEnumerable<DotCoverage> coverage) =>
        coverage.Select(entry => Item(entry));

    private static string? CountTooltip(DotCoverage coverage) => coverage.Dot.StackMethod switch
    {
        StackMethod.ConcurrentInstances => $"{coverage.Instances} concurrent applications",
        StackMethod.Stacking => $"{coverage.Stacks} stacks",
        _ => null,
    };
}
