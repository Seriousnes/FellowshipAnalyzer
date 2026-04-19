using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FellowshipAnalyzer.FellowshipLogs;
using FellowshipAnalyzer.Components;
using FellowshipAnalyzer.Components.Account;
using FellowshipAnalyzer.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddFellowshipLogsService(builder.Configuration);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(sp.GetRequiredService<NavigationManager>().BaseUri)
});
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(FellowshipAnalyzer.Client._Imports).Assembly);

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.MapGet(
    "/api/report/{reportCode}",
    async (
        string reportCode,
        FellowshipLogsProxy proxy,
        HttpContext ctx,
        CancellationToken cancellationToken) =>
    {
        using var upstream = await proxy.ProxyReportAsync(reportCode, cancellationToken);
        await StreamUpstreamResponseAsync(upstream, ctx, cancellationToken);
    });

app.MapGet(
    "/api/events",
    async (
        string reportCode,
        int playerId,
        int fightId,
        FellowshipLogsProxy proxy,
        HttpContext ctx,
        CancellationToken cancellationToken) =>
    {
        using var upstream = await proxy.ProxyEventsAsync(reportCode, playerId, fightId, cancellationToken);
        await StreamUpstreamResponseAsync(upstream, ctx, cancellationToken);
    });

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

app.Run();
