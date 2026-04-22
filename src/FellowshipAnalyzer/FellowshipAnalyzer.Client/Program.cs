using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using FellowshipAnalyzer.Client.Services;
using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Serialization;
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
};
jsonOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
jsonOptions.Converters.Add(new FSLJsonConverter<Event>());
builder.Services.AddSingleton(jsonOptions);

// Hero analysis runs client-side in WASM
builder.Services.AddCoreAnalysisServices();
builder.Services.AddRimeAnalysis();

// Per-report loading progress tracker
builder.Services.AddScoped<ReportLoadingTracker>();

// IFellowshipLogsClient: WASM proxy client deserializes raw GraphQL responses from API endpoints
builder.Services.AddScoped<IFellowshipLogsClient, FellowshipLogsProxyClient>();

// Report history + event cache (IndexedDB-backed)
builder.Services.AddScoped<IReportCacheService, IndexedDbReportCacheService>();

await builder.Build().RunAsync();

internal sealed record ClientConfiguration(string ApiBaseUrl);
