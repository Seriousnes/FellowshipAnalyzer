namespace FellowshipAnalyzer.Core.Common.Spells.Gunde;

/// <summary>Gunde's Season 3 talent tree: one <see cref="Talent"/> definition per selectable talent, each backing a generated <c>GundeTalents</c> id constant for <c>[RequiresTalent]</c> gates.</summary>
public class Talents
{
    /// <summary>Gunde's <c>Death's Arc</c> talent.</summary>
    public static Talent DeathsArc { get; } = new Talent { Id = 748, Name = "Death's Arc", Icon = "T_Nhance_RPG_Icons_NightmarePuppet.jpg" };
    /// <summary>Gunde's <c>Superior Serration</c> talent.</summary>
    public static Talent SuperiorSerration { get; } = new Talent { Id = 749, Name = "Superior Serration", Icon = "Gunde_Frontal.jpg" };
    /// <summary>Gunde's <c>Frenzied Reign</c> talent.</summary>
    public static Talent FrenziedReign { get; } = new Talent { Id = 750, Name = "Frenzied Reign", Icon = "T_Gunde_HPDrain.jpg" };
    /// <summary>Gunde's <c>Deep Rend</c> talent.</summary>
    public static Talent DeepRend { get; } = new Talent { Id = 751, Name = "Deep Rend", Icon = "T_Status_Bleed.jpg" };
    /// <summary>Gunde's <c>Carnage</c> talent.</summary>
    public static Talent Carnage { get; } = new Talent { Id = 754, Name = "Carnage", Icon = "t_gunde_Healstrike.jpg" };
    /// <summary>Gunde's <c>Ancestral Instinct</c> talent.</summary>
    public static Talent AncestralInstinct { get; } = new Talent { Id = 756, Name = "Ancestral Instinct", Icon = "Tex_yellow_14.jpg" };
    /// <summary>Gunde's <c>Darkening Hearts</c> talent.</summary>
    public static Talent DarkeningHearts { get; } = new Talent { Id = 757, Name = "Darkening Hearts", Icon = "T_Gunde_Cleave.jpg" };
    /// <summary>Gunde's <c>Murder of Crows</c> talent.</summary>
    public static Talent MurderOfCrows { get; } = new Talent { Id = 758, Name = "Murder of Crows", Icon = "T_Icon_BloodCombat_129.jpg" };
    /// <summary>Gunde's <c>Raven's Precision</c> talent.</summary>
    public static Talent RavensPrecision { get; } = new Talent { Id = 760, Name = "Raven's Precision", Icon = "Gunde_AOESwing_01.jpg" };
    /// <summary>Gunde's <c>Bloodcraze</c> talent.</summary>
    public static Talent Bloodcraze { get; } = new Talent { Id = 761, Name = "Bloodcraze", Icon = "T_Gunde_SanguineFeast_off.jpg" };
    /// <summary>Gunde's <c>Oathshatter</c> talent.</summary>
    public static Talent Oathshatter { get; } = new Talent { Id = 762, Name = "Oathshatter", Icon = "T_Gunde_DoubleAxe.jpg" };
    /// <summary>Gunde's <c>Slayer's Grin</c> talent.</summary>
    public static Talent SlayersGrin { get; } = new Talent { Id = 763, Name = "Slayer's Grin", Icon = "T_Gunde_Rupture.jpg" };
    /// <summary>Gunde's <c>Bloodbath</c> talent.</summary>
    public static Talent Bloodbath { get; } = new Talent { Id = 764, Name = "Bloodbath", Icon = "T_Nhance_RPG_BloodCombat_22.jpg" };
    /// <summary>Gunde's <c>Sundered Flesh</c> talent.</summary>
    public static Talent SunderedFlesh { get; } = new Talent { Id = 765, Name = "Sundered Flesh", Icon = "T_Nhance_RPG_Icons_NightmareAura.jpg" };
    /// <summary>Gunde's <c>Grim Harvest</c> talent.</summary>
    public static Talent GrimHarvest { get; } = new Talent { Id = 766, Name = "Grim Harvest", Icon = "T_Gunde_Sawblade.jpg" };
    /// <summary>Gunde's <c>Harvester's Toll</c> talent.</summary>
    public static Talent HarvestersToll { get; } = new Talent { Id = 767, Name = "Harvester's Toll", Icon = "T_Icon_BloodCombat_143.jpg" };
    /// <summary>Gunde's <c>Crimson strikes</c> talent.</summary>
    public static Talent CrimsonStrikes { get; } = new Talent { Id = 768, Name = "Crimson strikes", Icon = "T_Icon_BloodCombat_141.jpg" };
    /// <summary>Gunde's <c>Massacre</c> talent.</summary>
    public static Talent Massacre { get; } = new Talent { Id = 772, Name = "Massacre", Icon = "T_Gunde_Slaughter.jpg" };
}
