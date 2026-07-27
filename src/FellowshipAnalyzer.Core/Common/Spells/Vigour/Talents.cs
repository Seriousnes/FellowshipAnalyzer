namespace FellowshipAnalyzer.Core.Common.Spells.Vigour;

/// <summary>Vigour's talent tree nodes as spell metadata (id, name, icon) for display; the source the generator reads to emit the <c>VigourTalents</c> id constants.</summary>
public class Talents
{
    /// <summary>The <c>Master of Triage</c> talent; its id backs the generated <c>VigourTalents.MasterOfTriage</c> constant.</summary>
    public static Talent MasterOfTriage { get; } = new Talent { Id = 66, Name = "Master of Triage", Icon = "Tex_SpellBook06_87.jpg" };
    /// <summary>The <c>Ceremony of Light</c> talent; its id backs the generated <c>VigourTalents.CeremonyOfLight</c> constant.</summary>
    public static Talent CeremonyOfLight { get; } = new Talent { Id = 393, Name = "Ceremony of Light", Icon = "Tex_SpellBook06_23.jpg" };
    /// <summary>The <c>Epiphany</c> talent; its id backs the generated <c>VigourTalents.Epiphany</c> constant.</summary>
    public static Talent Epiphany { get; } = new Talent { Id = 178, Name = "Epiphany", Icon = "T_Vigor_DawnSphere.jpg" };
    /// <summary>The <c>Alacritous Healing</c> talent; its id backs the generated <c>VigourTalents.AlacritousHealing</c> constant.</summary>
    public static Talent AlacritousHealing { get; } = new Talent { Id = 59, Name = "Alacritous Healing", Icon = "T_Vigor_Heal.jpg" };
    /// <summary>The <c>Ruptured Soul</c> talent; its id backs the generated <c>VigourTalents.RupturedSoul</c> constant.</summary>
    public static Talent RupturedSoul { get; } = new Talent { Id = 62, Name = "Ruptured Soul", Icon = "T_Vigor_Soulbrand.jpg" };
    /// <summary>The <c>Expansive Mind</c> talent; its id backs the generated <c>VigourTalents.ExpansiveMind</c> constant.</summary>
    public static Talent ExpansiveMind { get; } = new Talent { Id = 61, Name = "Expansive Mind", Icon = "T_Nhance_RPG_Icons_ManaTome.jpg" };
    /// <summary>The <c>Sacred Barrier</c> talent; its id backs the generated <c>VigourTalents.SacredBarrier</c> constant.</summary>
    public static Talent SacredBarrier { get; } = new Talent { Id = 566, Name = "Sacred Barrier", Icon = "T_SunOrb.jpg" };
    /// <summary>The <c>Preserved Illumination</c> talent; its id backs the generated <c>VigourTalents.PreservedIllumination</c> constant.</summary>
    public static Talent PreservedIllumination { get; } = new Talent { Id = 564, Name = "Preserved Illumination", Icon = "T_GoldTransmutation.jpg" };
    /// <summary>The <c>Meticulous Runesmith</c> talent; its id backs the generated <c>VigourTalents.MeticulousRunesmith</c> constant.</summary>
    public static Talent MeticulousRunesmith { get; } = new Talent { Id = 179, Name = "Meticulous Runesmith", Icon = "T_LightPortal.jpg" };
    /// <summary>The <c>Bracing Light</c> talent; its id backs the generated <c>VigourTalents.BracingLight</c> constant.</summary>
    public static Talent BracingLight { get; } = new Talent { Id = 182, Name = "Bracing Light", Icon = "T_Vigor_Ward.jpg" };
    /// <summary>The <c>Magic Ward</c> talent; its id backs the generated <c>VigourTalents.MagicWard</c> constant.</summary>
    public static Talent MagicWard { get; } = new Talent { Id = 64, Name = "Magic Ward", Icon = "T_Arcane_Scroll.jpg" };
    /// <summary>The <c>Spirited Fortitude</c> talent; its id backs the generated <c>VigourTalents.SpiritedFortitude</c> constant.</summary>
    public static Talent SpiritedFortitude { get; } = new Talent { Id = 180, Name = "Spirited Fortitude", Icon = "Barbarian3.jpg" };
    /// <summary>The <c>Radiant Soul</c> talent; its id backs the generated <c>VigourTalents.RadiantSoul</c> constant.</summary>
    public static Talent RadiantSoul { get; } = new Talent { Id = 56, Name = "Radiant Soul", Icon = "T_RuneOfGold.jpg" };
    /// <summary>The <c>Ascending Avatar</c> talent; its id backs the generated <c>VigourTalents.AscendingAvatar</c> constant.</summary>
    public static Talent AscendingAvatar { get; } = new Talent { Id = 181, Name = "Ascending Avatar", Icon = "T_Vigor_Spirit.jpg" };
    /// <summary>The <c>Grand Design</c> talent; its id backs the generated <c>VigourTalents.GrandDesign</c> constant.</summary>
    public static Talent GrandDesign { get; } = new Talent { Id = 68, Name = "Grand Design", Icon = "Tex_SpellBook07_11.jpg" };
    /// <summary>The <c>Dawnbreaker's Legacy</c> talent; its id backs the generated <c>VigourTalents.DawnbreakersLegacy</c> constant.</summary>
    public static Talent DawnbreakersLegacy { get; } = new Talent { Id = 57, Name = "Dawnbreaker's Legacy", Icon = "Tex_orb_02_b.jpg" };
    /// <summary>The <c>Runic Revelations</c> talent; its id backs the generated <c>VigourTalents.RunicRevelations</c> constant.</summary>
    public static Talent RunicRevelations { get; } = new Talent { Id = 71, Name = "Runic Revelations", Icon = "T_Nhance_RPG_Icons_CorruptionKnowledge.jpg" };
    /// <summary>The <c>Beacon in the Dark</c> talent; its id backs the generated <c>VigourTalents.BeaconInTheDark</c> constant.</summary>
    public static Talent BeaconInTheDark { get; } = new Talent { Id = 65, Name = "Beacon in the Dark", Icon = "Priest5.jpg" };
}
