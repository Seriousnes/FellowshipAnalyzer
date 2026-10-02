namespace FellowshipAnalyzer.Core.Utility;

/// <summary>
/// Display formatting for amounts, percentages, and counts.
/// </summary>
public static class NumberFormat
{
    /// <summary>Formats an amount, abbreviating thousands and millions (<c>950</c>, <c>4.2k</c>, <c>38k</c>, <c>1.5M</c>).</summary>
    public static string Compact(double amount) => Math.Round(amount) switch
    {
        >= 1_000_000 and var rounded => $"{rounded / 1_000_000:0.0}M",
        >= 10_000 and var rounded => $"{rounded / 1_000:0}k",
        >= 1_000 and var rounded => $"{rounded / 1_000:0.0}k",
        var rounded => $"{rounded:0}",
    };

    /// <summary>
    /// Formats a fraction as a percentage, dropping trailing zero decimals.
    /// For example, 0.425 with <paramref name="decimals"/> = 1 yields <c>42.5%</c>.
    /// </summary>
    /// <param name="fraction">The ratio to format (for 50% pass 0.5).</param>
    /// <param name="decimals">The maximum number of decimals to show.</param>
    public static string Percent(double fraction, int decimals = 0) =>
        $"{(fraction * 100).ToString(decimals > 0 ? "0." + new string('#', decimals) : "0")}%";

    /// <summary>Formats <paramref name="part"/> as a <see cref="Percent"/> of <paramref name="whole"/>, or <c>0%</c> when <paramref name="whole"/> is not positive.</summary>
    public static string Share(double part, double whole, int decimals = 0) =>
        whole > 0 ? Percent(part / whole, decimals) : "0%";

    /// <summary>Formats a position as an ordinal word up to five, and as a numeric ordinal after that (<c>first</c>, <c>fifth</c>, <c>6th</c>, <c>22nd</c>).</summary>
    public static string Ordinal(int value) => value switch
    {
        1 => "first",
        2 => "second",
        3 => "third",
        4 => "fourth",
        5 => "fifth",
        _ when value % 100 is 11 or 12 or 13 => $"{value}th",
        _ => (value % 10) switch
        {
            1 => $"{value}st",
            2 => $"{value}nd",
            3 => $"{value}rd",
            _ => $"{value}th",
        },
    };
}
