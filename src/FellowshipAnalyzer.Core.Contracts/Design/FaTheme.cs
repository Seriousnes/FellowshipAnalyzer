namespace FellowshipAnalyzer.Core.Contracts.Design;

/// <summary>
/// A complete theme. Every one of the design system's tokens resolves in every theme, so no call
/// site needs a fallback and no token resolves to nothing in any app.
/// </summary>
/// <param name="Name">The theme's name, which is also its <c>data-theme</c> attribute value in lower case.</param>
/// <param name="Palette">Every colour token, plus the two section-header gradients.</param>
/// <param name="Typography">The font stacks and the type scale.</param>
/// <param name="Metrics">The edge widths and corner radii.</param>
/// <param name="Elevation">The drop shadows.</param>
public sealed record FaTheme(
    string Name,
    FaPalette Palette,
    FaTypography Typography,
    FaMetrics Metrics,
    FaElevation Elevation)
{
    /// <summary>The game-sampled theme the application ships with.</summary>
    public static FaTheme Original { get; } = new(
        "Original",
        FaPalette.Original,
        FaTypography.Default,
        FaMetrics.Default,
        FaElevation.Original);

    /// <summary>The steel-and-gold dark theme.</summary>
    public static FaTheme Dark { get; } = new(
        "Dark",
        FaPalette.Dark,
        FaTypography.Default,
        FaMetrics.Default,
        FaElevation.Dark);

    /// <summary>The cream light theme.</summary>
    public static FaTheme Light { get; } = new(
        "Light",
        FaPalette.Light,
        FaTypography.Default,
        FaMetrics.Default,
        FaElevation.Light);

    /// <summary>Every theme, in the order the generated stylesheet declares them.</summary>
    public static IReadOnlyList<FaTheme> All { get; } = [Original, Dark, Light];

    /// <summary>The <c>data-theme</c> attribute value that selects this theme.</summary>
    public string Selector => Name.ToLowerInvariant();

    /// <summary>
    /// Every token this theme defines, in the order the design system documents them, flattened out
    /// of <see cref="Groups"/>.
    /// </summary>
    public IEnumerable<FaToken> Tokens
    {
        get
        {
            foreach (var group in Groups)
            {
                foreach (var token in group.Tokens)
                {
                    yield return token;
                }
            }
        }
    }

    /// <summary>
    /// Every token this theme defines, under the group the design system documents it in. The list
    /// is written out member by member so nothing on this path reflects.
    /// </summary>
    public IEnumerable<FaTokenGroup> Groups
    {
        get
        {
            var p = Palette;

            yield return Group("Base palette", nameof(FaPalette),
                Token(nameof(FaPalette.White), p.White),
                Token(nameof(FaPalette.Black), p.Black),
                Token(nameof(FaPalette.Grey), p.Grey),
                Token(nameof(FaPalette.Amber), p.Amber),
                Token(nameof(FaPalette.Steel), p.Steel),
                Token(nameof(FaPalette.Arcane), p.Arcane),
                Token(nameof(FaPalette.Nature), p.Nature),
                Token(nameof(FaPalette.Link), p.Link),
                Token(nameof(FaPalette.Frame), p.Frame),
                Token(nameof(FaPalette.Blood), p.Blood),
                Token(nameof(FaPalette.Rust), p.Rust),
                Token(nameof(FaPalette.Stone), p.Stone),
                Token(nameof(FaPalette.Ash), p.Ash));

            yield return Group("Brand artwork", nameof(FaPalette),
                Token(nameof(FaPalette.BrandCream), p.BrandCream),
                Token(nameof(FaPalette.BrandGold), p.BrandGold));

            yield return Group("Surfaces", nameof(FaPalette),
                Token(nameof(FaPalette.BgBase), p.BgBase),
                Token(nameof(FaPalette.BgRaised), p.BgRaised),
                Token(nameof(FaPalette.BgSurface), p.BgSurface),
                Token(nameof(FaPalette.BgCard), p.BgCard),
                Token(nameof(FaPalette.BgInset), p.BgInset),
                Token(nameof(FaPalette.BgHover), p.BgHover),
                Token(nameof(FaPalette.Raise), p.Raise),
                Token(nameof(FaPalette.Recess), p.Recess),
                Token(nameof(FaPalette.RecessDeep), p.RecessDeep),
                Token(nameof(FaPalette.SectionHeader), p.SectionHeader),
                Token(nameof(FaPalette.SectionHeaderHover), p.SectionHeaderHover));

            yield return Group("Foreground", nameof(FaPalette),
                Token(nameof(FaPalette.Text), p.Text),
                Token(nameof(FaPalette.TextMuted), p.TextMuted),
                Token(nameof(FaPalette.TextDim), p.TextDim),
                Token(nameof(FaPalette.Fg), p.Fg),
                Token(nameof(FaPalette.FgMuted), p.FgMuted),
                Token(nameof(FaPalette.FgDim), p.FgDim),
                Token(nameof(FaPalette.FgNeutral), p.FgNeutral));

            yield return Group("Accents and intent", nameof(FaPalette),
                Token(nameof(FaPalette.Gold), p.Gold),
                Token(nameof(FaPalette.GoldLight), p.GoldLight),
                Token(nameof(FaPalette.GoldPale), p.GoldPale),
                Token(nameof(FaPalette.GoldDim), p.GoldDim),
                Token(nameof(FaPalette.Ice), p.Ice),
                Token(nameof(FaPalette.Danger), p.Danger));

            yield return Group("Structure and edges", nameof(FaPalette),
                Token(nameof(FaPalette.Border), p.Border),
                Token(nameof(FaPalette.BorderCard), p.BorderCard),
                Token(nameof(FaPalette.BorderSubtle), p.BorderSubtle),
                Token(nameof(FaPalette.Edge), p.Edge),
                Token(nameof(FaPalette.PanelBorder), p.PanelBorder));

            yield return Group("Performance tiers", nameof(FaPalette),
                Token(nameof(FaPalette.PerfPerfect), p.PerfPerfect),
                Token(nameof(FaPalette.PerfGood), p.PerfGood),
                Token(nameof(FaPalette.PerfOk), p.PerfOk),
                Token(nameof(FaPalette.PerfFail), p.PerfFail));

            yield return Group("Fire ramp", nameof(FaPalette),
                Token(nameof(FaPalette.Fire1), p.Fire1),
                Token(nameof(FaPalette.Fire2), p.Fire2),
                Token(nameof(FaPalette.Fire3), p.Fire3),
                Token(nameof(FaPalette.Fire4), p.Fire4),
                Token(nameof(FaPalette.Fire5), p.Fire5),
                Token(nameof(FaPalette.Fire6), p.Fire6));

            yield return Group("Item qualities", nameof(FaPalette),
                Token(nameof(FaPalette.Legendary), p.Legendary),
                Token(nameof(FaPalette.Regal), p.Regal),
                Token(nameof(FaPalette.Epic), p.Epic));

            yield return Group("Hero roles", nameof(FaPalette),
                Token(nameof(FaPalette.RoleTank), p.RoleTank),
                Token(nameof(FaPalette.RoleHealer), p.RoleHealer),
                Token(nameof(FaPalette.RoleDps), p.RoleDps),
                Token(nameof(FaPalette.RoleUnknown), p.RoleUnknown));

            yield return Group("Hero identity", nameof(FaPalette),
                Token(nameof(FaPalette.HeroAeona), p.HeroAeona),
                Token(nameof(FaPalette.HeroArdeos), p.HeroArdeos),
                Token(nameof(FaPalette.HeroElarion), p.HeroElarion),
                Token(nameof(FaPalette.HeroGunde), p.HeroGunde),
                Token(nameof(FaPalette.HeroHelena), p.HeroHelena),
                Token(nameof(FaPalette.HeroMara), p.HeroMara),
                Token(nameof(FaPalette.HeroMeiko), p.HeroMeiko),
                Token(nameof(FaPalette.HeroRime), p.HeroRime),
                Token(nameof(FaPalette.HeroSylvie), p.HeroSylvie),
                Token(nameof(FaPalette.HeroTariq), p.HeroTariq),
                Token(nameof(FaPalette.HeroVigour), p.HeroVigour),
                Token(nameof(FaPalette.HeroXavian), p.HeroXavian),
                Token(nameof(FaPalette.HeroUnknown), p.HeroUnknown));

            yield return Group("Event types", nameof(FaPalette),
                Token(nameof(FaPalette.EvCast), p.EvCast),
                Token(nameof(FaPalette.EvDamage), p.EvDamage),
                Token(nameof(FaPalette.EvHeal), p.EvHeal),
                Token(nameof(FaPalette.EvBuff), p.EvBuff),
                Token(nameof(FaPalette.EvBuffFade), p.EvBuffFade),
                Token(nameof(FaPalette.EvDebuff), p.EvDebuff),
                Token(nameof(FaPalette.EvDeath), p.EvDeath),
                Token(nameof(FaPalette.EvResource), p.EvResource),
                Token(nameof(FaPalette.EvSystem), p.EvSystem),
                Token(nameof(FaPalette.EvModified), p.EvModified),
                Token(nameof(FaPalette.EvFabricated), p.EvFabricated),
                Token(nameof(FaPalette.EvReordered), p.EvReordered));

            yield return Group("Elevation", nameof(FaElevation),
                Token(nameof(FaElevation.ShadowCard), Elevation.ShadowCard),
                Token(nameof(FaElevation.ShadowLg), Elevation.ShadowLg));

            yield return Group("Metrics", nameof(FaMetrics),
                Token(nameof(FaMetrics.BorderWidth), Metrics.BorderWidth),
                Token(nameof(FaMetrics.AccentWidth), Metrics.AccentWidth),
                Token(nameof(FaMetrics.RingWidth), Metrics.RingWidth),
                Token(nameof(FaMetrics.RadiusXs), Metrics.RadiusXs),
                Token(nameof(FaMetrics.RadiusSm), Metrics.RadiusSm),
                Token(nameof(FaMetrics.RadiusMd), Metrics.RadiusMd),
                Token(nameof(FaMetrics.RadiusLg), Metrics.RadiusLg),
                Token(nameof(FaMetrics.RadiusXl), Metrics.RadiusXl),
                Token(nameof(FaMetrics.RadiusPill), Metrics.RadiusPill));

            yield return Group("Typography", nameof(FaTypography),
                Token(nameof(FaTypography.FontBody), Typography.FontBody),
                Token(nameof(FaTypography.FontHeading), Typography.FontHeading),
                Token(nameof(FaTypography.FontMono), Typography.FontMono),
                Token(nameof(FaTypography.FsLabel), Typography.FsLabel),
                Token(nameof(FaTypography.FsMeta), Typography.FsMeta),
                Token(nameof(FaTypography.FsBody), Typography.FsBody),
                Token(nameof(FaTypography.FsValue), Typography.FsValue),
                Token(nameof(FaTypography.FsTitle), Typography.FsTitle),
                Token(nameof(FaTypography.FsLg), Typography.FsLg),
                Token(nameof(FaTypography.FsStat), Typography.FsStat));
        }
    }

    private static FaTokenGroup Group(string name, string source, params FaToken[] tokens) => new(name, source, tokens);

    private static FaToken Token(string property, FaColor value) => new(FaToken.KebabCase(property), value.ToCss());

    private static FaToken Token(string property, string value) => new(FaToken.KebabCase(property), value);
}
