using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.FellowshipLogs;
using Microsoft.Extensions.DependencyInjection;

namespace FellowshipAnalyzer.Core;

/// <summary>Dependency injection extension methods for registering FellowshipAnalyzer.Core services.</summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers FellowshipAnalyzer.Core services shared across all hero analyzers.
    /// Call this once during application startup alongside hero-specific registrations.
    /// </summary>
    public static IServiceCollection AddCoreAnalysisServices(this IServiceCollection services)
    {
        services.AddSingleton<IPerformanceScoringService, PerformanceScoringService>();
        services.AddScoped<ReportMasterDataService>();
        return services;
    }
}
