using System.Collections.Frozen;
using FellowshipAnalyzer.Core.Contracts.Design;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Combat role a hero fulfils in a Fellowship encounter.
/// </summary>
public enum HeroRole
{
    /// <summary>No role has been determined for the hero.</summary>
    Unknown = 0,
    /// <summary>The hero tanks, holding enemy aggro and mitigating incoming damage.</summary>
    Tank,
    /// <summary>The hero heals, sustaining the fellowship's health.</summary>
    Healer,
    /// <summary>The hero deals damage (DPS).</summary>
    Dps,
}

/// <summary>Identifies one of Fellowship's playable heroes, independent of the role it fills; combine with <see cref="HeroRole"/> via <see cref="Hero"/>.</summary>
public enum HeroName
{
    /// <summary>Aeona, one of Fellowship's healer heroes.</summary>
    Aeona,
    /// <summary>Ardeos, one of Fellowship's DPS heroes.</summary>
    Ardeos,
    /// <summary>Elarion, one of Fellowship's DPS heroes.</summary>
    Elarion,
    /// <summary>Gunde, one of Fellowship's DPS heroes.</summary>
    Gunde,
    /// <summary>Helena, one of Fellowship's tank heroes.</summary>
    Helena,
    /// <summary>Mara, one of Fellowship's DPS heroes.</summary>
    Mara,
    /// <summary>Meiko, one of Fellowship's tank heroes.</summary>
    Meiko,
    /// <summary>Rime, one of Fellowship's DPS heroes.</summary>
    Rime,
    /// <summary>Sylvie, one of Fellowship's healer heroes.</summary>
    Sylvie,
    /// <summary>Tariq, one of Fellowship's DPS heroes.</summary>
    Tariq,
    /// <summary>Vigour, one of Fellowship's healer heroes.</summary>
    Vigour,
    /// <summary>Xavian, one of Fellowship's tank heroes.</summary>
    Xavian,
}

/// <summary>Conversions between <see cref="HeroName"/> and the string identifiers Fellowship Logs uses.</summary>
public static class HeroNameExtensions
{
    /// <summary>Lowercase identifier Fellowship Logs uses for this hero, for example <c>"rime"</c>.</summary>
    public static string ToHeroId(this HeroName name) => name switch
    {
        HeroName.Aeona => "aeona",
        HeroName.Ardeos => "ardeos",
        HeroName.Elarion => "elarion",
        HeroName.Gunde => "gunde",
        HeroName.Helena => "helena",
        HeroName.Mara => "mara",
        HeroName.Meiko => "meiko",
        HeroName.Rime => "rime",
        HeroName.Sylvie => "sylvie",
        HeroName.Tariq => "tariq",
        HeroName.Vigour => "vigour",
        HeroName.Xavian => "xavian",
        _ => name.ToString().ToLowerInvariant(),
    };
}

