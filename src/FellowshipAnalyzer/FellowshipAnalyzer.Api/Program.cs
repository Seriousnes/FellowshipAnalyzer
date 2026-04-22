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
        "/api/report/{reportCode}",
        async (
            string reportCode,
            IFellowshipLogsProxy proxy,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            using var upstream = await proxy.ProxyReportAsync(reportCode, cancellationToken);
            await StreamUpstreamResponseAsync(upstream, ctx, cancellationToken);
        })
    .RequireCors("WasmHost");

app.MapGet(
        "/api/events",
        async (
            string reportCode,
            int playerId,
            int fightId,
            IFellowshipLogsProxy proxy,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            using var upstream = await proxy.ProxyEventsAsync(reportCode, playerId, fightId, cancellationToken);
            await StreamUpstreamResponseAsync(upstream, ctx, cancellationToken);
        })
    .RequireCors("WasmHost");

app.MapGet(
        "/api/masterdata/{reportCode}",
        async (
            string reportCode,
            IFellowshipLogsProxy proxy,
            HttpContext ctx,
            CancellationToken cancellationToken) =>
        {
            using var upstream = await proxy.ProxyMasterDataAsync(reportCode, cancellationToken);
            await StreamUpstreamResponseAsync(upstream, ctx, cancellationToken);
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

static async Task StreamUpstreamResponseAsync(
    HttpResponseMessage upstream,
    HttpContext ctx,
    CancellationToken cancellationToken)
{
    if (!upstream.IsSuccessStatusCode)
    {
        ctx.Response.StatusCode = (int)upstream.StatusCode;
        return;
    }

    ctx.Response.ContentType = "application/json";

    if (upstream.Content.Headers.ContentEncoding.Contains("gzip"))
    {
        ctx.Response.Headers.ContentEncoding = "gzip";
    }

    await upstream.Content.CopyToAsync(ctx.Response.Body, cancellationToken);
}