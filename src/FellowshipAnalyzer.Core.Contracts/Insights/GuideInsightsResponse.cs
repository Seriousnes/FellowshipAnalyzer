namespace FellowshipAnalyzer.Core.Insights;

/// <summary>A model's insights on one guide across one player's run.</summary>
/// <param name="Hero">Hero the guide belongs to.</param>
/// <param name="Guide">Guide name, as the hero's prompts name it.</param>
/// <param name="Insights">Patterns across the run, the most valuable first.</param>
public sealed record GuideInsightsResponse(string Hero, string Guide, IReadOnlyList<GuideInsight> Insights);

/// <summary>One pattern across a player's run.</summary>
/// <param name="Title">Short heading.</param>
/// <param name="Insight">The pattern in plain words, then what to do and why.</param>
public sealed record GuideInsight(string Title, string Insight);

/// <summary>Whether guide insights are switched on.</summary>
/// <param name="Available">True when the insights endpoint answers.</param>
public sealed record InsightsAvailability(bool Available);
