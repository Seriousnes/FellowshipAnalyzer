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

        services.AddHttpClient("FellowshipLogsAuth");

        services.AddSingleton<ClientCredentialsTokenCache>(sp =>
            new ClientCredentialsTokenCache(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("FellowshipLogsAuth"),
                sp.GetRequiredService<FellowshipLogsClientOptions>()));

        services.AddTransient<BearerTokenHandler>();

        services.AddFellowshipLogsApiClient()
            .ConfigureHttpClient(
                client => client.BaseAddress = new Uri(options.GraphQlEndpoint),
                builder => builder.AddHttpMessageHandler<BearerTokenHandler>());

        services.AddScoped<IFellowshipLogsClient>(sp =>
            new FellowshipLogsGraphQLWrapper(
                sp.GetRequiredService<IFellowshipLogsApiClient>(),
                sp.GetRequiredService<JsonSerializerOptions>()));

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
