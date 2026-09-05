using FellowshipAnalyzer.Core.Contracts.Design;

namespace FellowshipAnalyzer.Core.UI.Charts;

/// <summary>
/// The design tokens a chart's series use, in slot order. Which tokens those are stays with the
/// caller, as <see cref="ChartPalette"/> requires. Name each one as <c>nameof(FaPalette.Fire1)</c> so
/// a renamed token breaks the build.
/// </summary>
/// <param name="tokens">The <see cref="FaPalette"/> property names, in slot order.</param>
public sealed class SeriesTokens(params string[] tokens)
{
    /// <summary>How many slots are defined before the sequence repeats.</summary>
    public int Count => tokens.Length;

    /// <summary>The CSS colour for a slot, wrapping around when there are more series than tokens.</summary>
    public string Series(ChartPalette palette, int slot) => palette.Resolve(tokens[slot % tokens.Length]);
}
