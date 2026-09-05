using System.Text.RegularExpressions;

using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData.Json;

namespace FellowshipAnalyzer.SpellData;

/// <summary>What one game-data description states about resource generation.</summary>
/// <param name="Stated">The generation the description states, or <c>null</c> when it states none.</param>
/// <param name="Unclaimed">
/// Each sentence that states an amount of a resource in a form no rule recognises, verbatim.
/// </param>
public sealed record GenerationStatement(ResourceGeneration? Stated, IReadOnlyList<string> Unclaimed)
{
    /// <summary>The result for a description that states no resource amount.</summary>
    public static GenerationStatement None { get; } = new(null, []);
}

/// <summary>
/// Reads the resource generation a game-data description states. The game writes these as a closed
/// set of sentence forms, one per line of the description, and this recognises exactly those forms:
/// a sentence stating an amount of a resource in any other form is returned unclaimed rather than
/// guessed at. Only magnitudes are read; the conditions a sentence attaches to one belong to the
/// analyzer that applies them.
/// </summary>
public static class Generation
{
    /// <summary>Reads the generation stated by <paramref name="description"/>.</summary>
    public static GenerationStatement Read(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return GenerationStatement.None;

        ResourceGeneration? amount = null;
        (ResourceTypes Resource, double Value)? critical = null;
        ResourceGeneration? increase = null;
        List<string>? unclaimed = null;

        foreach (var sentence in Sentences(description))
        {
            if (amount is null && MatchAmount(sentence) is { } matched)
            {
                amount = matched;
                continue;
            }

            if (critical is null && MatchCritical(sentence) is { } crit)
            {
                critical = crit;
                continue;
            }

            if (increase is null && MatchIncrease(sentence) is { } more)
            {
                increase = more;
                continue;
            }

            if (StatesAnAmount(sentence))
                (unclaimed ??= []).Add(sentence);
        }

        var stated = amount is not null
            ? amount with { CriticalAmount = critical?.Resource == amount.Resource ? critical?.Value : null }
            : increase;

        return stated is null && unclaimed is null
            ? GenerationStatement.None
            : new GenerationStatement(stated, unclaimed ?? []);
    }

    private static ResourceGeneration? MatchAmount(string sentence)
    {
        foreach (var (rule, measure, trigger) in AmountRules)
        {
            var match = rule.Match(sentence);
            if (!match.Success || !ResourceTypesAliases.TryResolve(match.Groups["resource"].Value, out var resource))
                continue;

            return new ResourceGeneration
            {
                Resource = resource,
                Amount = Magnitude(match, measure),
                Measure = measure,
                Trigger = trigger,
            };
        }
        return null;
    }

    private static (ResourceTypes Resource, double Value)? MatchCritical(string sentence)
    {
        foreach (var rule in CriticalRules)
        {
            var match = rule.Match(sentence);
            if (match.Success && ResourceTypesAliases.TryResolve(match.Groups["resource"].Value, out var resource))
                return (resource, Number(match));
        }
        return null;
    }

    private static ResourceGeneration? MatchIncrease(string sentence)
    {
        var match = IncreaseRule.Match(sentence);
        if (!match.Success || !ResourceTypesAliases.TryResolve(match.Groups["resource"].Value, out var resource))
            return null;

        return new ResourceGeneration
        {
            Resource = resource,
            Amount = AsFraction(Number(match)),
            Measure = GenerationMeasure.Increase,
        };
    }

    /// <summary>
    /// Whether a sentence puts a number against a named resource downstream of a generating verb. This
    /// separates a form the rules missed from prose that merely names a resource next to a number, such as
    /// the threshold in "when you are below 50% Chrona", and it is deliberately wider than the rules
    /// themselves so a missed form is reported rather than dropped.
    /// </summary>
    private static bool StatesAnAmount(string sentence) => GeneratedAmount.IsMatch(sentence);

    private static double Magnitude(Match match, GenerationMeasure measure) =>
        measure == GenerationMeasure.Flat ? Number(match) : AsFraction(Number(match));

