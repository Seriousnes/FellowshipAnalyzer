namespace FellowshipAnalyzer.Api.Core;

/// <summary>
/// Framework-agnostic incoming request context required by <see cref="FellowshipLogsApiHandler"/>.
/// Populated by each host (Functions, Kestrel) from its native request type.
/// </summary>
public sealed class EndpointRequestContext
{
    /// <summary>The value of the request's <c>Origin</c> header, or empty string.</summary>
    public string Origin { get; init; } = string.Empty;

    /// <summary>The value of the request's <c>X-Forwarded-For</c> header, or empty string.</summary>
    public string ForwardedFor { get; init; } = string.Empty;

    /// <summary>The remote IP address of the connection, used as a rate-limit partition fallback.</summary>
    public string? RemoteIp { get; init; }
}
