using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.FellowshipLogs.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FellowshipAnalyzer.FellowshipLogs.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFellowshipLogsService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new FellowshipLogsClientOptions
        {
            ClientId = configuration["FellowshipLogs:ClientId"] ?? configuration["ClientId"] ?? string.Empty,
            ClientSecret = configuration["FellowshipLogs:ClientSecret"] ?? configuration["ClientSecret"] ?? string.Empty,
            TokenEndpoint = configuration["FellowshipLogs:TokenEndpoint"] ?? FellowshipLogsClientOptions.DefaultTokenEndpoint,
            GraphQlEndpoint = configuration["FellowshipLogs:GraphQlEndpoint"] ?? FellowshipLogsClientOptions.DefaultGraphQlEndpoint
        };

        services.AddSingleton(options);
        services.AddSingleton(CreateJsonSerializerOptions());
        services.AddHttpClient("FellowshipLogs")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip,
            });
        services.AddHttpClient("FellowshipLogsProxy")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.None,
            });
        services.AddScoped<IApiRequestExecutor>(sp =>
            new ApiRequestExecutor(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("FellowshipLogs"),
                sp.GetRequiredService<JsonSerializerOptions>()));
        services.AddScoped<ApiClient>(sp =>
            new ApiClient(
                sp.GetRequiredService<IApiRequestExecutor>(),
                sp.GetRequiredService<FellowshipLogsClientOptions>(),
                sp.GetRequiredService<IHttpClientFactory>()));
        services.AddScoped<IFellowshipLogsClient>(sp => sp.GetRequiredService<ApiClient>());
        services.AddScoped<IFellowshipLogsProxy>(sp => sp.GetRequiredService<ApiClient>());

        return services;
    }

    public static JsonSerializerOptions CreateJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerOptions.Default)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowOutOfOrderMetadataProperties = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
        return options;
    }
}
