namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>Shared number formatting for the item throughput readouts.</summary>
public static class ThroughputFormat
{
    /// <summary>Formats a per-second rate, abbreviating thousands and millions.</summary>
    public static string Rate(double perSecond) => Total((long)Math.Round(perSecond));

    /// <summary>Formats an absolute amount, abbreviating thousands and millions.</summary>
    public static string Total(long amount) => amount switch
    {
        >= 1_000_000 => $"{amount / 1_000_000d:0.0}m",
        >= 10_000 => $"{amount / 1000d:0}k",
        >= 1_000 => $"{amount / 1000d:0.0}k",
        _ => amount.ToString(),
    };

    /// <summary>Formats a fraction as a whole-number percentage.</summary>
    public static string Share(double fraction) => $"{fraction:P0}";
}
