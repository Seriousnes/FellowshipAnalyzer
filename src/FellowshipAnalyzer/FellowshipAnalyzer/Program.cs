using FellowshipAnalyzer.Components;
using FellowshipAnalyzer.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Fail fast if the API base URL cannot be resolved. We deliberately do NOT
// silently fall back to a hardcoded localhost URL: missing service discovery
// or configuration must surface as an error rather than be hidden behind a
// stale default that masks broken Aspire wiring.
var publicApiHttpBaseUrl =
    builder.Configuration["Services:fellowshipanalyzerapi:http:0"]?.TrimEnd('/')
    ?? builder.Configuration["PublicApi:HttpBaseUrl"]?.TrimEnd('/')
    ?? builder.Configuration["PublicApi:BaseUrl"]?.TrimEnd('/')
    ?? throw new InvalidOperationException(
        "API base URL (http) is not configured. Expected one of "
        + "'Services:fellowshipanalyzerapi:http:0' (Aspire service discovery), "
        + "'PublicApi:HttpBaseUrl', or 'PublicApi:BaseUrl'.");

var publicApiHttpsBaseUrl =
    builder.Configuration["Services:fellowshipanalyzerapi:https:0"]?.TrimEnd('/')
    ?? builder.Configuration["PublicApi:HttpsBaseUrl"]?.TrimEnd('/')
    ?? publicApiHttpBaseUrl;

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(FellowshipAnalyzer.Client._Imports).Assembly);

app.MapGet(
    "/config.json",
    (HttpContext httpContext) => TypedResults.Ok(
        new ClientConfiguration(
            string.Equals(httpContext.Request.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                ? publicApiHttpsBaseUrl
                : publicApiHttpBaseUrl)));

app.Run();

internal sealed record ClientConfiguration(string ApiBaseUrl);
