using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using ApexCharts;
using FellowshipAnalyzer.Client.Services;
using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Rime.Analysis;

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

using var hostConfigurationClient = new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
};

var clientConfiguration = await hostConfigurationClient.GetFromJsonAsync<ClientConfiguration>("config.json")
    ?? throw new InvalidOperationException("The FellowshipAnalyzer host did not provide client configuration.");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(clientConfiguration.ApiBaseUrl) });

// JSON options for deserializing API responses (including polymorphic events)
var jsonOptions = new JsonSerializerOptions(JsonSerializerOptions.Web)
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    AllowOutOfOrderMetadataProperties = true,
};
jsonOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
builder.Services.AddSingleton(jsonOptions);

// Hero analysis runs client-side in WASM
builder.Services.AddApexCharts();
builder.Services.AddCoreAnalysisServices();
builder.Services.AddCoreAnalysis();
builder.Services.AddRimeAnalysis();

// Per-report loading progress tracker
builder.Services.AddScoped<ReportLoadingTracker>();

// Navigation state cache (fight/player selection → analysis page)
builder.Services.AddScoped<ReportNavigationState>();

// IFellowshipLogsClient: WASM proxy client deserializes raw GraphQL responses from API endpoints
builder.Services.AddScoped<IFellowshipLogsClient, FellowshipLogsProxyClient>();

// Report history + event cache (IndexedDB-backed)
builder.Services.AddScoped<IReportCacheService, IndexedDbReportCacheService>();

// Analysis orchestration service
builder.Services.AddScoped<ReportAnalysisService>();

await builder.Build().RunAsync();

internal sealed record ClientConfiguration(string ApiBaseUrl);
