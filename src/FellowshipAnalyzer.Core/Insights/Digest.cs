using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Insights;

/// <summary>Talent names and the legendary item name.</summary>
public sealed record DigestBuild(List<string> Talents, string? Legendary);

/// <summary>
/// The user message of one guide's insights call. It holds no number, grade or pull name, so none can reach an
/// insight.
/// </summary>
/// <param name="Guide">Guide name, as the hero's playbook names it.</param>
/// <param name="Focus">One of <see cref="InsightFocus"/>'s values, from the guide's grades.</param>
/// <param name="Build">Talents and legendary of the player.</param>
/// <param name="Patterns">How often each of the guide's patterns showed, by kind of pull.</param>
/// <param name="Pulls">The guide's facts on each pull with an item, in order.</param>
public sealed record InsightMessage<TPull>(string Guide, string Focus, DigestBuild Build, List<DigestPattern> Patterns, List<TPull> Pulls);

/// <summary>How often one of a guide's patterns showed.</summary>
/// <param name="Pattern">The pattern, as the guide's playbook names it.</param>
/// <param name="Often">
/// <c>never</c>, <c>rarely</c>, <c>sometimes</c>, <c>mostly</c> or <c>always</c> for each kind of pull, over
/// the items the pattern counts on that kind.
/// </param>
public sealed record DigestPattern(string Pattern, Dictionary<string, string> Often);

/// <summary>The parts every hero's insight messages share.</summary>
public static class Digest
{
    /// <summary>The <paramref name="tree"/> talents the player took, by name, and the equipped legendary.</summary>
    public static DigestBuild Build(CombatLogParser parser, IEnumerable<Talent> tree)
    {
        var combatant = parser.SelectedCombatant;
        var talents = tree
            .Where(talent => combatant.HasTalent(talent.Id))
            .Select(talent => talent.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
        return new DigestBuild(talents, combatant.Legendary is { Name.Length: > 0 } legendary ? legendary.Name : null);
    }

    /// <summary>
    /// The focus of the most common grade in <paramref name="grades"/>, a tie going to the lower grade:
    /// <see cref="InsightFocus.Strengths"/> for Perfect, <see cref="InsightFocus.Mixed"/> for Good and Ok,
    /// <see cref="InsightFocus.Fixes"/> for Fail. Unrated rows have no say; with none rated the focus is
    /// <see cref="InsightFocus.Mixed"/>.
    /// </summary>
    public static string Focus(IEnumerable<QualitativePerformance?> grades)
    {
        var most = grades.OfType<QualitativePerformance>()
            .GroupBy(grade => grade)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => (QualitativePerformance?)group.Key)
            .FirstOrDefault();
        return most switch
        {
            QualitativePerformance.Perfect => InsightFocus.Strengths,
            QualitativePerformance.Fail => InsightFocus.Fixes,
            _ => InsightFocus.Mixed,
        };
    }

    /// <summary>
    /// <paramref name="pattern"/> with, for each kind of pull in <paramref name="items"/>, how often its items
    /// show it.
    /// </summary>
    public static DigestPattern Pattern(string pattern, IEnumerable<(string Kind, bool Shows)> items) => new(
        pattern,
        items.GroupBy(item => item.Kind, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => Often(group.Count(item => item.Shows), group.Count()), StringComparer.Ordinal));

    /// <summary>
    /// <c>never</c> for none of <paramref name="total"/>, <c>always</c> for all, <c>rarely</c> up to a quarter,
    /// <c>sometimes</c> up to half, <c>mostly</c> above half.
    /// </summary>
    public static string Often(int shown, int total) => shown switch
    {
        0 => "never",
        _ when shown == total => "always",
        _ when shown * 4 <= total => "rarely",
        _ when shown * 2 <= total => "sometimes",
        _ => "mostly",
    };

    /// <summary><c>boss</c>, <c>trash pack</c> or <c>single enemy</c>.</summary>
    public static string Kind(PullStartEvent pull) =>
        pull.IsBoss ? "boss" : pull.Targets == PullKind.Multi ? "trash pack" : "single enemy";

    /// <summary>
    /// <c>full</c> for <see cref="QualitativePerformance.Perfect"/>, <c>low</c> for
    /// <see cref="QualitativePerformance.Fail"/>, <c>partial</c> otherwise.
    /// </summary>
    public static string Level(QualitativePerformance tier) => tier switch
    {
        QualitativePerformance.Perfect => "full",
        QualitativePerformance.Fail => "low",
        _ => "partial",
    };
}
