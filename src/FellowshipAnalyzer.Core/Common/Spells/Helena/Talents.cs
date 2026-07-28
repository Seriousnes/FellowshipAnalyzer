namespace FellowshipAnalyzer.Core.Common.Spells.Helena;

/// <summary>Helena's talent tree nodes as spell metadata (id, name, icon) for display; the source the generator reads to emit the <c>HelenaTalents</c> id constants. Ids come from the Season 3 <c>hero_data.json</c> tree and are listed in tree order, row 1 first.</summary>
public class Talents
{
    /// <summary>The <c>The Best Defense</c> talent; its id backs the generated <c>HelenaTalents.TheBestDefense</c> constant.</summary>
    public static Talent TheBestDefense { get; } = new Talent { Id = 207, Name = "The Best Defense", Icon = "warmaster_shield_throw_3.jpg" };
    /// <summary>The <c>Shield Mastery</c> talent; its id backs the generated <c>HelenaTalents.ShieldMastery</c> constant.</summary>
    public static Talent ShieldMastery { get; } = new Talent { Id = 208, Name = "Shield Mastery", Icon = "Tex_y_17_layered.jpg" };
    /// <summary>The <c>Sword &amp; Board</c> talent; its id backs the generated <c>HelenaTalents.SwordAndBoard</c> constant.</summary>
    public static Talent SwordAndBoard { get; } = new Talent { Id = 209, Name = "Sword & Board", Icon = "T_Warmaster_ShieldSlam.jpg" };
    /// <summary>The <c>Lingering Rush</c> talent; its id backs the generated <c>HelenaTalents.LingeringRush</c> constant.</summary>
    public static Talent LingeringRush { get; } = new Talent { Id = 649, Name = "Lingering Rush", Icon = "" };
    /// <summary>The <c>Guarded Veteran</c> talent; its id backs the generated <c>HelenaTalents.GuardedVeteran</c> constant.</summary>
    public static Talent GuardedVeteran { get; } = new Talent { Id = 645, Name = "Guarded Veteran", Icon = "T_Nhance_RPG_Fire_08_Yellow.jpg" };
    /// <summary>The <c>Punishing Strikes</c> talent; its id backs the generated <c>HelenaTalents.PunishingStrikes</c> constant.</summary>
    public static Talent PunishingStrikes { get; } = new Talent { Id = 644, Name = "Punishing Strikes", Icon = "T_Warmaster_FatalBlow.jpg" };
    /// <summary>The <c>Aftershock</c> talent; its id backs the generated <c>HelenaTalents.Aftershock</c> constant.</summary>
    public static Talent Aftershock { get; } = new Talent { Id = 213, Name = "Aftershock", Icon = "T_Nhance_RPG_Arcane_36.jpg" };
    /// <summary>The <c>Sharpened Blade</c> talent; its id backs the generated <c>HelenaTalents.SharpenedBlade</c> constant.</summary>
    public static Talent SharpenedBlade { get; } = new Talent { Id = 215, Name = "Sharpened Blade", Icon = "T_Warmaster_BleedStrike.jpg" };
    /// <summary>The <c>Unscarred Soul</c> talent; its id backs the generated <c>HelenaTalents.UnscarredSoul</c> constant.</summary>
    public static Talent UnscarredSoul { get; } = new Talent { Id = 390, Name = "Unscarred Soul", Icon = "T_WM_HoldTheLine.jpg" };
    /// <summary>The <c>High Command</c> talent; its id backs the generated <c>HelenaTalents.HighCommand</c> constant.</summary>
    public static Talent HighCommand { get; } = new Talent { Id = 210, Name = "High Command", Icon = "T_Nhance_RPG_Fire_05.jpg" };
    /// <summary>The <c>Defiance</c> talent; its id backs the generated <c>HelenaTalents.Defiance</c> constant.</summary>
    public static Talent Defiance { get; } = new Talent { Id = 648, Name = "Defiance", Icon = "" };
    /// <summary>The <c>Skull Cracker</c> talent; its id backs the generated <c>HelenaTalents.SkullCracker</c> constant.</summary>
    public static Talent SkullCracker { get; } = new Talent { Id = 214, Name = "Skull Cracker", Icon = "T_Nhance_RPG_BloodCombat_01.jpg" };
    /// <summary>The <c>Second Wind</c> talent; its id backs the generated <c>HelenaTalents.SecondWind</c> constant.</summary>
    public static Talent SecondWind { get; } = new Talent { Id = 219, Name = "Second Wind", Icon = "T_Nhance_RPG_Energy_07.jpg" };
    /// <summary>The <c>Martial Command</c> talent; its id backs the generated <c>HelenaTalents.MartialCommand</c> constant.</summary>
    public static Talent MartialCommand { get; } = new Talent { Id = 14, Name = "Martial Command", Icon = "T_Warmaster_Ultimate.jpg" };
    /// <summary>The <c>Gleaming Shield</c> talent; its id backs the generated <c>HelenaTalents.GleamingShield</c> constant.</summary>
    public static Talent GleamingShield { get; } = new Talent { Id = 641, Name = "Gleaming Shield", Icon = "T_Nhance_RPG_Gold_05.jpg" };
    /// <summary>The <c>Front Line Defender</c> talent; its id backs the generated <c>HelenaTalents.FrontLineDefender</c> constant.</summary>
    public static Talent FrontLineDefender { get; } = new Talent { Id = 646, Name = "Front Line Defender", Icon = "warmaster_shields_up.jpg" };
    /// <summary>The <c>Master of War</c> talent; its id backs the generated <c>HelenaTalents.MasterOfWar</c> constant.</summary>
    public static Talent MasterOfWar { get; } = new Talent { Id = 647, Name = "Master of War", Icon = "T_Nhance_RPG_Gold_03.jpg" };
    /// <summary>The <c>Greater Shockwave</c> talent; its id backs the generated <c>HelenaTalents.GreaterShockwave</c> constant.</summary>
    public static Talent GreaterShockwave { get; } = new Talent { Id = 643, Name = "Greater Shockwave", Icon = "T_Warmaster_shockwave.jpg" };
}
