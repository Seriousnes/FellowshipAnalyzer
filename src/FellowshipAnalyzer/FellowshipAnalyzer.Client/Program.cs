using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using ApexCharts;
using FellowshipAnalyzer.Client;
using FellowshipAnalyzer.Client.Services;
using FellowshipAnalyzer.Components.Timeline;
using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Serialization;
using FellowshipAnalyzer.Heroes.Rime.Analysis;

#if STANDALONE_WASM
using Microsoft.AspNetCore.Components.Web;
#endif
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

#if STANDALONE_WASM
builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
#endif

var hostBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);

using var hostConfigurationClient = new HttpClient
{
    BaseAddress = hostBaseAddress
};

var clientConfiguration = await hostConfigurationClient.GetFromJsonAsync<ClientConfiguration>("config.json")
    ?? throw new InvalidOperationException("The FellowshipAnalyzer host did not provide client configuration.");

var apiBaseAddress = ResolveApiBaseAddress(hostBaseAddress, clientConfiguration.ApiBaseUrl);
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = apiBaseAddress });

// JSON options for deserializing API responses (including polymorphic events).
// Source-generated FellowshipAnalyzerJsonContext is inserted at the head of the resolver chain
// so registered types use precompiled metadata (critical for WASM perf — reflection-based JSON
// deserialization of ~30k polymorphic events takes 20+ seconds in interpreted WASM).
// Unregistered types fall through to the default reflection-based resolver.
var jsonOptions = new JsonSerializerOptions(JsonSerializerOptions.Web)
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    AllowOutOfOrderMetadataProperties = true,
};
jsonOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
jsonOptions.TypeInfoResolverChain.Insert(0, FellowshipAnalyzerJsonContext.Default);
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

// Per-hero Timeline customization (cooldown lanes / aura priorities), persisted in localStorage
builder.Services.AddScoped<TimelineConfigService>();

await builder.Build().RunAsync();

static Uri ResolveApiBaseAddress(Uri hostBaseAddress, string apiBaseUrl)
{
    if (string.IsNullOrWhiteSpace(apiBaseUrl))
    {
        throw new InvalidOperationException("Client configuration must provide an API base URL.");
    }

    var normalizedApiBaseUrl = apiBaseUrl.Trim();
    if (!normalizedApiBaseUrl.EndsWith('/'))
    {
        normalizedApiBaseUrl += "/";
    }

    return new Uri(hostBaseAddress, normalizedApiBaseUrl);
}

internal sealed record ClientConfiguration(string ApiBaseUrl);
