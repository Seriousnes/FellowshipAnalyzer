namespace FellowshipAnalyzer.Core.Contracts.Design;

/// <summary>
/// The CSS references C# hands to markup, one per token the application names from code. Every
/// member is derived from the <see cref="FaPalette"/> property that defines the token, so renaming
/// a palette property breaks the build here rather than silently emitting a dead custom property,
/// and a colour in C# and the same colour in CSS can never disagree.
/// </summary>
public static class FaVar
{
    /* Base palette */

    /// <summary><c>var(--fa-white)</c>.</summary>
    public static readonly string White = FaToken.Var(nameof(FaPalette.White));

    /// <summary><c>var(--fa-amber)</c>.</summary>
    public static readonly string Amber = FaToken.Var(nameof(FaPalette.Amber));

    /// <summary><c>var(--fa-steel)</c>.</summary>
    public static readonly string Steel = FaToken.Var(nameof(FaPalette.Steel));

    /// <summary><c>var(--fa-arcane)</c>.</summary>
    public static readonly string Arcane = FaToken.Var(nameof(FaPalette.Arcane));

    /// <summary><c>var(--fa-rust)</c>.</summary>
    public static readonly string Rust = FaToken.Var(nameof(FaPalette.Rust));

    /// <summary><c>var(--fa-stone)</c>.</summary>
    public static readonly string Stone = FaToken.Var(nameof(FaPalette.Stone));

    /* Brand artwork */

    /// <summary><c>var(--fa-brand-cream)</c>.</summary>
    public static readonly string BrandCream = FaToken.Var(nameof(FaPalette.BrandCream));

    /// <summary><c>var(--fa-brand-gold)</c>.</summary>
    public static readonly string BrandGold = FaToken.Var(nameof(FaPalette.BrandGold));

    /* Foreground and intent */

    /// <summary><c>var(--fa-fg-neutral)</c>.</summary>
    public static readonly string FgNeutral = FaToken.Var(nameof(FaPalette.FgNeutral));

    /// <summary><c>var(--fa-ice)</c>.</summary>
    public static readonly string Ice = FaToken.Var(nameof(FaPalette.Ice));

    /// <summary><c>var(--fa-danger)</c>.</summary>
    public static readonly string Danger = FaToken.Var(nameof(FaPalette.Danger));

    /* Performance tiers */

    /// <summary><c>var(--fa-perf-perfect)</c>.</summary>
    public static readonly string PerfPerfect = FaToken.Var(nameof(FaPalette.PerfPerfect));

    /// <summary><c>var(--fa-perf-good)</c>.</summary>
    public static readonly string PerfGood = FaToken.Var(nameof(FaPalette.PerfGood));

    /// <summary><c>var(--fa-perf-ok)</c>.</summary>
    public static readonly string PerfOk = FaToken.Var(nameof(FaPalette.PerfOk));

    /// <summary><c>var(--fa-perf-fail)</c>.</summary>
    public static readonly string PerfFail = FaToken.Var(nameof(FaPalette.PerfFail));

    /* Chart series */

    /// <summary><c>var(--fa-chart1)</c>.</summary>
    public static readonly string Chart1 = FaToken.Var(nameof(FaPalette.Chart1));

    /// <summary><c>var(--fa-chart2)</c>.</summary>
    public static readonly string Chart2 = FaToken.Var(nameof(FaPalette.Chart2));

    /// <summary><c>var(--fa-chart3)</c>.</summary>
    public static readonly string Chart3 = FaToken.Var(nameof(FaPalette.Chart3));

    /// <summary><c>var(--fa-chart4)</c>.</summary>
    public static readonly string Chart4 = FaToken.Var(nameof(FaPalette.Chart4));

    /// <summary><c>var(--fa-chart5)</c>.</summary>
    public static readonly string Chart5 = FaToken.Var(nameof(FaPalette.Chart5));

    /// <summary><c>var(--fa-chart6)</c>.</summary>
    public static readonly string Chart6 = FaToken.Var(nameof(FaPalette.Chart6));

    /// <summary>The categorical chart series references in slot order.</summary>
    public static IReadOnlyList<string> ChartSeries { get; } = [Chart1, Chart2, Chart3, Chart4, Chart5, Chart6];

    /* Hero roles */

    /// <summary><c>var(--fa-role-tank)</c>.</summary>
    public static readonly string RoleTank = FaToken.Var(nameof(FaPalette.RoleTank));

    /// <summary><c>var(--fa-role-healer)</c>.</summary>
    public static readonly string RoleHealer = FaToken.Var(nameof(FaPalette.RoleHealer));

    /// <summary><c>var(--fa-role-dps)</c>.</summary>
    public static readonly string RoleDps = FaToken.Var(nameof(FaPalette.RoleDps));

    /// <summary><c>var(--fa-role-unknown)</c>.</summary>
    public static readonly string RoleUnknown = FaToken.Var(nameof(FaPalette.RoleUnknown));

    /* Hero identity */

    /// <summary><c>var(--fa-hero-aeona)</c>.</summary>
    public static readonly string HeroAeona = FaToken.Var(nameof(FaPalette.HeroAeona));

    /// <summary><c>var(--fa-hero-ardeos)</c>.</summary>
    public static readonly string HeroArdeos = FaToken.Var(nameof(FaPalette.HeroArdeos));

    /// <summary><c>var(--fa-hero-elarion)</c>.</summary>
    public static readonly string HeroElarion = FaToken.Var(nameof(FaPalette.HeroElarion));

    /// <summary><c>var(--fa-hero-gunde)</c>.</summary>
    public static readonly string HeroGunde = FaToken.Var(nameof(FaPalette.HeroGunde));

    /// <summary><c>var(--fa-hero-helena)</c>.</summary>
    public static readonly string HeroHelena = FaToken.Var(nameof(FaPalette.HeroHelena));

    /// <summary><c>var(--fa-hero-mara)</c>.</summary>
    public static readonly string HeroMara = FaToken.Var(nameof(FaPalette.HeroMara));

    /// <summary><c>var(--fa-hero-meiko)</c>.</summary>
    public static readonly string HeroMeiko = FaToken.Var(nameof(FaPalette.HeroMeiko));

    /// <summary><c>var(--fa-hero-rime)</c>.</summary>
    public static readonly string HeroRime = FaToken.Var(nameof(FaPalette.HeroRime));

    /// <summary><c>var(--fa-hero-sylvie)</c>.</summary>
    public static readonly string HeroSylvie = FaToken.Var(nameof(FaPalette.HeroSylvie));

    /// <summary><c>var(--fa-hero-tariq)</c>.</summary>
    public static readonly string HeroTariq = FaToken.Var(nameof(FaPalette.HeroTariq));

    /// <summary><c>var(--fa-hero-vigour)</c>.</summary>
    public static readonly string HeroVigour = FaToken.Var(nameof(FaPalette.HeroVigour));

    /// <summary><c>var(--fa-hero-xavian)</c>.</summary>
    public static readonly string HeroXavian = FaToken.Var(nameof(FaPalette.HeroXavian));

    /// <summary><c>var(--fa-hero-unknown)</c>.</summary>
    public static readonly string HeroUnknown = FaToken.Var(nameof(FaPalette.HeroUnknown));
}
