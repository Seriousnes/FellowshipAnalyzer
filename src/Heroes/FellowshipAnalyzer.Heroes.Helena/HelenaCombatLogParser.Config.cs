using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Helena.Analysis;

public sealed partial class HelenaCombatLogParser
{
    public static HeroConfig HeroConfig { get; } = new()
    {
        Support = SupportLevel.Minimal,
        Maintainers = [Contributors.Seriousnes],
        SeasonLabel = Seasons.Season3,
        Changelog = Changelog.Entries,
        ExampleReport = "a:V2XvpaMWxLTnjDAZ/43/3",
    };
}
