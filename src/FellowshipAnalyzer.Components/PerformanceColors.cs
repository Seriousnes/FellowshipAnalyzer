namespace FellowshipAnalyzer.Components;

public static class PerformanceColors
{
    public const string Perfect = "#2090c0";
    public const string Good = "#4ec04e";
    public const string Ok = "#ffc84a";
    public const string Fail = "#ac1f39";

    public static string ToColor(PerformanceTier tier) => tier switch
    {
        PerformanceTier.Perfect => Perfect,
        PerformanceTier.Good => Good,
        PerformanceTier.Ok => Ok,
        PerformanceTier.Fail => Fail,
        _ => "#ffffff",
    };

    public static string ToLabel(PerformanceTier tier) => tier switch
    {
        PerformanceTier.Perfect => "Perfect",
        PerformanceTier.Good => "Good",
        PerformanceTier.Ok => "Ok",
        PerformanceTier.Fail => "Bad",
        _ => "Unknown",
    };
}
