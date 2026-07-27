namespace FellowshipAnalyzer.Core.Common.Spells.Mara;

/// <summary>Mara's talent tree, exposed as named <see cref="Talent"/> lookups for spell links and UI display.</summary>
public class Talents
{
    /// <summary>The <c>Red Ledger</c> talent in Mara's talent tree (FellowshipLogs talent id <c>108</c>).</summary>
    public static Talent RedLedger { get; } = new Talent { Id = 108, Name = "Red Ledger", Icon = "Mara_Bleed.jpg" };
    /// <summary>The <c>Corrosive Spill</c> talent in Mara's talent tree (FellowshipLogs talent id <c>206</c>).</summary>
    public static Talent CorrosiveSpill { get; } = new Talent { Id = 206, Name = "Corrosive Spill", Icon = "T_Icon_Unholy_197.jpg" };
    /// <summary>The <c>Assassin's Guile</c> talent in Mara's talent tree (FellowshipLogs talent id <c>422</c>).</summary>
    public static Talent AssassinsGuile { get; } = new Talent { Id = 422, Name = "Assassin's Guile", Icon = "T_Icon_Shadow_121.jpg" };
    /// <summary>The <c>Bloodrush</c> talent in Mara's talent tree (FellowshipLogs talent id <c>163</c>).</summary>
    public static Talent Bloodrush { get; } = new Talent { Id = 163, Name = "Bloodrush", Icon = "T_Nhance_RPG_BloodCombat_24.jpg" };
    /// <summary>The <c>Venomous Delight</c> talent in Mara's talent tree (FellowshipLogs talent id <c>112</c>).</summary>
    public static Talent VenomousDelight { get; } = new Talent { Id = 112, Name = "Venomous Delight", Icon = "Tex_green_23.jpg" };
    /// <summary>The <c>Efficient Killer</c> talent in Mara's talent tree (FellowshipLogs talent id <c>113</c>).</summary>
    public static Talent EfficientKiller { get; } = new Talent { Id = 113, Name = "Efficient Killer", Icon = "T_ShadowStab.jpg" };
    /// <summary>The <c>Gushing Blood</c> talent in Mara's talent tree (FellowshipLogs talent id <c>122</c>).</summary>
    public static Talent GushingBlood { get; } = new Talent { Id = 122, Name = "Gushing Blood", Icon = "Berserker5.jpg" };
    /// <summary>The <c>Feed the Queen</c> talent in Mara's talent tree (FellowshipLogs talent id <c>121</c>).</summary>
    public static Talent FeedTheQueen { get; } = new Talent { Id = 121, Name = "Feed the Queen", Icon = "T_Nhance_RPG_Shadow_41.jpg" };
    /// <summary>The <c>Deadly Scheme</c> talent in Mara's talent tree (FellowshipLogs talent id <c>110</c>).</summary>
    public static Talent DeadlyScheme { get; } = new Talent { Id = 110, Name = "Deadly Scheme", Icon = "Tex_SpellBook08_71.jpg" };
    /// <summary>The <c>Macabre Stratagem</c> talent in Mara's talent tree (FellowshipLogs talent id <c>10000</c>).</summary>
    public static Talent MacabreStratagem { get; } = new Talent { Id = 10000, Name = "Macabre Stratagem", Icon = "T_Nhance_RPG_Shadow_16.jpg" };
    /// <summary>The <c>Maiden's Doom</c> talent in Mara's talent tree (FellowshipLogs talent id <c>442</c>).</summary>
    public static Talent MaidensDoom { get; } = new Talent { Id = 442, Name = "Maiden's Doom", Icon = "Mara_Maiden.jpg" };
    /// <summary>The <c>Seething Burst</c> talent in Mara's talent tree (FellowshipLogs talent id <c>668</c>).</summary>
    public static Talent SeethingBurst { get; } = new Talent { Id = 668, Name = "Seething Burst", Icon = "T_Nhance_RPG_Shadow_20.jpg" };
    /// <summary>The <c>Hemotoxin</c> talent in Mara's talent tree (FellowshipLogs talent id <c>205</c>).</summary>
    public static Talent Hemotoxin { get; } = new Talent { Id = 205, Name = "Hemotoxin", Icon = "T_PoisonBlister.jpg" };
    /// <summary>The <c>Sinner's Pride</c> talent in Mara's talent tree (FellowshipLogs talent id <c>586</c>).</summary>
    public static Talent SinnersPride { get; } = new Talent { Id = 586, Name = "Sinner's Pride", Icon = "T_Nhance_RPG_Icons_SoulDarkening.jpg" };
    /// <summary>The <c>Malevolence</c> talent in Mara's talent tree (FellowshipLogs talent id <c>443</c>).</summary>
    public static Talent Malevolence { get; } = new Talent { Id = 443, Name = "Malevolence", Icon = "Tex_violet_7.jpg" };
    /// <summary>The <c>Arachnid Onslaught</c> talent in Mara's talent tree (FellowshipLogs talent id <c>166</c>).</summary>
    public static Talent ArachnidOnslaught { get; } = new Talent { Id = 166, Name = "Arachnid Onslaught", Icon = "Mara_SpiderAOE.jpg" };
    /// <summary>The <c>Caustic Wounds</c> talent in Mara's talent tree (FellowshipLogs talent id <c>667</c>).</summary>
    public static Talent CausticWounds { get; } = new Talent { Id = 667, Name = "Caustic Wounds", Icon = "Mara_backstab.jpg" };
    /// <summary>The <c>Puncture</c> talent in Mara's talent tree (FellowshipLogs talent id <c>123</c>).</summary>
    public static Talent Puncture { get; } = new Talent { Id = 123, Name = "Puncture", Icon = "Mara_RegainEnergyHit.jpg" };
}
