namespace FellowshipAnalyzer.Core.Insights;

/// <summary>Sends one guide's insights request to the insights endpoint.</summary>
public interface IGuideInsightsClient
{
    /// <summary>Whether the insights endpoint is switched on, read once when the app loads.</summary>
    Task<bool> Available { get; }

    /// <summary>The model's insights on <paramref name="guide"/> of a <paramref name="hero"/> run, before validation.</summary>
    Task<GuideInsightsResponse> GetAsync(string hero, string guide, GuideInsightsRequest request, CancellationToken cancellationToken = default);
}
