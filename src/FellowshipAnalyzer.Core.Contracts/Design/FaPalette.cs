namespace FellowshipAnalyzer.Core.Contracts.Design;

/// <summary>
/// Every colour token in the design system. A property name becomes a token name by kebab-casing
/// it under the <c>--fa-</c> prefix, so <c>BgCard</c> is <c>--fa-bg-card</c>.
/// </summary>
public sealed record FaPalette
{
    /// <summary>Pure white, base for light tints.</summary>
    public required FaColor White { get; init; }

    /// <summary>Pure black, base for shadow and recess tints.</summary>
    public required FaColor Black { get; init; }

    /// <summary>Neutral grey.</summary>
    public required FaColor Grey { get; init; }

    /// <summary>Warning amber.</summary>
    public required FaColor Amber { get; init; }

    /// <summary>Steel blue.</summary>
    public required FaColor Steel { get; init; }

    /// <summary>Arcane violet.</summary>
    public required FaColor Arcane { get; init; }

    /// <summary>Nature teal.</summary>
    public required FaColor Nature { get; init; }

    /// <summary>Hyperlink blue.</summary>
    public required FaColor Link { get; init; }

    /// <summary>Desaturated steel frame, base for borders.</summary>
    public required FaColor Frame { get; init; }

    /// <summary>Deep blood red, the death event colour.</summary>
    public required FaColor Blood { get; init; }

    /// <summary>Scorched orange-red.</summary>
    public required FaColor Rust { get; init; }

    /// <summary>Warm grey for an inert or available state.</summary>
    public required FaColor Stone { get; init; }

    /// <summary>Cream stop in the brand mark. Artwork, so every theme carries the same value.</summary>
    public required FaColor BrandCream { get; init; }

    /// <summary>Gold stop in the brand mark. Artwork, so every theme carries the same value.</summary>
    public required FaColor BrandGold { get; init; }

    /// <summary>Universal ground under every screen.</summary>
    public required FaColor BgBase { get; init; }

    /// <summary>Panel one step above the ground.</summary>
    public required FaColor BgRaised { get; init; }

    /// <summary>Standard content surface.</summary>
    public required FaColor BgSurface { get; init; }

    /// <summary>Card plate, the lightest ground.</summary>
    public required FaColor BgCard { get; init; }

    /// <summary>Recessed region inside a card.</summary>
    public required FaColor BgInset { get; init; }

    /// <summary>Hover wash on an interactive row.</summary>
    public required FaColor BgHover { get; init; }

    /// <summary>Surface lifted above its container.</summary>
    public required FaColor Raise { get; init; }

    /// <summary>Recessed well.</summary>
    public required FaColor Recess { get; init; }

    /// <summary>Deepest well.</summary>
    public required FaColor RecessDeep { get; init; }

    /// <summary>Section header fill, a gradient rather than a flat colour.</summary>
    public required string SectionHeader { get; init; }

    /// <summary>Section header fill on hover, a gradient rather than a flat colour.</summary>
    public required string SectionHeaderHover { get; init; }

    /// <summary>Primary cream body text.</summary>
    public required FaColor Text { get; init; }

    /// <summary>Secondary text, metadata.</summary>
    public required FaColor TextMuted { get; init; }

    /// <summary>Tertiary text, disabled states.</summary>
    public required FaColor TextDim { get; init; }

    /// <summary>Foreground on a coloured or inverted ground.</summary>
    public required FaColor Fg { get; init; }

    /// <summary>Muted foreground on a coloured ground.</summary>
    public required FaColor FgMuted { get; init; }

    /// <summary>Dim foreground on a coloured ground.</summary>
    public required FaColor FgDim { get; init; }

    /// <summary>Value colour for a stat with no performance tier.</summary>
    public required FaColor FgNeutral { get; init; }

    /// <summary>Emphasis gold: titles, links, eyebrows.</summary>
    public required FaColor Gold { get; init; }

    /// <summary>Lifted gold for gradients and hover.</summary>
    public required FaColor GoldLight { get; init; }

    /// <summary>Palest gold step.</summary>
    public required FaColor GoldPale { get; init; }

    /// <summary>Gold edge at low presence.</summary>
    public required FaColor GoldDim { get; init; }

