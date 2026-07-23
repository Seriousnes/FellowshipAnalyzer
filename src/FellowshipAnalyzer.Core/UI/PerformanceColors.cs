using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.UI;

public static class PerformanceColors
{
    // Mirror the SCSS $fa-perf-* / --fa-perf-* tokens (the authoritative palette).
    public const string Perfect = "#2090c0";
    public const string Good = "#4ec04e";
    public const string Ok = "#ffc84a";
    public const string Fail = "#ac1f39";

    public const string VeryBad = "#661111";
    public const string Mediocre = "#dd5533";
    public const string Available = "#696864";

    public static string ToColor(QualitativePerformance tier) => tier switch
    {
        QualitativePerformance.Perfect => Perfect,
        QualitativePerformance.Good => Good,
        QualitativePerformance.Ok => Ok,
        QualitativePerformance.Fail => Fail,
        _ => "#ffffff",
    };

    public static string ToLabel(QualitativePerformance tier) => tier switch
    {
        QualitativePerformance.Perfect => "Perfect",
        QualitativePerformance.Good => "Good",
        QualitativePerformance.Ok => "Ok",
        QualitativePerformance.Fail => "Bad",
        _ => "Unknown",
    };
}
