using System.Text.Json;

using FellowshipAnalyzer.Core.Insights;

using Microsoft.AspNetCore.Http;

namespace FellowshipAnalyzer.Api.Core.Insights;

/// <summary>
/// Guide insights endpoints. The insights body is the guide's insight message, which the client builds from the
/// parsed report; the response is a <see cref="GuideInsightsResponse"/>.
/// </summary>
public sealed class GuideInsightsApiHandler(
    GuideInsightsService insights,
    FellowshipLogsRateLimiter rateLimiter,
    JsonSerializerOptions jsonOptions)
{
    /// <summary>Whether guide insights are switched on, as an <see cref="InsightsAvailability"/>.</summary>
    [ApiEndpoint("GET", "insights")]
    public Task<IResult> GetInsightsAvailabilityAsync() =>
        Task.FromResult(Results.Json(new InsightsAvailability(insights.Available), jsonOptions));

    [ApiEndpoint("POST", "insights/{hero}/{guide}")]
    public async Task<IResult> GetGuideInsightsAsync(
        HttpContext context,
        string hero,
        string guide,
        CancellationToken cancellationToken)
    {
        if (!insights.Available)
        {
            return Error(503, "Guide insights are not available.");
        }

        if (await RateLimitResults.TryApplyAsync(rateLimiter, context, jsonOptions, cancellationToken) is { } limited)
        {
            return limited;
        }

        if (context.Request.ContentLength is { } length && length > insights.MaxRequestBytes)
        {
            return Error(413, "The request is too large.");
        }

        var body = await ReadBodyAsync(context.Request, insights.MaxRequestBytes, cancellationToken);
        if (body is null)
        {
            return Error(413, "The request is too large.");
        }

        var outcome = await insights.GetAsync(hero, guide, body, cancellationToken);
        return outcome.Response is { } response
            ? Results.Json(response, jsonOptions)
            : Error(outcome.StatusCode, outcome.Error ?? "Insights failed.");
    }

    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, long maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
                return null;
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private IResult Error(int statusCode, string message) =>
        Results.Json(new { error = message }, jsonOptions, statusCode: statusCode);
}
