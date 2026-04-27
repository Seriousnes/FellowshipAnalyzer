namespace FellowshipAnalyzer.Api.Core;

public sealed class FellowshipLogsClientOptions
{
    public const string DefaultTokenEndpoint = "https://www.fellowshiplogs.com/oauth/token";
    public const string DefaultGraphQlEndpoint = "https://www.fellowshiplogs.com/api/v2/client";

    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string TokenEndpoint { get; init; } = DefaultTokenEndpoint;
    public string GraphQlEndpoint { get; init; } = DefaultGraphQlEndpoint;
}
