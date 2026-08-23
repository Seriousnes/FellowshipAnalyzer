namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>A hero paired with its declared <see cref="HeroConfig"/>.</summary>
public readonly record struct HeroConfigEntry(HeroName Hero, HeroConfig Config);

/// <summary>
/// Provides every hero's <see cref="HeroConfig"/>. Implemented over the compile-time
/// <c>HeroManifest</c> by the host application, so consumers never instantiate a parser.
/// </summary>
public interface IHeroConfigCatalog
{
    /// <summary>Every hero and its config, in <see cref="HeroName"/> order.</summary>
    List<HeroConfigEntry> Heroes { get; }
}