/// <summary>A playable hero paired with the combat role it fills in a Fellowship encounter.</summary>
/// <param name="Name">Which hero this is.</param>
/// <param name="Role">The combat role this hero fills.</param>
public readonly record struct Hero(HeroName Name, HeroRole Role)
{
    /// <summary>Lowercase string identifier (e.g. <c>"rime"</c>).</summary>
    public string Id => Name.ToHeroId();

    /// <summary>Portrait image URL from the Fellowship CDN.</summary>
    public string IconUrl => Name switch
    {
        HeroName.Aeona   => "https://assets.fellows.gg/static/heroes/hero_portrait_Lisa_default.webp?v=1",
        HeroName.Ardeos  => "https://assets.fellows.gg/static/heroes/hero_portrait_Firemage_default.webp?v=1",
        HeroName.Elarion => "https://assets.fellows.gg/static/heroes/hero_portrait_Bowguy_01.webp?v=1",
        HeroName.Gunde   => "https://assets.fellows.gg/static/heroes/hero_potrtrait_Gunde_01.webp?v=1",
        HeroName.Helena  => "https://assets.fellows.gg/static/heroes/hero_portrait_warmaster_01.webp?v=1",
        HeroName.Mara    => "https://assets.fellows.gg/static/heroes/hero_portrait_Mara_01.webp?v=1",
        HeroName.Meiko   => "https://assets.fellows.gg/static/heroes/hero_portrait_meiko_default.webp?v=1",
        HeroName.Rime    => "https://assets.fellows.gg/static/heroes/hero_portrait_rime_default.webp?v=1",
        HeroName.Sylvie  => "https://assets.fellows.gg/static/heroes/hero_portrait_Mosse_01_default.webp?v=1",
        HeroName.Tariq   => "https://assets.fellows.gg/static/heroes/hero_portrait_Ink_01.webp?v=1",
        HeroName.Vigour  => "https://assets.fellows.gg/static/heroes/hero_portrait_vigor_default.webp?v=1",
        HeroName.Xavian  => "https://assets.fellows.gg/static/heroes/T_HeroPortrait_Sune.webp?v=1",
        _                => "",
    };

    /// <summary>CSS reference to the hero's identity token, for example <c>var(--fa-hero-rime)</c>.</summary>
    public string Color => Name switch
    {
        HeroName.Aeona   => FaVar.HeroAeona,
        HeroName.Ardeos  => FaVar.HeroArdeos,
        HeroName.Elarion => FaVar.HeroElarion,
        HeroName.Gunde   => FaVar.HeroGunde,
        HeroName.Helena  => FaVar.HeroHelena,
        HeroName.Mara    => FaVar.HeroMara,
        HeroName.Meiko   => FaVar.HeroMeiko,
        HeroName.Rime    => FaVar.HeroRime,
        HeroName.Sylvie  => FaVar.HeroSylvie,
        HeroName.Tariq   => FaVar.HeroTariq,
        HeroName.Vigour  => FaVar.HeroVigour,
        HeroName.Xavian  => FaVar.HeroXavian,
        _                => FaVar.HeroUnknown,
    };

    /// <summary>Helena, one of Fellowship's tank heroes.</summary>
    public static readonly Hero Helena = new(HeroName.Helena, HeroRole.Tank);
    /// <summary>Meiko, one of Fellowship's tank heroes.</summary>
    public static readonly Hero Meiko = new(HeroName.Meiko, HeroRole.Tank);
    /// <summary>Xavian, one of Fellowship's tank heroes.</summary>
    public static readonly Hero Xavian = new(HeroName.Xavian, HeroRole.Tank);

    /// <summary>Aeona, one of Fellowship's healer heroes.</summary>
    public static readonly Hero Aeona = new(HeroName.Aeona, HeroRole.Healer);
    /// <summary>Sylvie, one of Fellowship's healer heroes.</summary>
    public static readonly Hero Sylvie = new(HeroName.Sylvie, HeroRole.Healer);
    /// <summary>Vigour, one of Fellowship's healer heroes.</summary>
    public static readonly Hero Vigour = new(HeroName.Vigour, HeroRole.Healer);

    /// <summary>Gunde, one of Fellowship's DPS heroes.</summary>
    public static readonly Hero Gunde = new(HeroName.Gunde, HeroRole.Dps);
    /// <summary>Mara, one of Fellowship's DPS heroes.</summary>
    public static readonly Hero Mara = new(HeroName.Mara, HeroRole.Dps);
    /// <summary>Tariq, one of Fellowship's DPS heroes.</summary>
    public static readonly Hero Tariq = new(HeroName.Tariq, HeroRole.Dps);

    /// <summary>Ardeos, one of Fellowship's DPS heroes.</summary>
    public static readonly Hero Ardeos = new(HeroName.Ardeos, HeroRole.Dps);
    /// <summary>Elarion, one of Fellowship's DPS heroes.</summary>
    public static readonly Hero Elarion = new(HeroName.Elarion, HeroRole.Dps);
    /// <summary>Rime, one of Fellowship's DPS heroes.</summary>
    public static readonly Hero Rime = new(HeroName.Rime, HeroRole.Dps);


    /// <summary>All defined heroes, in <see cref="HeroName"/> order.</summary>
    public static IReadOnlyList<Hero> All { get; } =
    [
        Aeona, Ardeos, Elarion, Gunde, Helena, Mara, Meiko, Rime, Sylvie, Tariq, Vigour, Xavian,
    ];

    private static readonly FrozenDictionary<HeroName, Hero> ByName = All.ToDictionary(h => h.Name).ToFrozenDictionary();
    private static readonly FrozenDictionary<string, Hero> ById = All.ToDictionary(h => h.Id, StringComparer.OrdinalIgnoreCase).ToFrozenDictionary();

    /// <summary>All heroes grouped by the <see cref="HeroRole"/> they fill.</summary>
    public static FrozenDictionary<HeroRole, IReadOnlyList<Hero>> ByRole { get; } = All.GroupBy(h => h.Role).ToDictionary(g => g.Key, g => (IReadOnlyList<Hero>)[.. g]).ToFrozenDictionary();

    /// <summary>Looks up the canonical <see cref="Hero"/> for a <see cref="HeroName"/>.</summary>
    public static Hero For(HeroName name) => ByName[name];

    /// <summary>Attempts to resolve a <see cref="Hero"/> from its lowercase id (see <see cref="Id"/>), case-insensitively.</summary>
    /// <param name="id">The hero id to look up.</param>
    /// <param name="hero">The resolved hero, or <c>default</c> if <paramref name="id"/> does not match a known hero.</param>
    /// <returns><see langword="true"/> if <paramref name="id"/> matched a known hero.</returns>
    public static bool TryParse(string? id, out Hero hero)
    {
        if (id is not null && ById.TryGetValue(id.ToLowerInvariant(), out hero))
            return true;
        hero = default;
        return false;
    }

    /// <summary>Implicitly resolves the canonical <see cref="Hero"/> for a <see cref="HeroName"/>.</summary>
    public static implicit operator Hero(HeroName name) => For(name);
    /// <summary>Implicitly resolves a <see cref="Hero"/> from its id, throwing <see cref="ArgumentException"/> if <paramref name="name"/> is not recognized.</summary>
    public static implicit operator Hero(string name) => TryParse(name, out var hero) ? hero : throw new ArgumentException($"Invalid hero name: {name}", nameof(name));    
    /// <summary>Implicitly extracts the <see cref="HeroName"/> from a <see cref="Hero"/>.</summary>
    public static implicit operator HeroName(Hero hero) => hero.Name;
    /// <summary>Implicitly extracts the hero id (see <see cref="Id"/>) from a <see cref="Hero"/>.</summary>
    public static implicit operator string(Hero hero) => hero.Id;
}
