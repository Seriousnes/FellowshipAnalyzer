using FellowshipAnalyzer.Core.UI.Charts;
using FellowshipAnalyzer.Core.UI.Theming;
using FellowshipAnalyzer.DesignSystem.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSassCompiler();

builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<ChartPalette>();

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
