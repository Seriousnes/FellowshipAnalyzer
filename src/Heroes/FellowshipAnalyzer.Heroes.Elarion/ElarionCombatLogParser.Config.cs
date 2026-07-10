using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Elarion.Analysis;

public sealed partial class ElarionCombatLogParser
{
    public static HeroConfig HeroConfig { get; } = new()
    {
        Support = SupportLevel.Partial,
        Maintainers = [Contributors.Seriousnes],
        SeasonLabel = Seasons.Current,
        Changelog = Changelog.Entries,
        ExampleReport = "a:13Qj7ZaLRWryHhxB/20/91",
    };
}
