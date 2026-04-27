namespace FellowshipAnalyzer.Api.Core;

public sealed class FellowshipLogsCacheOptions
{
    public const string SectionName = "FellowshipLogs:Cache";

    public TimeSpan RecentReportWindow { get; set; } = TimeSpan.FromHours(2);
    public TimeSpan RecentReportMetadataCacheDuration { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan StableReportMetadataCacheDuration { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan CompletedEventsCacheDuration { get; set; } = TimeSpan.FromDays(30);
}

public sealed class FellowshipLogsRateLimitOptions
{
    public const string SectionName = "FellowshipLogs:RateLimit";

    public int PermitLimit { get; set; } = 60;
    public int QueueLimit { get; set; } = 10;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

public sealed class FellowshipLogsCorsOptions(string[] allowedOrigins, bool allowDevelopmentLoopbackOrigins)
{
    private readonly HashSet<string> _allowedOrigins = allowedOrigins.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool IsAllowedOrigin(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        return _allowedOrigins.Contains(origin)
            || (allowDevelopmentLoopbackOrigins && IsDevelopmentLoopbackOrigin(origin));
    }

    private static bool IsDevelopmentLoopbackOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return uri.IsLoopback
            || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }
}
