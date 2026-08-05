namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// How complete a hero's analysis is.
/// </summary>
public enum SupportLevel
{
    /// <summary>No analysis is available for this hero.</summary>
    None,
    /// <summary>Analysis covers a few of the hero's abilities.</summary>
    Minimal,
    /// <summary>Analysis is still being built out.</summary>
    Partial,
    /// <summary>Analysis is largely complete.</summary>
    Full,
}
