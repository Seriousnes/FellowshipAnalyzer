using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Contracts.Design;

namespace FellowshipAnalyzer.Core.UI;

/// <summary>
/// The four <see cref="QualitativePerformance"/> tiers and the neutral for a stat with no rating,
/// each as the CSS reference to the token that colours it. Nothing here holds a value,
/// so C# and the stylesheet cannot disagree.
/// </summary>
public static class PerformanceColors
{
    /// <summary>Perfect tier.</summary>
    public static readonly string Perfect = FaVar.PerfPerfect;

    /// <summary>Good tier.</summary>
    public static readonly string Good = FaVar.PerfGood;

    /// <summary>Ok tier.</summary>
    public static readonly string Ok = FaVar.PerfOk;

    /// <summary>Fail tier.</summary>
    public static readonly string Fail = FaVar.PerfFail;

    /// <summary>Value colour for a stat with no performance tier.</summary>
    public static readonly string Neutral = FaVar.FgNeutral;

    /// <summary>Maps a <see cref="QualitativePerformance"/> tier to its CSS colour token, giving <see cref="Neutral"/> to a value with no tier.</summary>
    public static string ToColor(QualitativePerformance? tier) => tier switch
    {
        QualitativePerformance.Perfect => Perfect,
        QualitativePerformance.Good => Good,
        QualitativePerformance.Ok => Ok,
        QualitativePerformance.Fail => Fail,
        _ => Neutral,
    };

    /// <summary>Maps a <see cref="QualitativePerformance"/> tier to its display label (<c>Fail</c> shows as "Bad"), falling back to "Unknown" for unmapped values.</summary>
    public static string ToLabel(QualitativePerformance tier) => tier switch
    {
        QualitativePerformance.Perfect => "Perfect",
        QualitativePerformance.Good => "Good",
        QualitativePerformance.Ok => "Ok",
        QualitativePerformance.Fail => "Bad",
        _ => "Unknown",
    };
}
