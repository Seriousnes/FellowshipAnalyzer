namespace FellowshipAnalyzer.Core.Utility;

/// <summary>
/// Word-form helpers, intended for <c>using static</c> import so they read as standalone functions.
/// </summary>
public static class Inflection
{
    /// <summary>Selects the word form that agrees with <paramref name="value"/>: <paramref name="singular"/> for exactly 1, <paramref name="multiple"/> otherwise.</summary>
    /// <example><c>$"{charges} {Plural(charges, "charge", "charges")}"</c> yields <c>1 charge</c> or <c>3 charges</c>.</example>
    public static string Plural(long value, string singular, string multiple) =>
        value == 1 ? singular : multiple;
}
