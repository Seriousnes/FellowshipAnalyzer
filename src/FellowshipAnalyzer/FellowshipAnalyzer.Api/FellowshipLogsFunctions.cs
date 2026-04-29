using System.Globalization;

using FellowshipAnalyzer.Api.Core;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace FellowshipAnalyzer.Api;

internal sealed class FellowshipLogsFunctions(FellowshipLogsApiHandler handler)
{
    [Function(nameof(GetEvents))]
    public async Task<IActionResult> GetEvents(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var reportCode = request.Query["reportCode"].ToString();
        var playerId = TryParseInt(request.Query["playerId"]);
        var fightId = TryParseInt(request.Query["fightId"]);

        var response = await handler.GetEventsAsync(
            BuildContext(request),
            reportCode,
            playerId,
            fightId,
            cancellationToken);

        return await TranslateAsync(request.HttpContext, response);
    }

    [Function(nameof(GetAnalysis))]
    public async Task<IActionResult> GetAnalysis(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "analysis/{reportCode}")] HttpRequest request,
        string reportCode,
        CancellationToken cancellationToken)
    {
        var response = await handler.GetAnalysisAsync(
            BuildContext(request),
            reportCode,
            cancellationToken);

        return await TranslateAsync(request.HttpContext, response);
    }

    [Function(nameof(GetCharacterReports))]
    public async Task<IActionResult> GetCharacterReports(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "character/{id}")] HttpRequest request,
        string id,
        CancellationToken cancellationToken)
    {
        var characterId = TryParseInt(id);

        var response = await handler.GetCharacterReportsAsync(
            BuildContext(request),
            characterId,
            cancellationToken);

        return await TranslateAsync(request.HttpContext, response);
    }

    private static EndpointRequestContext BuildContext(HttpRequest request) => new()
    {
        Origin = request.Headers.Origin.ToString(),
        ForwardedFor = request.Headers["X-Forwarded-For"].ToString(),
        RemoteIp = request.HttpContext.Connection.RemoteIpAddress?.ToString()
    };

    private static int? TryParseInt(Microsoft.Extensions.Primitives.StringValues raw)
    {
        var text = raw.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static int? TryParseInt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static async Task<IActionResult> TranslateAsync(HttpContext httpContext, EndpointResponse response)
    {
        foreach (var (key, value) in response.Headers)
        {
            httpContext.Response.Headers[key] = value;
        }

        httpContext.Response.StatusCode = response.StatusCode;
        httpContext.Response.ContentType = response.ContentType;

        if (response.Body is { Length: > 0 })
        {
            await httpContext.Response.Body.WriteAsync(response.Body);
        }

        return new EmptyResult();
    }
}