    /// <summary>Rime hero accent.</summary>
    public required FaColor Ice { get; init; }

    /// <summary>Destructive action, a UI intent rather than a rating.</summary>
    public required FaColor Danger { get; init; }

    /// <summary>Default structural edge and divider.</summary>
    public required FaColor Border { get; init; }

    /// <summary>Card edge.</summary>
    public required FaColor BorderCard { get; init; }

    /// <summary>Quietest divider.</summary>
    public required FaColor BorderSubtle { get; init; }

    /// <summary>Hairline highlight along a lit edge.</summary>
    public required FaColor Edge { get; init; }

    /// <summary>
    /// Edge separating a panel from its container. The dark themes carry it at zero alpha, where a
    /// panel already reads by its own ground; the light theme is where a panel needs an outline.
    /// </summary>
    public required FaColor PanelBorder { get; init; }

    /// <summary>Perfect tier.</summary>
    public required FaColor PerfPerfect { get; init; }

    /// <summary>Good tier.</summary>
    public required FaColor PerfGood { get; init; }

    /// <summary>Ok tier.</summary>
    public required FaColor PerfOk { get; init; }

    /// <summary>Fail tier.</summary>
    public required FaColor PerfFail { get; init; }

    /// <summary>
    /// Brightest step of the fire ramp, a six-step warm scale running pale gold to burnt umber.
    /// Fire is a theme rather than a role, so a component picks the steps it wants and in what
    /// order; the design system does not assign them.
    /// </summary>
    public required FaColor Fire1 { get; init; }

    /// <summary>Second fire step, amber.</summary>
    public required FaColor Fire2 { get; init; }

    /// <summary>Third fire step, orange.</summary>
    public required FaColor Fire3 { get; init; }

    /// <summary>Fourth fire step, flame.</summary>
    public required FaColor Fire4 { get; init; }

    /// <summary>Fifth fire step, scorched red.</summary>
    public required FaColor Fire5 { get; init; }

    /// <summary>Deepest fire step, burnt umber.</summary>
    public required FaColor Fire6 { get; init; }

    /// <summary>Tank role.</summary>
    public required FaColor RoleTank { get; init; }

    /// <summary>Healer role.</summary>
    public required FaColor RoleHealer { get; init; }

    /// <summary>DPS role.</summary>
    public required FaColor RoleDps { get; init; }

    /// <summary>Role not determined.</summary>
    public required FaColor RoleUnknown { get; init; }

    /// <summary>Aeona identity colour.</summary>
    public required FaColor HeroAeona { get; init; }

    /// <summary>Ardeos identity colour.</summary>
    public required FaColor HeroArdeos { get; init; }

    /// <summary>Elarion identity colour.</summary>
    public required FaColor HeroElarion { get; init; }

    /// <summary>Gunde identity colour.</summary>
    public required FaColor HeroGunde { get; init; }

    /// <summary>Helena identity colour.</summary>
    public required FaColor HeroHelena { get; init; }

    /// <summary>Mara identity colour.</summary>
    public required FaColor HeroMara { get; init; }

    /// <summary>Meiko identity colour.</summary>
    public required FaColor HeroMeiko { get; init; }

    /// <summary>Rime identity colour.</summary>
    public required FaColor HeroRime { get; init; }

    /// <summary>Sylvie identity colour.</summary>
    public required FaColor HeroSylvie { get; init; }

    /// <summary>Tariq identity colour.</summary>
    public required FaColor HeroTariq { get; init; }

    /// <summary>Vigour identity colour.</summary>
    public required FaColor HeroVigour { get; init; }

    /// <summary>Xavian identity colour.</summary>
    public required FaColor HeroXavian { get; init; }

    /// <summary>Colour for a hero the report does not identify.</summary>
    public required FaColor HeroUnknown { get; init; }

    /// <summary>Cast event.</summary>
    public required FaColor EvCast { get; init; }

    /// <summary>Damage event.</summary>
    public required FaColor EvDamage { get; init; }

    /// <summary>Heal event.</summary>
    public required FaColor EvHeal { get; init; }

    /// <summary>Buff application event.</summary>
    public required FaColor EvBuff { get; init; }

    /// <summary>Buff removal event.</summary>
    public required FaColor EvBuffFade { get; init; }

