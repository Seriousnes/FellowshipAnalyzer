using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Xavian.Analysis;

public sealed partial class XavianCombatLogParser
{
    public static HeroConfig HeroConfig { get; } = new()
    {
        Support = SupportLevel.Minimal,
        Maintainers = [Contributors.Seriousnes],
        SeasonLabel = Seasons.Season3,
        Changelog = Changelog.Entries,
        ExampleReport = "2b9qHKQTcp14XVPt/10/5",
    };
}
