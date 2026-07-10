using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Ardeos.Analysis;

public sealed partial class ArdeosCombatLogParser
{
    public static HeroConfig HeroConfig { get; } = new()
    {
        Support = SupportLevel.Partial,
        Maintainers = [Contributors.Seriousnes],
        SeasonLabel = Seasons.Current,
        Changelog = Changelog.Entries,
        ExampleReport = "a:RaMDvgzWXBCnF4QT/16/25",
    };
}
