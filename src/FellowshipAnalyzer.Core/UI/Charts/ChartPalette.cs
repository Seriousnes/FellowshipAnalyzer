using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Contracts.Design;
using FellowshipAnalyzer.Core.UI.Theming;

namespace FellowshipAnalyzer.Core.UI.Charts;

/// <summary>
/// Resolves design tokens to the concrete CSS text a charting library needs. Stylesheets reference a
/// token as <c>var(--fa-*)</c>, but ApexCharts writes colours into SVG presentation attributes, where a
/// custom property does not resolve, so a chart is the one place that must be handed the value rather
/// than the reference. Reading it back out of <see cref="ThemeService"/> keeps the C# theme the only
/// place a colour is written, and keeps a chart honouring both the selected theme and any runtime token
/// override.
/// </summary>
/// <remarks>
/// A component that holds resolved colours must rebuild them on <see cref="ThemeService.Changed"/>;
/// caching them in a <c>readonly</c> field freezes the chart at whichever theme was selected when it
/// first rendered.
/// </remarks>
public sealed class ChartPalette(ThemeService theme)
{
    private FaTheme? _cachedTheme;
    private Dictionary<string, string> _cachedValues = [];

    /// <summary>The number of categorical series colours the design system declares.</summary>
    public static int SeriesCount => 6;

    /// <summary>
    /// The CSS text in force for the token a <see cref="FaPalette"/> property defines, naming it as
    /// <c>Resolve(nameof(FaPalette.Gold))</c> so a renamed property breaks the build.
    /// </summary>
    public string Resolve(string paletteProperty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paletteProperty);

        var name = FaToken.KebabCase(paletteProperty);
        return theme.Overrides.TryGetValue(name, out var overridden)
            ? overridden
            : Values.GetValueOrDefault(name, "");
    }

    /// <summary>
    /// The categorical series colour for a slot, counted from zero. Slots are assigned in fixed order
    /// and wrap past <see cref="SeriesCount"/> rather than inventing a hue.
    /// </summary>
    public string Series(int slot) => Resolve(SeriesProperty(slot % SeriesCount));

    /// <summary>The colour of a performance tier, matching what <see cref="PerformanceColors"/> hands to markup.</summary>
    public string Performance(QualitativePerformance tier) => Resolve(tier switch
    {
        QualitativePerformance.Perfect => nameof(FaPalette.PerfPerfect),
        QualitativePerformance.Good => nameof(FaPalette.PerfGood),
        QualitativePerformance.Ok => nameof(FaPalette.PerfOk),
        QualitativePerformance.Fail => nameof(FaPalette.PerfFail),
        _ => nameof(FaPalette.FgNeutral),
    });

    private Dictionary<string, string> Values
    {
        get
        {
            if (!ReferenceEquals(_cachedTheme, theme.Theme))
            {
                _cachedTheme = theme.Theme;
                _cachedValues = theme.Theme.Tokens.ToDictionary(token => token.Name, token => token.Value, StringComparer.Ordinal);
            }
            return _cachedValues;
        }
    }

    private static string SeriesProperty(int slot) => slot switch
    {
        0 => nameof(FaPalette.Chart1),
        1 => nameof(FaPalette.Chart2),
        2 => nameof(FaPalette.Chart3),
        3 => nameof(FaPalette.Chart4),
        4 => nameof(FaPalette.Chart5),
        _ => nameof(FaPalette.Chart6),
    };
}
