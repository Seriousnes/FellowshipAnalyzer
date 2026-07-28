using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Sylvie.Analysis;

public sealed partial class SylvieCombatLogParser
{
    public static HeroConfig HeroConfig { get; } = new()
    {
        Support = SupportLevel.WIP,
        Maintainers = [Contributors.Seriousnes],
        SeasonLabel = Seasons.Season3,
        Changelog = Changelog.Entries,
        ExampleReport = "a:gDf7m3N2wvk96dWP/22/118",
    };
}
