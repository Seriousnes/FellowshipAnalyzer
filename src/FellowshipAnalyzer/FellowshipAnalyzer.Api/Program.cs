using FellowshipAnalyzer.Api.Core;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddApiDefaults();
builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService(options => options.EnableAdaptiveSampling = false)
    .ConfigureFunctionsApplicationInsights();

builder.Services.Configure<LoggerFilterOptions>(options =>
{
    var applicationInsightsRule = options.Rules.FirstOrDefault(rule =>
        rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");

    if (applicationInsightsRule is not null)
    {
        options.Rules.Remove(applicationInsightsRule);
    }
});

builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddFilter("FellowshipAnalyzer.Api.Core", LogLevel.Information);

builder.AddAzureBlobServiceClient("BlobsConnection");

builder.Services.AddFellowshipLogsApi(
    builder.Configuration,
    allowDevelopmentLoopbackOrigins: builder.Environment.IsDevelopment());

builder.Services.AddBlobPersistentCache();

var app = builder.Build();
app.Run();
