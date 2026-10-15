using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Insights;

/// <summary>The parts every hero's insight messages share.</summary>
public static class Digest
{
    /// <summary>
    /// The <paramref name="tree"/> talents the player took, by name, and the power name of the equipped legendary
    /// when it is one of <paramref name="legendaries"/>.
    /// </summary>
    public static DigestBuild Build(CombatLogParser parser, IEnumerable<Talent> tree, IEnumerable<Common.Items.Item> legendaries)
    {
        var combatant = parser.SelectedCombatant;
        var talents = tree
            .Where(talent => combatant.HasTalent(talent.Id))
            .Select(talent => talent.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
        return new DigestBuild(talents, legendaries.FirstOrDefault(legendary => legendary.Id == combatant.Legendary?.Id)?.Name);
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
        0 => DigestValues.Never,
        _ when shown == total => DigestValues.Always,
        _ when shown * 4 <= total => DigestValues.Rarely,
        _ when shown * 2 <= total => DigestValues.Sometimes,
        _ => DigestValues.Mostly,
    };

    /// <summary><c>boss</c>, <c>trash pack</c> or <c>single enemy</c>.</summary>
    public static string Kind(PullStartEvent pull) =>
        pull.IsBoss ? DigestValues.Boss : pull.Targets == PullKind.Multi ? DigestValues.TrashPack : DigestValues.SingleEnemy;

    /// <summary>
    /// <c>full</c> for <see cref="QualitativePerformance.Perfect"/>, <c>low</c> for
    /// <see cref="QualitativePerformance.Fail"/>, <c>partial</c> otherwise.
    /// </summary>
    public static string Level(QualitativePerformance tier) => tier switch
    {
        QualitativePerformance.Perfect => DigestValues.Full,
        QualitativePerformance.Fail => DigestValues.Low,
        _ => DigestValues.Partial,
    };
}
