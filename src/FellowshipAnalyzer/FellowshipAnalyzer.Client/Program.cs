using System.Text.Json;
using System.Text.Json.Serialization;
using FellowshipAnalyzer.Client.Services;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Serialization;
using FellowshipAnalyzer.Heroes.Rime.Analysis;

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

// JSON options for deserializing API responses (including polymorphic events)
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
jsonOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
jsonOptions.Converters.Add(new WCLJsonConverter<Event>());
builder.Services.AddSingleton(jsonOptions);

// Hero analysis runs client-side in WASM
builder.Services.AddRimeAnalysis();

// Report history + event cache (IndexedDB-backed)
builder.Services.AddScoped<IReportCacheService, IndexedDbReportCacheService>();

await builder.Build().RunAsync();
