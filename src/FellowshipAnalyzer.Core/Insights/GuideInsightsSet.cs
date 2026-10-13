using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Insights;

/// <summary>One guide's insights, or why the request failed.</summary>
/// <param name="Response">The insights.</param>
/// <param name="Error">Why the request failed.</param>
public sealed record GuideInsightsStatus(GuideInsightsResponse? Response, string? Error);

/// <summary>The insights of every guide in one report's digest, keyed by guide component.</summary>
public sealed class GuideInsightsSet
{
    private readonly IReadOnlyDictionary<Type, GuideInsightsStatus> _byGuide;

    private GuideInsightsSet(IReadOnlyList<Spell> spells, IReadOnlyDictionary<Type, GuideInsightsStatus> byGuide)
    {
        Spells = spells;
        _byGuide = byGuide;
    }

    /// <summary>Abilities and talents whose names become spell links in insight text.</summary>
    public IReadOnlyList<Spell> Spells { get; }

    /// <summary>The insights of the guide rendered by <paramref name="guide"/>, or null for a guide without a digest section.</summary>
    public GuideInsightsStatus? For(Type guide) => _byGuide.GetValueOrDefault(guide);

    /// <summary>Requests insights on every guide of <paramref name="insights"/> with a message at once and waits for all of them.</summary>
    public static async Task<GuideInsightsSet> LoadAsync(string hero, HeroInsights insights, IGuideInsightsClient client, CancellationToken cancellationToken = default)
    {
        var messages = insights.Messages();
        var requests = insights.Guides
            .Where(guide => messages.ContainsKey(guide.Key))
            .Select(async guide => (guide.Value, await RequestAsync(hero, guide.Key, messages[guide.Key], client, cancellationToken)))
            .ToList();
        var statuses = await Task.WhenAll(requests);
        return new GuideInsightsSet(insights.Spells, statuses.ToDictionary(entry => entry.Item1, entry => entry.Item2));
    }

    private static async Task<GuideInsightsStatus> RequestAsync(string hero, string guide, byte[] message, IGuideInsightsClient client, CancellationToken cancellationToken)
    {
        try
        {
            return new GuideInsightsStatus(await client.GetAsync(hero, guide, message, cancellationToken), null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new GuideInsightsStatus(null, exception.Message);
        }
    }
}
