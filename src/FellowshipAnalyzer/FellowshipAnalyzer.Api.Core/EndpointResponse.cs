namespace FellowshipAnalyzer.Api.Core;

/// <summary>
/// Framework-agnostic representation of an HTTP response produced by <see cref="FellowshipLogsApiHandler"/>.
/// Each host (Functions, Kestrel) translates this into its native response type.
/// </summary>
public sealed class EndpointResponse
{
    public required int StatusCode { get; init; }
    public required string ContentType { get; init; }
    public byte[]? Body { get; init; }
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
}
