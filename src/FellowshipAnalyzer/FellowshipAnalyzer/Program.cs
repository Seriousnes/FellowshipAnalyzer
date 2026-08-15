using System.Text.Json;
using System.Text.Json.Serialization;
using ApexCharts;
using FellowshipAnalyzer;
using FellowshipAnalyzer.Services;
using FellowshipAnalyzer.Core.UI.Charts;
using FellowshipAnalyzer.Core.UI.Theming;
using FellowshipAnalyzer.Core.UI.Timeline;
using FellowshipAnalyzer.Core.UI.Components;
using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Serialization;
using Microsoft.AspNetCore.Components.Web;

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var hostBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = hostBaseAddress });

var jsonOptions = new JsonSerializerOptions(JsonSerializerOptions.Web)
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    AllowOutOfOrderMetadataProperties = true,
};
jsonOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
var jsonContext = new FellowshipAnalyzerJsonContext(new JsonSerializerOptions(jsonOptions));
jsonOptions.TypeInfoResolverChain.Insert(0, jsonContext);
builder.Services.AddSingleton(jsonOptions);
builder.Services.AddSingleton(jsonContext);


builder.Services.AddApexCharts();
builder.Services.AddCoreAnalysisServices();
builder.Services.AddCoreAnalysis();
builder.Services.AddFellowshipHeroAnalysis();
builder.Services.AddSingleton<IHeroConfigCatalog, HeroConfigCatalog>();
builder.Services.AddScoped<ContributorModalService>();

builder.Services.AddScoped<ReportLoadingTracker>();
builder.Services.AddScoped<ReportNavigationState>();
builder.Services.AddScoped<FellowshipLogsApiClient>();
builder.Services.AddScoped<IReportCacheService, IndexedDbReportCacheService>();
builder.Services.AddScoped<ReportAnalysisService>();
builder.Services.AddScoped<TimelineConfigService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<ChartPalette>();

await builder.Build().RunAsync();
