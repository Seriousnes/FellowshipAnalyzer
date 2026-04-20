using FellowshipAnalyzer.Core.Analysis;
using Microsoft.Extensions.DependencyInjection;

namespace FellowshipAnalyzer.Core;

public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers FellowshipAnalyzer.Core services shared across all hero analyzers.
    /// Call this once during application startup alongside hero-specific registrations.
    /// </summary>
    public static IServiceCollection AddCoreAnalysisServices(this IServiceCollection services)
    {
        services.AddSingleton<IPerformanceScoringService, PerformanceScoringService>();
        return services;
    }
}