    /// <summary>Debuff event.</summary>
    public required FaColor EvDebuff { get; init; }

    /// <summary>Death event.</summary>
    public required FaColor EvDeath { get; init; }

    /// <summary>Resource change event.</summary>
    public required FaColor EvResource { get; init; }

    /// <summary>System event.</summary>
    public required FaColor EvSystem { get; init; }

    /// <summary>Event mutated by a normalizer.</summary>
    public required FaColor EvModified { get; init; }

    /// <summary>Event fabricated by a normalizer.</summary>
    public required FaColor EvFabricated { get; init; }

    /// <summary>Event reordered by a normalizer.</summary>
    public required FaColor EvReordered { get; init; }

    /// <summary>The game-sampled palette the application ships with.</summary>
    public static FaPalette Original { get; } = CreateOriginal();

    /// <summary>The steel-and-gold dark theme.</summary>
    public static FaPalette Dark { get; } = CreateDark();

    /// <summary>The cream light theme, where structure comes from borders alone.</summary>
    public static FaPalette Light { get; } = CreateLight();

    private static FaPalette CreateOriginal()
    {
        FaColor white = "#ffffff";
        FaColor black = "#000000";
        FaColor frame = "#63696f";
        FaColor gold = "#b8965c";
        FaColor amber = "#fab700";
        FaColor steel = "#6b9ed2";
        FaColor arcane = "#9a7bff";
        FaColor ice = "#6ec8e8";
        FaColor blood = "#661111";
        FaColor textDim = "#949ba5";
        FaColor perfGood = "#4ec04e";
        FaColor perfFail = "#ac1f39";

        return new FaPalette
        {
            White = white,
            Black = black,
            Grey = "#787878",
            Amber = amber,
            Steel = steel,
            Arcane = arcane,
            Nature = "#5fd0b0",
            Link = "#0087ff",
            Frame = frame,
            Blood = blood,
            Rust = "#dd5533",
            Stone = "#696864",

            BrandCream = "#f4e9d0",
            BrandGold = "#d4a744",

            BgBase = "#182028",
            BgRaised = "#283038",
            BgSurface = "#313a45",
            BgCard = "#3a4452",
            BgInset = "#0e151d",
            BgHover = white.WithAlpha(FaTint.Faint),
            Raise = white.WithAlpha(FaTint.Faint),
            Recess = black.WithAlpha(FaTint.Soft),
            RecessDeep = black.WithAlpha(FaTint.Medium),
            SectionHeader = Gradient(gold.WithAlpha(FaTint.Subtle), gold.WithAlpha(FaTint.Faint)),
            SectionHeaderHover = Gradient(gold.WithAlpha(FaTint.Soft), gold.WithAlpha(FaTint.Faint)),

            Text = "#e8e0d8",
            TextMuted = "#abb3bc",
            TextDim = textDim,
            Fg = white.WithAlpha(FaTint.Strong),
            FgMuted = white.WithAlpha(FaTint.Medium),
            FgDim = white.WithAlpha(FaTint.Muted),
            FgNeutral = white,

            Gold = gold,
            GoldLight = "#d9b887",
            GoldPale = "#d9b887",
            GoldDim = gold.WithAlpha(FaTint.Subtle),
            Ice = ice,
            Danger = perfFail,

            Border = frame.WithAlpha(FaTint.Soft),
            BorderCard = frame.WithAlpha(FaTint.Muted),
            BorderSubtle = frame.WithAlpha(FaTint.Subtle),
            Edge = white.WithAlpha(FaTint.Faint),
            PanelBorder = frame.WithAlpha(FaTint.None),

            PerfPerfect = "#2090c0",
            PerfGood = perfGood,
            PerfOk = "#ffc84a",
            PerfFail = perfFail,

            Fire1 = "#ffe9a3",
            Fire2 = "#ffc233",
            Fire3 = "#fb9520",
            Fire4 = "#ee6a18",
            Fire5 = "#d24a15",
            Fire6 = "#a3601f",

            RoleTank = "#336699",
            RoleHealer = perfGood,
            RoleDps = perfFail,
            RoleUnknown = frame.WithAlpha(FaTint.Soft),

            HeroAeona = "#fc9fec",
            HeroArdeos = "#eb6328",
            HeroElarion = "#935dff",
            HeroGunde = "#943738",
            HeroHelena = "#b46831",
            HeroMara = "#965a90",
            HeroMeiko = "#28e05c",
            HeroRime = "#1ea3ee",
            HeroSylvie = "#ea4f84",
            HeroTariq = "#527af5",
            HeroVigour = "#dddbc5",
            HeroXavian = "#077365",
            HeroUnknown = "#9a9586",

            EvCast = ice,
            EvDamage = perfFail,
            EvHeal = perfGood,
            EvBuff = arcane,
            EvBuffFade = arcane,
            EvDebuff = amber,
            EvDeath = blood,
            EvResource = gold,
            EvSystem = textDim,
            EvModified = amber,
            EvFabricated = perfGood,
            EvReordered = steel,
        };
    }

