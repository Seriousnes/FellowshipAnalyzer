namespace FellowshipAnalyzer.Core.Insights;

/// <summary>Talent names and the legendary item name.</summary>
public sealed record DigestBuild(List<string> Talents, string? Legendary);

/// <summary>
/// One guide's insights request, and the user message the server writes from it. It holds no number, grade or
/// pull name, so none can reach an insight.
/// </summary>
/// <param name="Guide">Guide name, as the hero's prompts name it.</param>
/// <param name="Focus">One of <see cref="InsightFocus"/>'s values, from the guide's grades.</param>
/// <param name="Build">Talents and legendary of the player.</param>
/// <param name="Patterns">How often each of the guide's patterns showed, by kind of pull.</param>
/// <param name="Pulls">The guide's facts on each pull with an item, in order.</param>
public sealed record InsightMessage<TPull>(string Guide, string Focus, DigestBuild Build, List<DigestPattern> Patterns, List<TPull> Pulls);

/// <summary>How often one of a guide's patterns showed.</summary>
/// <param name="Pattern">The pattern, as the guide's prompt names it.</param>
/// <param name="Often">One of <see cref="DigestValues.Often"/> for each kind of pull, over the items the pattern counts on that kind.</param>
public sealed record DigestPattern(string Pattern, Dictionary<string, string> Often);

/// <summary>The fixed strings every hero's insight messages use.</summary>
public static class DigestValues
{
    /// <summary>A boss pull.</summary>
    public const string Boss = "boss";

    /// <summary>A pull of several enemies.</summary>
    public const string TrashPack = "trash pack";

    /// <summary>A pull of one enemy.</summary>
    public const string SingleEnemy = "single enemy";

    /// <summary>A Perfect grade.</summary>
    public const string Full = "full";

    /// <summary>A grade between Perfect and Fail.</summary>
    public const string Partial = "partial";

    /// <summary>A Fail grade.</summary>
    public const string Low = "low";

    /// <summary>A pattern on none of the items.</summary>
    public const string Never = "never";

    /// <summary>A pattern on up to a quarter of the items.</summary>
    public const string Rarely = "rarely";

    /// <summary>A pattern on up to half of the items.</summary>
    public const string Sometimes = "sometimes";

    /// <summary>A pattern on over half of the items.</summary>
    public const string Mostly = "mostly";

    /// <summary>A pattern on every item.</summary>
    public const string Always = "always";

    /// <summary>A cast of a spell the spell data has no record of.</summary>
    public const string UnknownSpell = "an unknown spell";

    /// <summary>Every kind of pull.</summary>
    public static IReadOnlyList<string> Kinds { get; } = [Boss, TrashPack, SingleEnemy];

    /// <summary>Every grade level.</summary>
    public static IReadOnlyList<string> Levels { get; } = [Full, Partial, Low];

    /// <summary>Every frequency of a pattern.</summary>
    public static IReadOnlyList<string> Often { get; } = [Never, Rarely, Sometimes, Mostly, Always];

    /// <summary>Every string on this class and <see cref="InsightFocus"/>.</summary>
    public static IReadOnlyList<string> All { get; } = [.. Kinds, .. Levels, .. Often, UnknownSpell, .. InsightFocus.All];
}
