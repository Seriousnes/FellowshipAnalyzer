using FellowshipAnalyzer.Api.Core;
using FellowshipAnalyzer.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddFellowshipLogsApi(
    builder.Configuration,
    allowDevelopmentLoopbackOrigins: builder.Environment.IsDevelopment());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseSwagger();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    o.RoutePrefix = string.Empty;
});

app.MapGet("/api/events", async (
    HttpContext httpContext,
    FellowshipLogsApiHandler handler,
    string? reportCode,
    int? playerId,
    int? fightId,
    CancellationToken cancellationToken) =>
{
    var response = await handler.GetEventsAsync(
        BuildContext(httpContext.Request),
        reportCode,
        playerId,
        fightId,
        cancellationToken);

    await WriteResponseAsync(httpContext, response);
});

app.MapGet("/api/analysis/{reportCode}", async (
    HttpContext httpContext,
    FellowshipLogsApiHandler handler,
    string reportCode,
    CancellationToken cancellationToken) =>
{
    var response = await handler.GetAnalysisAsync(
        BuildContext(httpContext.Request),
        reportCode,
        cancellationToken);

    await WriteResponseAsync(httpContext, response);
});

app.MapGet("/api/character/{id:int}", async (
    HttpContext httpContext,
    FellowshipLogsApiHandler handler,
    int id,
    CancellationToken cancellationToken) =>
{
    var response = await handler.GetCharacterReportsAsync(
        BuildContext(httpContext.Request),
        id,
        cancellationToken);

    await WriteResponseAsync(httpContext, response);
});

app.Run();

static EndpointRequestContext BuildContext(HttpRequest request) => new()
{
    Origin = request.Headers.Origin.ToString(),
    ForwardedFor = request.Headers["X-Forwarded-For"].ToString(),
    RemoteIp = request.HttpContext.Connection.RemoteIpAddress?.ToString()
};

static async Task WriteResponseAsync(HttpContext httpContext, EndpointResponse response)
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
}
