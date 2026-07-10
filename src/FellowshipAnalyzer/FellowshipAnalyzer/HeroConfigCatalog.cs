using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer;

/// <summary>
/// Binds the compile-time <see cref="HeroManifest"/> to <see cref="IHeroConfigCatalog"/> so Core
/// UI can read every hero's config without instantiating a parser.
/// </summary>
public sealed class HeroConfigCatalog : IHeroConfigCatalog
{
    public IReadOnlyList<HeroConfigEntry> Heroes { get; } =
        [.. HeroManifest.Entries.Select(e => new HeroConfigEntry(e.Hero, e.Config))];
}
