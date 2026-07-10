using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Gunde.Analysis;

public sealed partial class GundeCombatLogParser
{
    public static HeroConfig HeroConfig { get; } = new()
    {
        Support = SupportLevel.Partial,
        Maintainers = [Contributors.Seriousnes],
        SeasonLabel = Seasons.Current,
        Changelog = Changelog.Entries,
        ExampleReport = "a:QK8XBDqf6ZhFLWAy/55/5",
    };
}
