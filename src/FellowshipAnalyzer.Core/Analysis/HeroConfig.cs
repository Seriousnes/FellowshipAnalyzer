namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Declarative metadata for a hero: how well-supported it is, who maintains it, which season its
/// analysis targets, an example report, and its changelog. Declared as a public static
/// <c>HeroConfig</c> member on the hero's parser and collected at compile time into the
/// <c>HeroManifest</c>; heroes that declare none resolve to <see cref="Unmaintained"/>.
/// </summary>
public sealed record HeroConfig
{
    /// <summary>How complete the hero's analysis is.</summary>
    public SupportLevel Support { get; init; } = SupportLevel.Unmaintained;

    /// <summary>People responsible for the hero's analysis.</summary>
    public IReadOnlyList<Contributor> Maintainers { get; init; } = [];

    /// <summary>Season the analysis is validated against, e.g. <c>"Season 1"</c>.</summary>
    public string? SeasonLabel { get; init; }

    /// <summary>An example report path (<c>code/fightId/playerId</c>) demonstrating the hero.</summary>
    public string? ExampleReport { get; init; }

    /// <summary>The hero's changelog, most-recent-first when rendered.</summary>
    public IReadOnlyList<ChangelogEntry> Changelog { get; init; } = [];

    /// <summary>The config a hero resolves to when it declares none.</summary>
    public static HeroConfig Unmaintained { get; } = new();
}
