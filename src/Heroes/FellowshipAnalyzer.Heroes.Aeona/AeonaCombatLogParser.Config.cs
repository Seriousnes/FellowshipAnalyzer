using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Aeona.Analysis;

public sealed partial class AeonaCombatLogParser
{
    public static HeroConfig HeroConfig { get; } = new()
    {
        Support = SupportLevel.Minimal,
        Maintainers = [Contributors.Seriousnes],
        SeasonLabel = Seasons.Season3,
        Changelog = Changelog.Entries,
        ExampleReport = "a:6g3jkMGFqrZn1wmW/417/310",
    };
}
