namespace FellowshipAnalyzer.Api.Core.Insights;

/// <summary>Model access for <see cref="GuideInsightsService"/>.</summary>
public sealed class GuideInsightsOptions
{
    /// <summary>Configuration section of these options. The <c>Insights__Enabled</c> environment variable sets <see cref="Enabled"/>.</summary>
    public const string SectionName = "Insights";

    /// <summary>Switches guide insights on. Off when the setting is missing.</summary>
    public bool Enabled { get; set; }

    /// <summary>Anthropic API key, from the <c>ClaudeApiKey</c> setting. Insights are off without one.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Anthropic workspace id for a service key, from the <c>ClaudeWorkspaceId</c> setting.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Model id for every insights call.</summary>
    public string Model { get; set; } = "claude-haiku-5-5";

    /// <summary>Reasoning effort for every insights call: <c>low</c>, <c>medium</c> or <c>high</c>.</summary>
    public string Effort { get; set; } = "low";

    /// <summary>How long a guide's insights stay in the insights cache.</summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Largest request body the endpoint reads, in bytes.</summary>
    public long MaxRequestBytes { get; set; } = 96 * 1024;

    /// <summary>Whether <see cref="Enabled"/> is set and <see cref="ApiKey"/> holds a key.</summary>
    public bool Available => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