    private static FaPalette CreateDark()
    {
        var navy = FaSourceColors.BsNavy;
        var grey = FaSourceColors.BsGrey;

        return Original with
        {
            BgBase = navy.Step(7),
            BgRaised = navy.Step(11),
            BgSurface = navy.Step(15),
            BgCard = navy.Step(14).WithAlpha(FaTint.Veil),
            BgInset = navy.Step(6).WithAlpha(FaTint.Strong),
            SectionHeader = Gradient(grey.WithAlpha(FaTint.Subtle)),
            SectionHeaderHover = Gradient(grey.WithAlpha(FaTint.Soft)),

            Text = FaSourceColors.BsPearl,
            TextMuted = grey,
            TextDim = grey.Step(41),

            Gold = FaSourceColors.WgGold,
            GoldLight = FaSourceColors.WgLight,
            GoldPale = FaSourceColors.WgPale,
            GoldDim = FaSourceColors.WgGold.WithAlpha(FaTint.Subtle),

            Border = grey.WithAlpha(FaTint.Soft),
            BorderCard = grey.WithAlpha(FaTint.Muted),
            BorderSubtle = grey.WithAlpha(FaTint.Subtle),

            RoleUnknown = grey.WithAlpha(FaTint.Soft),
        };
    }

    private static FaPalette CreateLight()
    {
        var cream = FaSourceColors.WgCream;
        var navy = FaSourceColors.BsNavy;
        var grey = FaSourceColors.BsGrey;
        var deep = FaSourceColors.WgDeep;

        return Original with
        {
            BgBase = cream,
            BgRaised = cream,
            BgSurface = cream,
            BgCard = cream,
            BgInset = cream,
            Raise = cream.Step(99),
            Recess = cream.Step(95),
            RecessDeep = cream.Step(91),
            SectionHeader = Gradient(navy.WithAlpha(FaTint.Subtle)),
            SectionHeaderHover = Gradient(navy.WithAlpha(FaTint.Soft)),

            Text = navy,
            TextMuted = grey.Step(42),
            TextDim = grey.Step(55),
            Fg = navy,
            FgMuted = grey.Step(42),
            FgDim = grey.Step(55),
            FgNeutral = navy,

            Gold = deep,
            GoldLight = deep.Step(30),
            GoldPale = deep.Step(26),
            GoldDim = FaSourceColors.WgLight.WithAlpha(FaTint.Medium),

            Fire1 = "#d99b00",
            Fire2 = "#e2731a",
            Fire3 = "#cf4a17",
            Fire4 = "#9d4a12",
            Fire5 = "#6f3f14",
            Fire6 = "#432a16",

            Border = navy.WithAlpha(FaTint.Soft),
            BorderCard = navy.WithAlpha(FaTint.Muted),
            BorderSubtle = navy.WithAlpha(FaTint.Subtle),
            Edge = navy.WithAlpha(FaTint.Subtle),
            PanelBorder = navy.WithAlpha(FaTint.Soft),

            BgHover = navy.WithAlpha(FaTint.Faint),

            RoleUnknown = navy.WithAlpha(FaTint.Soft),
        };
    }

    private static string Gradient(FaColor stop) => $"linear-gradient(180deg, transparent, {stop.ToCss()})";

    private static string Gradient(FaColor from, FaColor to) => $"linear-gradient(180deg, {from.ToCss()}, {to.ToCss()})";
}
