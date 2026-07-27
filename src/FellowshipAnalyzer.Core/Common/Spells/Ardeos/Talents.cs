namespace FellowshipAnalyzer.Core.Common.Spells.Ardeos;

/// <summary>Ardeos's talent tree nodes as spell metadata (id, name, icon) for display; the source the generator reads to emit the <c>ArdeosTalents</c> id constants.</summary>
public class Talents
{
    /// <summary>The <c>Slow Burn</c> talent; its id backs the generated <c>ArdeosTalents.SlowBurn</c> constant.</summary>
    public static Talent SlowBurn { get; } = new Talent { Id = 162, Name = "Slow Burn", Icon = "T_Nhance_RPG_Fire_24.jpg" };
    /// <summary>The <c>Frog Squad</c> talent; its id backs the generated <c>ArdeosTalents.FrogSquad</c> constant.</summary>
    public static Talent FrogSquad { get; } = new Talent { Id = 158, Name = "Frog Squad", Icon = "T_Nhance_RPG_Icons_HypnoToad.jpg" };
    /// <summary>The <c>Great Balls of Fire</c> talent; its id backs the generated <c>ArdeosTalents.GreatBallsOfFire</c> constant.</summary>
    public static Talent GreatBallsOfFire { get; } = new Talent { Id = 160, Name = "Great Balls of Fire", Icon = "T_Nhance_RPG_Fire_36.jpg" };
    /// <summary>The <c>Backdraft</c> talent; its id backs the generated <c>ArdeosTalents.Backdraft</c> constant.</summary>
    public static Talent Backdraft { get; } = new Talent { Id = 617, Name = "Backdraft", Icon = "Firemage_Dot_02.jpg" };
    /// <summary>The <c>Flare up</c> talent; its id backs the generated <c>ArdeosTalents.FlareUp</c> constant.</summary>
    public static Talent FlareUp { get; } = new Talent { Id = 159, Name = "Flare up", Icon = "T_Nhance_RPG_Icons_FireRune.jpg" };
    /// <summary>The <c>Crash and Burn</c> talent; its id backs the generated <c>ArdeosTalents.CrashAndBurn</c> constant.</summary>
    public static Talent CrashAndBurn { get; } = new Talent { Id = 184, Name = "Crash and Burn", Icon = "T_Nhance_RPG_Icons_FireRealm.jpg" };
    /// <summary>The <c>Agonizing Blaze</c> talent; its id backs the generated <c>ArdeosTalents.AgonizingBlaze</c> constant.</summary>
    public static Talent AgonizingBlaze { get; } = new Talent { Id = 145, Name = "Agonizing Blaze", Icon = "T_Nhance_RPG_Fire_30.jpg" };
    /// <summary>The <c>Firestarter</c> talent; its id backs the generated <c>ArdeosTalents.Firestarter</c> constant.</summary>
    public static Talent Firestarter { get; } = new Talent { Id = 149, Name = "Firestarter", Icon = "Pyromancer20.jpg" };
    /// <summary>The <c>Undying Flame</c> talent; its id backs the generated <c>ArdeosTalents.UndyingFlame</c> constant.</summary>
    public static Talent UndyingFlame { get; } = new Talent { Id = 150, Name = "Undying Flame", Icon = "Pyromancer5.jpg" };
    /// <summary>The <c>Fiery Resilience</c> talent; its id backs the generated <c>ArdeosTalents.FieryResilience</c> constant.</summary>
    public static Talent FieryResilience { get; } = new Talent { Id = 152, Name = "Fiery Resilience", Icon = "T_Nhance_RPG_Fire_61.jpg" };
    /// <summary>The <c>Crackling Inferno</c> talent; its id backs the generated <c>ArdeosTalents.CracklingInferno</c> constant.</summary>
    public static Talent CracklingInferno { get; } = new Talent { Id = 183, Name = "Crackling Inferno", Icon = "Pyromancer15.jpg" };
    /// <summary>The <c>Magic Ward</c> talent; its id backs the generated <c>ArdeosTalents.MagicWard</c> constant.</summary>
    public static Talent MagicWard { get; } = new Talent { Id = 155, Name = "Magic Ward", Icon = "T_Arcane_Scroll.jpg" };
    /// <summary>The <c>Rolling Flames</c> talent; its id backs the generated <c>ArdeosTalents.RollingFlames</c> constant.</summary>
    public static Talent RollingFlames { get; } = new Talent { Id = 226, Name = "Rolling Flames", Icon = "Firemage_Dot_01.jpg" };
    /// <summary>The <c>Pyrophibian Frenzy</c> talent; its id backs the generated <c>ArdeosTalents.PyrophibianFrenzy</c> constant.</summary>
    public static Talent PyrophibianFrenzy { get; } = new Talent { Id = 146, Name = "Pyrophibian Frenzy", Icon = "Firemage_Firefrog.jpg" };
    /// <summary>The <c>Reign of Fire</c> talent; its id backs the generated <c>ArdeosTalents.ReignOfFire</c> constant.</summary>
    public static Talent ReignOfFire { get; } = new Talent { Id = 157, Name = "Reign of Fire", Icon = "Firemage_Inferno.jpg" };
    /// <summary>The <c>Intensifying Inferno</c> talent; its id backs the generated <c>ArdeosTalents.IntensifyingInferno</c> constant.</summary>
    public static Talent IntensifyingInferno { get; } = new Talent { Id = 151, Name = "Intensifying Inferno", Icon = "Firemage_Bolt.jpg" };
    /// <summary>The <c>Spirited Fortitude</c> talent; its id backs the generated <c>ArdeosTalents.SpiritedFortitude</c> constant.</summary>
    public static Talent SpiritedFortitude { get; } = new Talent { Id = 156, Name = "Spirited Fortitude", Icon = "Barbarian3.jpg" };
    /// <summary>The <c>Spontaneous Combustion</c> talent; its id backs the generated <c>ArdeosTalents.SpontaneousCombustion</c> constant.</summary>
    public static Talent SpontaneousCombustion { get; } = new Talent { Id = 153, Name = "Spontaneous Combustion", Icon = "Tex_r_02.jpg" };
    /// <summary>The <c>Apocalyptic Surge</c> talent; its id backs the generated <c>ArdeosTalents.ApocalypticSurge</c> constant.</summary>
    public static Talent ApocalypticSurge { get; } = new Talent { Id = 678, Name = "Apocalyptic Surge", Icon = "Firemage_Apocalypse.jpg" };
}
