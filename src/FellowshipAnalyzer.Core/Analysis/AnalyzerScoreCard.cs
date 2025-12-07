namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// A summary score card produced by an analyzer module.
/// </summary>
public sealed record AnalyzerScoreCard(
    string Title,
    int Score,
    string Summary,
    string Accent);
