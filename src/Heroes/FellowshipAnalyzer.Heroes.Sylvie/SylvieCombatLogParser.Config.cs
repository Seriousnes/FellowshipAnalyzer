using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Sylvie.Analysis;

public sealed partial class SylvieCombatLogParser
{
    public static HeroConfig HeroConfig { get; } = new()
    {
        Support = SupportLevel.Minimal,
        Maintainers = [Contributors.Seriousnes],
        SeasonLabel = Seasons.Season3,
        Changelog = Changelog.Entries,
        ExampleReport = "a:mhNX1WMfB2pCvyY6/39/5",
    };
}
