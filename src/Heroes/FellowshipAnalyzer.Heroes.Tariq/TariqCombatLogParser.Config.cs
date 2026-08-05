using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Tariq.Analysis;

public sealed partial class TariqCombatLogParser
{
    public static HeroConfig HeroConfig { get; } = new()
    {
        Support = SupportLevel.Minimal,
        Maintainers = [Contributors.Seriousnes],
        SeasonLabel = Seasons.Season3,
        Changelog = Changelog.Entries,
        ExampleReport = "a:NcqHDKzamL7n6YFv/18/5",
    };
}
