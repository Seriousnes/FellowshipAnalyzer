using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.UI.Charts;
using FellowshipAnalyzer.Core.UI.Components;
using FellowshipAnalyzer.Core.UI.Theming;
using FellowshipAnalyzer.DesignSystem.Components;

var builder = WebApplication.CreateBuilder(args);

var codex = builder.Configuration.GetSection(CodexOptions.SectionName);
Codex.Use(new CodexOptions
{
    Origin = codex["Origin"] ?? CodexOptions.Default.Origin,
    TextureOrigin = codex["TextureOrigin"] ?? CodexOptions.Default.TextureOrigin,
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSassCompiler();

builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<ChartPalette>();
builder.Services.AddScoped<ContributorModalService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
