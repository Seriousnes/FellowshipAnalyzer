namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// How complete a hero's analysis is.
/// </summary>
public enum SupportLevel
{
    /// <summary>Analysis is absent or incomplete.</summary>
    Unmaintained,

    /// <summary>Analysis is still being built out.</summary>
    Partial,

    /// <summary>Analysis is largely complete.</summary>
    Full,
}
