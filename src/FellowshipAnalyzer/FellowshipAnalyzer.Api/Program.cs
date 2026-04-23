using System.Text.Json;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.FellowshipLogs;
using FellowshipAnalyzer.FellowshipLogs.Extensions;
using FellowshipAnalyzer.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddFellowshipLogsService(builder.Configuration);

var configuredAllowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray()
    ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "WasmHost",
        policy =>
        {
            policy.AllowAnyHeader()
                .AllowAnyMethod();

            if (configuredAllowedOrigins.Length > 0)
            {
                policy.WithOrigins(configuredAllowedOrigins);
                return;
            }

            if (builder.Environment.IsDevelopment())
            {
                policy.SetIsOriginAllowed(IsDevelopmentLoopbackOrigin);
                return;
            }

            policy.WithOrigins("http://fellowshipanalyzer.dev.localhost:5122");
        });
});

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();

app.MapGet(
        "/api/events",
        async (
            string reportCode,
            int playerId,
            int fightId,
            IFellowshipLogsClient client,
            JsonSerializerOptions jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var request = new FellowshipLogsEventsRequest(reportCode, playerId, fightId);
            var result = await client.Events.GetAsync(request, cancellationToken);
            return Results.Json(result, jsonOptions);
        })
    .RequireCors("WasmHost");

app.MapGet(
        "/api/analysis/{reportCode}",
        async (
            string reportCode,
            IFellowshipLogsClient client,
            JsonSerializerOptions jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var preload = await client.AnalysisPreload.GetAsync(reportCode, cancellationToken);
            return Results.Json(preload, jsonOptions);
        })
    .RequireCors("WasmHost");

app.Run();

static bool IsDevelopmentLoopbackOrigin(string origin)
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