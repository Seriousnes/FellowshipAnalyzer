namespace FellowshipAnalyzer.Core.Insights;

/// <summary>What a guide's insights lead with, as the user message's <c>focus</c> names it.</summary>
public static class InsightFocus
{
    /// <summary>What went well, then a change if one is worth making.</summary>
    public const string Strengths = "strengths";

    /// <summary>The change first, then what went well.</summary>
    public const string Mixed = "mixed";

    /// <summary>Changes only.</summary>
    public const string Fixes = "fixes";

    /// <summary>Every focus value.</summary>
    public static IReadOnlyList<string> All { get; } = [Strengths, Mixed, Fixes];
}
