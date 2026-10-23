using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Mara.Analysis;

public sealed partial class MaraCombatLogParser
{
    public static HeroConfig HeroConfig { get; } = new()
    {
        Support = SupportLevel.Minimal,
        Maintainers = [Contributors.Seriousnes],
        SeasonLabel = Seasons.Season3,
        Changelog = Changelog.Entries,
        ExampleReport = "a:V2XvpaMWxLTnjDAZ/43/5",
    };
}
