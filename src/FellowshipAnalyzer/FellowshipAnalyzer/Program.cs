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

var publicApiHttpBaseUrl =
    builder.Configuration["Services:fellowshipanalyzerapi:http:0"]?.TrimEnd('/')
    ?? builder.Configuration["PublicApi:HttpBaseUrl"]?.TrimEnd('/')
    ?? builder.Configuration["PublicApi:BaseUrl"]?.TrimEnd('/')
    ?? "http://localhost:5123";

var publicApiHttpsBaseUrl =
    builder.Configuration["Services:fellowshipanalyzerapi:https:0"]?.TrimEnd('/')
    ?? builder.Configuration["PublicApi:HttpsBaseUrl"]?.TrimEnd('/')
    ?? "https://localhost:57510";

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
