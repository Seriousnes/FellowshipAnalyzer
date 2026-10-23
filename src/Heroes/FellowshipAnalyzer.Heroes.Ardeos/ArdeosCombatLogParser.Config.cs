using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Ardeos.Analysis;

public sealed partial class ArdeosCombatLogParser
{
    public static HeroConfig HeroConfig { get; } = new()
    {
        Support = SupportLevel.Minimal,
        Maintainers = [Contributors.Seriousnes],
        SeasonLabel = Seasons.Season3,
        Changelog = Changelog.Entries,
        ExampleReport = "a:dXGv74VgMy9T1xLD/53/145",
    };
}
