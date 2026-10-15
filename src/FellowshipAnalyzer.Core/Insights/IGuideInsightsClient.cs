namespace FellowshipAnalyzer.Core.Insights;

/// <summary>Sends one guide's insight message to the insights endpoint.</summary>
public interface IGuideInsightsClient
{
    /// <summary>Whether the insights endpoint is switched on, read once when the app loads.</summary>
    Task<bool> Available { get; }

    /// <summary>The insights on <paramref name="guide"/> of a <paramref name="hero"/> run, from its UTF-8 JSON <paramref name="message"/>.</summary>
    Task<GuideInsightsResponse> GetAsync(string hero, string guide, byte[] message, CancellationToken cancellationToken = default);
}
