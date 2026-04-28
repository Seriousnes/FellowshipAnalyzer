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

/// <summary>
/// Canonical, compile-time identifier for every supported hero.
/// Used as the argument to <see cref="HeroAnalyzerAttribute"/> (attribute arguments must be constants).
/// The lowercase string form of the enum name is the hero's stable identifier
/// (DI key, log <c>SubType</c>, cache key, etc.) — see <see cref="HeroNameExtensions.ToHeroId"/>.
/// </summary>
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

/// <summary>
/// Helpers for converting between <see cref="HeroName"/> and its lowercase string form.
/// </summary>
public static class HeroNameExtensions
{
    /// <summary>Lowercase string identifier (e.g. <c>HeroName.Rime → "rime"</c>).</summary>
    public static string ToHeroId(this HeroName name) => name switch
    {
        HeroName.Aeona   => "aeona",
        HeroName.Ardeos  => "ardeos",
        HeroName.Elarion => "elarion",
        HeroName.Helena  => "helena",
        HeroName.Mara    => "mara",
        HeroName.Meiko   => "meiko",
        HeroName.Rime    => "rime",
        HeroName.Sylvie  => "sylvie",
        HeroName.Tariq   => "tariq",
        HeroName.Vigour  => "vigour",
        HeroName.Xavian  => "xavian",
        _ => name.ToString().ToLowerInvariant(),
    };
}

/// <summary>
/// Definition of a hero — its canonical <see cref="HeroName"/> and combat <see cref="HeroRole"/>.
/// All hero definitions are declared once as static fields on this type
/// (<c>Hero.Rime</c>, <c>Hero.Aeona</c>, …) and enumerated via <see cref="All"/>.
/// </summary>
/// <param name="Name">Canonical hero identifier.</param>
/// <param name="Role">Combat role.</param>
public readonly record struct Hero(HeroName Name, HeroRole Role)
{
    /// <summary>Lowercase string identifier (e.g. <c>"rime"</c>).</summary>
    public string Id => Name.ToHeroId();

    public static readonly Hero Aeona   = new(HeroName.Aeona,   HeroRole.Unknown);
    public static readonly Hero Ardeos  = new(HeroName.Ardeos,  HeroRole.Unknown);
    public static readonly Hero Elarion = new(HeroName.Elarion, HeroRole.Unknown);
    public static readonly Hero Helena  = new(HeroName.Helena,  HeroRole.Unknown);
    public static readonly Hero Mara    = new(HeroName.Mara,    HeroRole.Unknown);
    public static readonly Hero Meiko   = new(HeroName.Meiko,   HeroRole.Unknown);
    public static readonly Hero Rime    = new(HeroName.Rime,    HeroRole.Dps);
    public static readonly Hero Sylvie  = new(HeroName.Sylvie,  HeroRole.Unknown);
    public static readonly Hero Tariq   = new(HeroName.Tariq,   HeroRole.Unknown);
    public static readonly Hero Vigour  = new(HeroName.Vigour,  HeroRole.Unknown);
    public static readonly Hero Xavian  = new(HeroName.Xavian,  HeroRole.Unknown);

    /// <summary>All defined heroes, in <see cref="HeroName"/> order.</summary>
    public static IReadOnlyList<Hero> All { get; } =
    [
        Aeona, Ardeos, Elarion, Helena, Mara, Meiko, Rime, Sylvie, Tariq, Vigour, Xavian,
    ];

    private static readonly Dictionary<HeroName, Hero> ByName = All.ToDictionary(h => h.Name);
    private static readonly Dictionary<string, Hero> ById =
        All.ToDictionary(h => h.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>Look up the hero definition for a given <see cref="HeroName"/>.</summary>
    public static Hero For(HeroName name) => ByName[name];

    /// <summary>
    /// Try to parse a hero's lowercase string identifier (case-insensitive)
    /// — typically a log <c>SubType</c> or DI key — into a <see cref="Hero"/>.
    /// </summary>
    public static bool TryParse(string? id, out Hero hero)
    {
        if (id is not null && ById.TryGetValue(id, out hero))
            return true;
        hero = default;
        return false;
    }

    /// <summary>Implicit conversion to <see cref="HeroName"/>.</summary>
    public static implicit operator HeroName(Hero hero) => hero.Name;

    /// <summary>Implicit conversion to the lowercase string identifier.</summary>
    public static implicit operator string(Hero hero) => hero.Id;
}
