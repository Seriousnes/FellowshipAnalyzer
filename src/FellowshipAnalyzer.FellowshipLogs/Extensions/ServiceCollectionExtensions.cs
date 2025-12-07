using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Serialization;
using FellowshipAnalyzer.FellowshipLogs.API;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FellowshipAnalyzer.FellowshipLogs;

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
        services.AddScoped<IApiRequestExecutor>(sp =>
            new ApiRequestExecutor(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("FellowshipLogs"),
                sp.GetRequiredService<JsonSerializerOptions>()));
        services.AddScoped<IFellowshipLogsClient>(sp =>
            new ApiClient(
                sp.GetRequiredService<IApiRequestExecutor>(),
                sp.GetRequiredService<FellowshipLogsClientOptions>()));

        return services;
    }

    public static JsonSerializerOptions CreateJsonSerializerOptions()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerOptions.Default)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
        jsonOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
        jsonOptions.Converters.Add(new WCLJsonConverter<Event>());
        return jsonOptions;
    }
}
