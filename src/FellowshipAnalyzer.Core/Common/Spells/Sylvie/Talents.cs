namespace FellowshipAnalyzer.Core.Common.Spells.Sylvie;

/// <summary>Sylvie's talent tree nodes as spell metadata (id, name, icon) for display; the source the generator reads to emit the <c>SylvieTalents</c> id constants. Ids come from the Season 3 talent tree and are listed in tree order, row 1 first.</summary>
public class Talents
{
    /// <summary>The <c>Nettle to the Petal</c> talent; its id backs the generated <c>SylvieTalents.NettleToThePetal</c> constant.</summary>
    public static Talent NettleToThePetal { get; } = new Talent { Id = 176, Name = "Nettle to the Petal", Icon = "T_Mosse_Lifepetal.jpg" };
    /// <summary>The <c>Synchronized Fluttering</c> talent; its id backs the generated <c>SylvieTalents.SynchronizedFluttering</c> constant.</summary>
    public static Talent SynchronizedFluttering { get; } = new Talent { Id = 20, Name = "Synchronized Fluttering", Icon = "Sylvie_AbilityIcon_03.jpg" };
    /// <summary>The <c>Verdant Restoration</c> talent; its id backs the generated <c>SylvieTalents.VerdantRestoration</c> constant.</summary>
    public static Talent VerdantRestoration { get; } = new Talent { Id = 170, Name = "Verdant Restoration", Icon = "T_Nhance_RPG_Shadow_58.jpg" };
    /// <summary>The <c>Bloomin' Boomshrooms</c> talent; its id backs the generated <c>SylvieTalents.BloominBoomshrooms</c> constant.</summary>
    public static Talent BloominBoomshrooms { get; } = new Talent { Id = 32, Name = "Bloomin' Boomshrooms", Icon = "T_Mosse_Boomshroom.jpg" };
    /// <summary>The <c>Natural Knowledge</c> talent; its id backs the generated <c>SylvieTalents.NaturalKnowledge</c> constant.</summary>
    public static Talent NaturalKnowledge { get; } = new Talent { Id = 198, Name = "Natural Knowledge", Icon = "Tex_blue_9.jpg" };
    /// <summary>The <c>Every Other Flutter</c> talent; its id backs the generated <c>SylvieTalents.EveryOtherFlutter</c> constant.</summary>
    public static Talent EveryOtherFlutter { get; } = new Talent { Id = 662, Name = "Every Other Flutter", Icon = "" };
    /// <summary>The <c>Symbiosis</c> talent; its id backs the generated <c>SylvieTalents.Symbiosis</c> constant.</summary>
    public static Talent Symbiosis { get; } = new Talent { Id = 661, Name = "Symbiosis", Icon = "T_Mosse_Vine.jpg" };
    /// <summary>The <c>Saving Grace</c> talent; its id backs the generated <c>SylvieTalents.SavingGrace</c> constant.</summary>
    public static Talent SavingGrace { get; } = new Talent { Id = 664, Name = "Saving Grace", Icon = "" };
    /// <summary>The <c>Shimmering Wings</c> talent; its id backs the generated <c>SylvieTalents.ShimmeringWings</c> constant.</summary>
    public static Talent ShimmeringWings { get; } = new Talent { Id = 660, Name = "Shimmering Wings", Icon = "" };
    /// <summary>The <c>Sprouting Nettles</c> talent; its id backs the generated <c>SylvieTalents.SproutingNettles</c> constant.</summary>
    public static Talent SproutingNettles { get; } = new Talent { Id = 659, Name = "Sprouting Nettles", Icon = "Sylvie_AbilityIcon_02.jpg" };
    /// <summary>The <c>Nurtured Haven</c> talent; its id backs the generated <c>SylvieTalents.NurturedHaven</c> constant.</summary>
    public static Talent NurturedHaven { get; } = new Talent { Id = 197, Name = "Nurtured Haven", Icon = "T_Mosse_LinkCD.jpg" };
    /// <summary>The <c>Bluey's Gambit</c> talent; its id backs the generated <c>SylvieTalents.BlueysGambit</c> constant.</summary>
    public static Talent BlueysGambit { get; } = new Talent { Id = 26, Name = "Bluey's Gambit", Icon = "T_Nhance_RPG_Elements_32.jpg" };
    /// <summary>The <c>Natural Protector</c> talent; its id backs the generated <c>SylvieTalents.NaturalProtector</c> constant.</summary>
    public static Talent NaturalProtector { get; } = new Talent { Id = 173, Name = "Natural Protector", Icon = "Druid17.jpg" };
    /// <summary>The <c>Flutterswift</c> talent; its id backs the generated <c>SylvieTalents.Flutterswift</c> constant.</summary>
    public static Talent Flutterswift { get; } = new Talent { Id = 23, Name = "Flutterswift", Icon = "T_ArcaneWhirl.jpg" };
    /// <summary>The <c>Flower Power</c> talent; its id backs the generated <c>SylvieTalents.FlowerPower</c> constant.</summary>
    public static Talent FlowerPower { get; } = new Talent { Id = 31, Name = "Flower Power", Icon = "T_Mosse_Bigheal.jpg" };
    /// <summary>The <c>Rowdy Rootsap</c> talent; its id backs the generated <c>SylvieTalents.RowdyRootsap</c> constant.</summary>
    public static Talent RowdyRootsap { get; } = new Talent { Id = 196, Name = "Rowdy Rootsap", Icon = "T_Mosse_Rootheal.jpg" };
    /// <summary>The <c>Trailing Restoration</c> talent; its id backs the generated <c>SylvieTalents.TrailingRestoration</c> constant.</summary>
    public static Talent TrailingRestoration { get; } = new Talent { Id = 172, Name = "Trailing Restoration", Icon = "T_ArcaneAid.jpg" };
    /// <summary>The <c>In Bloom</c> talent; its id backs the generated <c>SylvieTalents.InBloom</c> constant.</summary>
    public static Talent InBloom { get; } = new Talent { Id = 663, Name = "In Bloom", Icon = "" };
}
