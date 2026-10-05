using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Vigour.Analysis;

public sealed partial class VigourCombatLogParser
{
    public static HeroConfig HeroConfig { get; } = new()
    {
        Support = SupportLevel.Minimal,
        Maintainers = [Contributors.Seriousnes],
        SeasonLabel = Seasons.Season3,
        Changelog = Changelog.Entries,
        ExampleReport = "MY91KkT7NwrFfmWt/5/4",
    };
}
