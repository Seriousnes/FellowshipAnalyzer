namespace FellowshipAnalyzer.Core.Common.Spells.Vigour;

/// <summary>Vigour's talent tree nodes as spell metadata (id, name, icon) for display; the source the generator reads to emit the <c>VigourTalents</c> id constants. Ids come from the Season 3 <c>hero_data.json</c> tree and are listed in tree order, row 1 first.</summary>
public class Talents
{
    /// <summary>The <c>Master of Triage</c> talent</summary>
    public static Talent MasterOfTriage { get; } = new Talent { Id = 66, Name = "Master of Triage", Icon = "Tex_SpellBook06_87.jpg" };
    /// <summary>The <c>Enduring Light</c> talent</summary>
    public static Talent EnduringLight { get; } = new Talent { Id = 448, Name = "Enduring Light", Icon = "" };
    /// <summary>The <c>Radiant Soul</c> talent</summary>
    public static Talent RadiantSoul { get; } = new Talent { Id = 654, Name = "Radiant Soul", Icon = "T_RuneOfGold.jpg" };
    /// <summary>The <c>Alacritous Healing</c> talent</summary>
    public static Talent AlacritousHealing { get; } = new Talent { Id = 59, Name = "Alacritous Healing", Icon = "T_Vigor_Heal.jpg" };
    /// <summary>The <c>Ruptured Soul</c> talent</summary>
    public static Talent RupturedSoul { get; } = new Talent { Id = 62, Name = "Ruptured Soul", Icon = "T_Vigor_Soulbrand.jpg" };
    /// <summary>The <c>Expansive Mind</c> talent</summary>
    public static Talent ExpansiveMind { get; } = new Talent { Id = 61, Name = "Expansive Mind", Icon = "T_Nhance_RPG_Icons_ManaTome.jpg" };
    /// <summary>The <c>Sacred Barrier</c> talent</summary>
    public static Talent SacredBarrier { get; } = new Talent { Id = 566, Name = "Sacred Barrier", Icon = "T_SunOrb.jpg" };
    /// <summary>The <c>Ceremony of Light</c> talent</summary>
    public static Talent CeremonyOfLight { get; } = new Talent { Id = 393, Name = "Ceremony of Light", Icon = "Tex_SpellBook06_23.jpg" };
    /// <summary>The <c>Dawn Strider</c> talent</summary>
    public static Talent DawnStrider { get; } = new Talent { Id = 655, Name = "Dawn Strider", Icon = "" };
    /// <summary>The <c>Enlightened Soul</c> talent</summary>
    public static Talent EnlightenedSoul { get; } = new Talent { Id = 56, Name = "Enlightened Soul", Icon = "T_GoldTransmutation.jpg" };
    /// <summary>The <c>Shining Blast</c> talent</summary>
    public static Talent ShiningBlast { get; } = new Talent { Id = 651, Name = "Shining Blast", Icon = "T_Vigor_Cone.jpg" };
    /// <summary>The <c>Greater Renewal</c> talent</summary>
    public static Talent GreaterRenewal { get; } = new Talent { Id = 656, Name = "Greater Renewal", Icon = "T_Vigor_Rune.jpg" };
    /// <summary>The <c>Grand Proliferation</c> talent</summary>
    public static Talent GrandProliferation { get; } = new Talent { Id = 653, Name = "Grand Proliferation", Icon = "T_Vigor_HealRune.jpg" };
    /// <summary>The <c>Ascending Avatar</c> talent</summary>
    public static Talent AscendingAvatar { get; } = new Talent { Id = 181, Name = "Ascending Avatar", Icon = "T_Vigor_Spirit.jpg" };
    /// <summary>The <c>Meticulous Runesmith</c> talent</summary>
    public static Talent MeticulousRunesmith { get; } = new Talent { Id = 179, Name = "Meticulous Runesmith", Icon = "T_LightPortal.jpg" };
    /// <summary>The <c>Divine Light</c> talent</summary>
    public static Talent DivineLight { get; } = new Talent { Id = 650, Name = "Divine Light", Icon = "" };
    /// <summary>The <c>Runic Revelations</c> talent</summary>
    public static Talent RunicRevelations { get; } = new Talent { Id = 71, Name = "Runic Revelations", Icon = "T_Nhance_RPG_Icons_CorruptionKnowledge.jpg" };
    /// <summary>The <c>Beacon in the Dark</c> talent</summary>
    public static Talent BeaconInTheDark { get; } = new Talent { Id = 65, Name = "Beacon in the Dark", Icon = "Priest5.jpg" };
}
