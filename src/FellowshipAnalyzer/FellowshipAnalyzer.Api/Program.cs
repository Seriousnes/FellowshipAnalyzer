using FellowshipAnalyzer.Api;
using FellowshipAnalyzer.FellowshipLogs.Extensions;
using FellowshipAnalyzer.ServiceDefaults;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.ConfigureFunctionsWebApplication();

builder.Services.AddFellowshipLogsService(builder.Configuration);
builder.Services.AddMemoryCache();

builder.Services.AddSingleton(
    builder.Configuration
        .GetSection(FellowshipLogsProxyCacheOptions.SectionName)
        .Get<FellowshipLogsProxyCacheOptions>()
    ?? new FellowshipLogsProxyCacheOptions());

builder.Services.AddSingleton(
    builder.Configuration
        .GetSection(FellowshipLogsProxyRateLimitOptions.SectionName)
        .Get<FellowshipLogsProxyRateLimitOptions>()
    ?? new FellowshipLogsProxyRateLimitOptions());

builder.Services.AddSingleton(new FellowshipLogsProxyCorsOptions(
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
        ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray()
    ?? [],
    builder.Environment.IsDevelopment()));

builder.Services.AddSingleton<FellowshipLogsProxyRateLimiter>();

builder.Build().Run();
