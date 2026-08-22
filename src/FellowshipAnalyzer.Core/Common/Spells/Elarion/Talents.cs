namespace FellowshipAnalyzer.Core.Common.Spells.Elarion;

/// <summary>Elarion's Season 3 talents, one static <see cref="Talent"/> per talent keyed by native game id; the generated <c>ElarionTalents</c> class exposes the same ids as compile-time constants for <c>[RequiresTalent]</c>.</summary>
public static class Talents
{
    /// <summary>Elarion's <c>Focused Expanse</c> talent.</summary>
    public static Talent FocusedExpanse { get; } = new Talent { Id = 167, Name = "Focused Expanse", Icon = "Bowguy_Multishot.jpg" };
    /// <summary>Elarion's <c>Piercing Seekers</c> talent.</summary>
    public static Talent PiercingSeekers { get; } = new Talent { Id = 201, Name = "Piercing Seekers", Icon = "T_Nhance_RPG_Icons_SoulArrow.jpg" };
    /// <summary>Elarion's <c>Resurgent Winds</c> talent, which grants a proc buff tracked by the hero's <c>ResurgentWindsTracker</c> module.</summary>
    public static Talent ResurgentWinds { get; } = new Talent { Id = 126, Name = "Resurgent Winds", Icon = "T_Icon_Tech_40.jpg" };
    /// <summary>Elarion's <c>Deadly Focus</c> talent.</summary>
    public static Talent DeadlyFocus { get; } = new Talent { Id = 679, Name = "Deadly Focus", Icon = "Bowguy_FocusShot.jpg" };
    /// <summary>Elarion's <c>Fusillade</c> talent.</summary>
    public static Talent Fusillade { get; } = new Talent { Id = 128, Name = "Fusillade", Icon = "T_Icon_Energy_106.jpg" };
    /// <summary>Elarion's <c>Skyward Munitions</c> talent.</summary>
    public static Talent SkywardMunitions { get; } = new Talent { Id = 132, Name = "Skyward Munitions", Icon = "Bowguy_Shot.jpg" };
    /// <summary>Elarion's <c>Repeating Stars</c> talent.</summary>
    public static Talent RepeatingStars { get; } = new Talent { Id = 124, Name = "Repeating Stars", Icon = "Bowguy_Rain.jpg" };
    /// <summary>Elarion's <c>Lunar Fury</c> talent.</summary>
    public static Talent LunarFury { get; } = new Talent { Id = 130, Name = "Lunar Fury", Icon = "Bowguy_Mark.jpg" };
    /// <summary>Elarion's <c>Lethal Shots</c> talent.</summary>
    public static Talent LethalShots { get; } = new Talent { Id = 202, Name = "Lethal Shots", Icon = "Tex_arrow.jpg" };
    /// <summary>Elarion's <c>Rising Moon</c> talent.</summary>
    public static Talent RisingMoon { get; } = new Talent { Id = 681, Name = "Rising Moon", Icon = "T_Nhance_RPG_Energy_17.jpg" };
    /// <summary>Elarion's <c>Lunarlight Affinity</c> talent.</summary>
    public static Talent LunarlightAffinity { get; } = new Talent { Id = 203, Name = "Lunarlight Affinity", Icon = "T_Icon_Energy_108.jpg" };
    /// <summary>Elarion's <c>Striker's Aim</c> talent.</summary>
    public static Talent StrikersAim { get; } = new Talent { Id = 680, Name = "Striker's Aim", Icon = "T_Nhance_RPG_Icons_MagicShot.jpg" };
    /// <summary>Elarion's <c>Fervent Supremacy</c> talent, which shortens Skystrider's Supremacy's cooldown and turns its window into four charges of Empowered Multishot over fifteen seconds.</summary>
    public static Talent FerventSupremacy { get; } = new Talent { Id = 136, Name = "Fervent Supremacy", Icon = "Bowguy_Supremacy.jpg" };
    /// <summary>Elarion's <c>Impending Heartseeker</c> talent, whose proc buff resets Heartseeker Barrage and empowers the next one when consumed.</summary>
    public static Talent ImpendingHeartseeker { get; } = new Talent { Id = 168, Name = "Impending Heartseeker", Icon = "Bowguy_Spray.jpg" };
    /// <summary>Elarion's <c>Final Crescendo</c> talent.</summary>
    public static Talent FinalCrescendo { get; } = new Talent { Id = 169, Name = "Final Crescendo", Icon = "Bowguy_Ricochet.jpg" };
    /// <summary>Elarion's <c>Last Lights</c> talent.</summary>
    public static Talent LastLights { get; } = new Talent { Id = 164, Name = "Last Lights", Icon = "Tex_b_24.jpg" };
    /// <summary>Elarion's <c>Swift Reload</c> talent.</summary>
    public static Talent SwiftReload { get; } = new Talent { Id = 682, Name = "Swift Reload", Icon = "Tex_SpellBook01_16.jpg" };
    /// <summary>Elarion's <c>Skylit Grace</c> talent.</summary>
    public static Talent SkylitGrace { get; } = new Talent { Id = 200, Name = "Skylit Grace", Icon = "Tex_b_03.jpg" };
}