    /// <summary>Turns a stated percentage into a fraction of one, at the precision the game states it to.</summary>
    private static double AsFraction(double percent) => Math.Round(percent / 100, 6);

    private static double Number(Match match) =>
        double.Parse(match.Groups["amount"].Value, System.Globalization.CultureInfo.InvariantCulture);

    private static IEnumerable<string> Sentences(string description)
    {
        foreach (var line in RichTextTag.Replace(description, string.Empty).Split('\n'))
        {
            foreach (var part in SentenceBreak.Split(line))
            {
                var sentence = Whitespace.Replace(part, " ").Trim().TrimEnd('.').Trim();
                if (sentence.Length > 0)
                    yield return sentence;
            }
        }
    }

    private const string Amount = @"(?<amount>\d+(?:\.\d+)?)";

    private static readonly string Resource =
        "(?<resource>" + string.Join("|", ResourceTypesAliases.Tokens.Select(Regex.Escape)) + ")";

    private static readonly Regex RichTextTag = new(@"<[^>]*>", RegexOptions.Compiled);

    private static readonly Regex SentenceBreak = new(@"(?<=\.)\s+", RegexOptions.Compiled);

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private static Regex Rule(string pattern) =>
        new("^" + pattern + "$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// The sentence forms that state an amount, most qualified first so "Generates 1 Anima per tick"
    /// is read as a per-hit amount rather than as a plain one whose resource fails to resolve.
    /// </summary>
    private static readonly (Regex Rule, GenerationMeasure Measure, GenerationTrigger Trigger)[] AmountRules =
    [
        (Rule($@"Generates? {Amount} {Resource}(?: per (?:tick|hit|bolt|pulse|strike|enemy)| with each strike| for each enemy hit)"),
            GenerationMeasure.Flat, GenerationTrigger.PerHit),
        (Rule($@"Generates? {Amount} {Resource} over its full duration"),
            GenerationMeasure.Flat, GenerationTrigger.OverDuration),
        (Rule($@"Generates? {Amount} {Resource}"),
            GenerationMeasure.Flat, GenerationTrigger.PerCast),
        (Rule($@"Instantly generates? {Amount} {Resource}"),
            GenerationMeasure.Flat, GenerationTrigger.PerCast),
        (Rule($@"Instantly restores? {Amount}% of your maximum {Resource}"),
            GenerationMeasure.FractionOfMaximum, GenerationTrigger.PerCast),
        (Rule($@"Each time .+ deals damage, it generates {Amount} {Resource}"),
            GenerationMeasure.Flat, GenerationTrigger.PerHit),
        (Rule($@"Each pulse of .+ generates {Amount} {Resource}"),
            GenerationMeasure.Flat, GenerationTrigger.PerHit),
        (Rule($@"Each tick of .+ replenishes {Amount} {Resource}"),
            GenerationMeasure.Flat, GenerationTrigger.PerHit),
        (Rule($@"When you cast .+, you gain {Amount} {Resource}"),
            GenerationMeasure.Flat, GenerationTrigger.PerCast),
        (Rule($@".+ replenishes {Amount}% of your maximum {Resource} per stack when it expires"),
            GenerationMeasure.FractionOfMaximum, GenerationTrigger.PerStack),
    ];

    /// <summary>The sentence forms that state the amount a critical strike generates instead.</summary>
    private static readonly Regex[] CriticalRules =
    [
        Rule($@"Critical Strike chance to generate {Amount} {Resource}"),
        Rule($@"Critical Strikes generate {Amount} {Resource}"),
    ];

    /// <summary>
    /// The clause that raises the generation the rest of the kit states. It is matched inside a sentence
    /// rather than against the whole of one, because the game states it alongside the condition that
    /// gates it and alongside the other things the same talent changes.
    /// </summary>
    private static readonly Regex IncreaseRule =
        new($@"\bgenerates? {Amount}% (?:more|increased) {Resource}\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex GeneratedAmount =
        new($@"\b(?:generat(?:e|es|ing)|restores?|replenishes?|gains?)\b.{{0,40}}?"
            + $@"\d+(?:\.\d+)?%?(?: of (?:a|an|the|your maximum))? {Resource}\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
}
