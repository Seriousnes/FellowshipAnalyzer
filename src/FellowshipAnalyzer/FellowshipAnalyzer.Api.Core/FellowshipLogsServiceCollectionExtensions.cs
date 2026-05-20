using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Api.Core.Caching;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.IO;

namespace FellowshipAnalyzer.Api.Core;

public static class FellowshipLogsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the FellowshipLogs API services: GraphQL client, OAuth token cache, in-memory cache,
    /// rate limiter, CORS options, and the framework-agnostic <see cref="FellowshipLogsApiHandler"/>.
    /// Each HTTP host (Functions, Kestrel) consumes <see cref="FellowshipLogsApiHandler"/> directly.
    /// </summary>
    /// <param name="allowDevelopmentLoopbackOrigins">
    /// When true, CORS will accept any loopback (localhost / 127.x.x.x / *.localhost) origin.
    /// Hosts should typically pass <c>builder.Environment.IsDevelopment()</c>.
    /// </param>
    public static IServiceCollection AddFellowshipLogsApi(
        this IServiceCollection services,
        IConfiguration configuration,
        bool allowDevelopmentLoopbackOrigins)
    {
        var fellowshipLogsOptions = new FellowshipLogsClientOptions
        {
            ClientId = configuration["FellowshipLogs:ClientId"] ?? configuration["ClientId"] ?? string.Empty,
            ClientSecret = configuration["FellowshipLogs:ClientSecret"] ?? configuration["ClientSecret"] ?? string.Empty,
            TokenEndpoint = configuration["FellowshipLogs:TokenEndpoint"] ?? FellowshipLogsClientOptions.DefaultTokenEndpoint,
            GraphQlEndpoint = configuration["FellowshipLogs:GraphQlEndpoint"] ?? FellowshipLogsClientOptions.DefaultGraphQlEndpoint
        };
        services.AddSingleton(fellowshipLogsOptions);

        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
        jsonOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
        services.AddSingleton(jsonOptions);

        services.AddHttpClient("FellowshipLogsAuth");
        services.AddSingleton(sp =>
            new ClientCredentialsTokenCache(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("FellowshipLogsAuth"),
                sp.GetRequiredService<FellowshipLogsClientOptions>()));
        services.AddTransient<BearerTokenHandler>();
        services.AddFellowshipLogsApiClient()
            .ConfigureHttpClient(
                client => client.BaseAddress = new Uri(fellowshipLogsOptions.GraphQlEndpoint),
                httpBuilder => httpBuilder.AddHttpMessageHandler<BearerTokenHandler>());

        services.AddScoped<FellowshipLogsService>();
        services.AddSingleton<GraphQLMapper>();
        services.AddMemoryCache();

        services.AddSingleton(
            configuration
                .GetSection(FellowshipLogsCacheOptions.SectionName)
                .Get<FellowshipLogsCacheOptions>()
            ?? new FellowshipLogsCacheOptions());

        services.AddSingleton(
            configuration
                .GetSection(FellowshipLogsRateLimitOptions.SectionName)
                .Get<FellowshipLogsRateLimitOptions>()
            ?? new FellowshipLogsRateLimitOptions());

        var corsOptions = new FellowshipLogsCorsOptions(
            configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>()
                ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            ?? [],
            allowDevelopmentLoopbackOrigins);
        services.AddSingleton(corsOptions);

        services.AddCors(options =>
        {
            options.AddPolicy(FellowshipLogsApiBuilderExtensions.CorsPolicyName, policy =>
            {
                policy.SetIsOriginAllowed(corsOptions.IsAllowedOrigin)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders(
                        "X-FellowshipAnalyzer-Cache",
                        "X-FellowshipAnalyzer-ExpiresAt");
            });
        });

        services.AddSingleton(new RecyclableMemoryStreamManager());
        services.AddSingleton<FellowshipLogsRateLimiter>();
        services.AddScoped<FellowshipLogsApiHandler>();
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IStartupFilter, FellowshipLogsApiStartupFilter>();

        return services;
    }

    /// <summary>
    /// Registers <see cref="BlobPersistentCache"/> as the <see cref="IPersistentCache"/>
    /// implementation. Call this from each host after calling
    /// <see cref="AddFellowshipLogsApi"/>; the core registration deliberately leaves
    /// <see cref="IPersistentCache"/> unregistered so each host can choose its storage backend.
    /// </summary>
    public static IServiceCollection AddBlobPersistentCache(
        this IServiceCollection services,
        Action<BlobPersistentCacheOptions>? configure = null)
    {
        var options = new BlobPersistentCacheOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<IPersistentCache, BlobPersistentCache>();
        return services;
    }
}
