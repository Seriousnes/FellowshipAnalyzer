using System.Text.RegularExpressions;

namespace FellowshipAnalyzer.Core.Insights;

/// <summary>An insight <see cref="InsightValidation"/> dropped.</summary>
/// <param name="Index">Position of the insight in the response.</param>
/// <param name="Reason"><c>number-not-in-playbook</c>, <c>forbidden-term</c>, <c>id</c> or <c>beyond-limit</c>.</param>
/// <param name="Detail">The offending numbers, term or id.</param>
public sealed record InsightRejection(int Index, string Reason, string? Detail);

/// <summary>A response after validation.</summary>
/// <param name="Accepted">The insights kept.</param>
/// <param name="Rejections">Insights dropped.</param>
public sealed record InsightValidationResult(IReadOnlyList<GuideInsight> Accepted, IReadOnlyList<InsightRejection> Rejections);

/// <summary>
/// Checks a guide's insights against its playbook: each number in an insight is one the playbook states, no
/// insight uses a term the playbook forbids, and none names an id such as <c>pull3</c>.
/// </summary>
public static partial class InsightValidation
{
    /// <summary>Most insights kept from one response.</summary>
    public const int MaxInsights = 3;

    [GeneratedRegex(@"\b[A-Za-z]+\d+\b")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"Never write (?<terms>""[^""]+""(?:,?(?:\s+or)?\s+""[^""]+"")*)\.")]
    private static partial Regex ForbiddenLine();

    [GeneratedRegex("\"(?<term>[^\"]+)\"")]
    private static partial Regex QuotedTerm();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>Terms the playbook's Output section forbids in any text.</summary>
    public static IReadOnlyList<string> ForbiddenTerms(Playbook playbook) =>
        ForbiddenLine().Match(playbook.Overview) is { Success: true } line
            ? [.. QuotedTerm().Matches(line.Groups["terms"].Value).Select(match => Whitespace().Replace(match.Groups["term"].Value, " "))]
            : [];

    /// <summary>Validates <paramref name="insights"/>, the response on <paramref name="guide"/> of <paramref name="playbook"/>.</summary>
    public static InsightValidationResult Check(IReadOnlyList<GuideInsight> insights, Playbook playbook, string guide)
    {
        var accepted = new List<GuideInsight>();
        var rejections = new List<InsightRejection>();
        var allowed = playbook.GuideNumbers(guide).ToList();
        var forbidden = ForbiddenTerms(playbook);

        for (var index = 0; index < insights.Count; index++)
        {
            if (accepted.Count == MaxInsights)
            {
                rejections.Add(new InsightRejection(index, "beyond-limit", null));
                continue;
            }

            var text = insights[index].Title + " " + insights[index].Insight;
            if (Rejection(index, text, allowed, forbidden) is { } rejection)
                rejections.Add(rejection);
            else
                accepted.Add(insights[index]);
        }

        return new InsightValidationResult(accepted, rejections);
    }

    private static InsightRejection? Rejection(int index, string text, List<double> allowed, IReadOnlyList<string> forbidden)
    {
        if (forbidden.FirstOrDefault(term => text.Contains(term, StringComparison.OrdinalIgnoreCase)) is { } term)
            return new InsightRejection(index, "forbidden-term", term);
        if (IdPattern().Match(text) is { Success: true } id)
            return new InsightRejection(index, "id", id.Value);
        var offenders = Playbook.Numbers(text)
            .Where(value => !allowed.Any(a => Math.Abs(Math.Abs(a) - Math.Abs(value)) < 0.051))
            .Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
        return offenders.Count > 0 ? new InsightRejection(index, "number-not-in-playbook", string.Join(", ", offenders)) : null;
    }
}
