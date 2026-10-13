using System.Globalization;
using System.Text.RegularExpressions;

using FellowshipAnalyzer.Core.Insights;

namespace FellowshipAnalyzer.Api.Core.Insights;

/// <summary>An insight <see cref="InsightValidation"/> dropped.</summary>
/// <param name="Index">Position of the insight in the response.</param>
/// <param name="Reason"><c>number-not-in-prompt</c>, <c>forbidden-term</c>, <c>id</c> or <c>beyond-limit</c>.</param>
/// <param name="Detail">The offending numbers, term or id.</param>
public sealed record InsightRejection(int Index, string Reason, string? Detail);

/// <summary>A response after validation.</summary>
/// <param name="Accepted">The insights kept.</param>
/// <param name="Rejections">Insights dropped.</param>
public sealed record InsightValidationResult(IReadOnlyList<GuideInsight> Accepted, IReadOnlyList<InsightRejection> Rejections);

/// <summary>
/// Checks a guide's insights against its system prompt: each number in an insight is one the prompt states, no
/// insight uses a term the prompt forbids, and none names an id such as <c>pull3</c>.
/// </summary>
public static partial class InsightValidation
{
    /// <summary>Most insights kept from one response.</summary>
    public const int MaxInsights = 3;

    [GeneratedRegex(@"(?<![A-Za-z0-9_.])-?\d+(?:\.\d+)?")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"\b[A-Za-z]+\d+\b")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"Never write (?<terms>""[^""]+""(?:,?(?:\s+or)?\s+""[^""]+"")*)\.")]
    private static partial Regex ForbiddenLine();

    [GeneratedRegex("\"(?<term>[^\"]+)\"")]
    private static partial Regex QuotedTerm();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>Terms the Output rules of <paramref name="guide"/> forbid in any text.</summary>
    public static IReadOnlyList<string> ForbiddenTerms(InsightGuide guide) =>
        ForbiddenLine().Match(guide.Overview) is { Success: true } line
            ? [.. QuotedTerm().Matches(line.Groups["terms"].Value).Select(match => Whitespace().Replace(match.Groups["term"].Value, " "))]
            : [];

    /// <summary>Validates <paramref name="insights"/>, the response on <paramref name="guide"/>.</summary>
    public static InsightValidationResult Check(IReadOnlyList<GuideInsight> insights, InsightGuide guide)
    {
        var accepted = new List<GuideInsight>();
        var rejections = new List<InsightRejection>();
        var allowed = Numbers(guide.System).ToList();
        var forbidden = ForbiddenTerms(guide);

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

    /// <summary>Numbers written in digits in <paramref name="text"/>, thousands separators ignored.</summary>
    public static IEnumerable<double> Numbers(string text) =>
        NumberPattern().Matches(text.Replace(",", "", StringComparison.Ordinal))
            .Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture));

    private static InsightRejection? Rejection(int index, string text, List<double> allowed, IReadOnlyList<string> forbidden)
    {
        if (forbidden.FirstOrDefault(term => text.Contains(term, StringComparison.OrdinalIgnoreCase)) is { } term)
            return new InsightRejection(index, "forbidden-term", term);
        if (IdPattern().Match(text) is { Success: true } id)
            return new InsightRejection(index, "id", id.Value);
        var offenders = Numbers(text)
            .Where(value => !allowed.Any(a => Math.Abs(Math.Abs(a) - Math.Abs(value)) < 0.051))
            .Select(value => value.ToString(CultureInfo.InvariantCulture))
            .ToList();
        return offenders.Count > 0 ? new InsightRejection(index, "number-not-in-prompt", string.Join(", ", offenders)) : null;
    }
}
