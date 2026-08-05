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
        ExampleReport = "a:RaMDvgzWXBCnF4QT/16/25",
    };
}
