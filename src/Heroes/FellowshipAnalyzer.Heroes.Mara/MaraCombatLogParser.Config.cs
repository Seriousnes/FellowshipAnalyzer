using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Mara.Analysis;

public sealed partial class MaraCombatLogParser
{
    public static HeroConfig HeroConfig { get; } = new()
    {
        Support = SupportLevel.Partial,
        Maintainers = [Contributors.Seriousnes],
        SeasonLabel = Seasons.Current,
        Changelog = Changelog.Entries,
        ExampleReport = "a:6g3jkMGFqrZn1wmW/408/4",
    };
}
