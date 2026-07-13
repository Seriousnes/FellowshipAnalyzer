namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// How complete a hero's analysis is.
/// </summary>
public enum SupportLevel
{
    /// <summary>/// No analysis is available for this hero./// </summary>
    None,
    /// <summary>/// Analysis is still being built out, but is not yet usable./// </summary>
    WIP,
    /// <summary>Analysis is absent or incomplete.</summary>
    Unmaintained,
    /// <summary>Analysis is still being built out.</summary>
    Partial,
    /// <summary>Analysis is largely complete.</summary>
    Full,
}
