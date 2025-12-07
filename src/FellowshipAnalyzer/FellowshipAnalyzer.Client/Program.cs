using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using ApexCharts;
using FellowshipAnalyzer.Client.Services;
using FellowshipAnalyzer.Components.Timeline;
using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Serialization;

#if STANDALONE_WASM
using Microsoft.AspNetCore.Components.Web;
using FellowshipAnalyzer.Client;
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

builder.Services.AddScoped<ReportLoadingTracker>();
builder.Services.AddScoped<ReportNavigationState>();
builder.Services.AddScoped<IFellowshipLogsClient, FellowshipLogsProxyClient>();
builder.Services.AddScoped<IReportCacheService, IndexedDbReportCacheService>();
builder.Services.AddScoped<ReportAnalysisService>();
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
