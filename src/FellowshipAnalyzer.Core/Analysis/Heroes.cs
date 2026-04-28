using System.Collections.Frozen;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Combat role a hero fulfils in a Fellowship encounter.
/// </summary>
public enum HeroRole
{
    Unknown = 0,
    Tank,
    Healer,
    Dps,
}

public enum HeroName
{
    Aeona,
    Ardeos,
    Elarion,
    Helena,
    Mara,
    Meiko,
    Rime,
    Sylvie,
    Tariq,
    Vigour,
    Xavian,
}

public static class HeroNameExtensions
{
    public static string ToHeroId(this HeroName name) => name switch
    {
        HeroName.Aeona => "aeona",
        HeroName.Ardeos => "ardeos",
        HeroName.Elarion => "elarion",
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

public readonly record struct Hero(HeroName Name, HeroRole Role)
{
    /// <summary>Lowercase string identifier (e.g. <c>"rime"</c>).</summary>
    public string Id => Name.ToHeroId();

    /* Tanks */
    public static readonly Hero Helena = new(HeroName.Helena, HeroRole.Tank);
    public static readonly Hero Meiko = new(HeroName.Meiko, HeroRole.Tank);
    public static readonly Hero Xavian = new(HeroName.Xavian, HeroRole.Tank);

    /* Healers */
    public static readonly Hero Aeona = new(HeroName.Aeona, HeroRole.Healer);
    public static readonly Hero Sylvie = new(HeroName.Sylvie, HeroRole.Healer);
    public static readonly Hero Vigour = new(HeroName.Vigour, HeroRole.Healer);

    /* Melee DPS */
    public static readonly Hero Mara = new(HeroName.Mara, HeroRole.Dps);
    public static readonly Hero Tariq = new(HeroName.Tariq, HeroRole.Dps);

    /* Ranged DPS */
    public static readonly Hero Ardeos = new(HeroName.Ardeos, HeroRole.Dps);
    public static readonly Hero Elarion = new(HeroName.Elarion, HeroRole.Dps);
    public static readonly Hero Rime = new(HeroName.Rime, HeroRole.Dps);


    /// <summary>All defined heroes, in <see cref="HeroName"/> order.</summary>
    public static IReadOnlyList<Hero> All { get; } =
    [
        Aeona, Ardeos, Elarion, Helena, Mara, Meiko, Rime, Sylvie, Tariq, Vigour, Xavian,
    ];

    private static readonly FrozenDictionary<HeroName, Hero> ByName = All.ToDictionary(h => h.Name).ToFrozenDictionary();
    private static readonly FrozenDictionary<string, Hero> ById = All.ToDictionary(h => h.Id, StringComparer.OrdinalIgnoreCase).ToFrozenDictionary();

    public static FrozenDictionary<HeroRole, IReadOnlyList<Hero>> ByRole { get; } = All.GroupBy(h => h.Role).ToDictionary(g => g.Key, g => (IReadOnlyList<Hero>)[.. g]).ToFrozenDictionary();

    public static Hero For(HeroName name) => ByName[name];

    public static bool TryParse(string? id, out Hero hero)
    {
        if (id is not null && ById.TryGetValue(id.ToLowerInvariant(), out hero))
            return true;
        hero = default;
        return false;
    }

    public static implicit operator Hero(HeroName name) => For(name);
    public static implicit operator Hero(string name) => TryParse(name, out var hero) ? hero : throw new ArgumentException($"Invalid hero name: {name}", nameof(name));    
    public static implicit operator HeroName(Hero hero) => hero.Name;
    public static implicit operator string(Hero hero) => hero.Id;
}
