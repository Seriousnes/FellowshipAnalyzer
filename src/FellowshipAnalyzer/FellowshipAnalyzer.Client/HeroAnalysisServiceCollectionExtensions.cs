using FellowshipAnalyzer.Heroes.Aeona.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;
using FellowshipAnalyzer.Heroes.Elarion.Analysis;
using FellowshipAnalyzer.Heroes.Helena.Analysis;
using FellowshipAnalyzer.Heroes.Mara.Analysis;
using FellowshipAnalyzer.Heroes.Meiko.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Analysis;
using FellowshipAnalyzer.Heroes.Sylvie.Analysis;
using FellowshipAnalyzer.Heroes.Tariq.Analysis;
using FellowshipAnalyzer.Heroes.Vigour.Analysis;
using FellowshipAnalyzer.Heroes.Xavian.Analysis;

namespace Microsoft.Extensions.DependencyInjection;

public static class HeroAnalysisServiceCollectionExtensions
{
    public static IServiceCollection AddFellowshipHeroAnalysis(this IServiceCollection services)
    {
        services.AddAeonaAnalysis();
        services.AddArdeosAnalysis();
        services.AddElarionAnalysis();
        services.AddHelenaAnalysis();
        services.AddMaraAnalysis();
        services.AddMeikoAnalysis();
        services.AddRimeAnalysis();
        services.AddSylvieAnalysis();
        services.AddTariqAnalysis();
        services.AddVigourAnalysis();
        services.AddXavianAnalysis();

        return services;
    }
}
