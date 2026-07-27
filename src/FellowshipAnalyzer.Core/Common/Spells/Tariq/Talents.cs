namespace FellowshipAnalyzer.Core.Common.Spells.Tariq;

/// <summary>Tariq's talent tree nodes as spell metadata (id, name, icon) for display; the source the generator reads to emit the <c>TariqTalents</c> id constants.</summary>
public class Talents
{
    /// <summary>The <c>Left Hand Path</c> talent; its id backs the generated <c>TariqTalents.LeftHandPath</c> constant.</summary>
    public static Talent LeftHandPath { get; } = new Talent { Id = 72, Name = "Left Hand Path", Icon = "T_Ink_Facebreaker.jpg" };
    /// <summary>The <c>Blood &amp; Thunder</c> talent; its id backs the generated <c>TariqTalents.BloodAndThunder</c> constant.</summary>
    public static Talent BloodAndThunder { get; } = new Talent { Id = 74, Name = "Blood & Thunder", Icon = "Tex_SpellBook01_91.jpg" };
    /// <summary>The <c>Them Bones</c> talent; its id backs the generated <c>TariqTalents.ThemBones</c> constant.</summary>
    public static Talent ThemBones { get; } = new Talent { Id = 75, Name = "Them Bones", Icon = "T_Ink_Skullcrusher.jpg" };
    /// <summary>The <c>Bloodline</c> talent; its id backs the generated <c>TariqTalents.Bloodline</c> constant.</summary>
    public static Talent Bloodline { get; } = new Talent { Id = 76, Name = "Bloodline", Icon = "T_Icon_BloodCombat_141.jpg" };
    /// <summary>The <c>Crack the Sky</c> talent; its id backs the generated <c>TariqTalents.CrackTheSky</c> constant.</summary>
    public static Talent CrackTheSky { get; } = new Talent { Id = 77, Name = "Crack the Sky", Icon = "Electromancer18.jpg" };
    /// <summary>The <c>Sledgehammer</c> talent; its id backs the generated <c>TariqTalents.Sledgehammer</c> constant.</summary>
    public static Talent Sledgehammer { get; } = new Talent { Id = 78, Name = "Sledgehammer", Icon = "T_Nhance_RPG_Icons_ChaoticSpikes.jpg" };
    /// <summary>The <c>Far Beyond Driven</c> talent; its id backs the generated <c>TariqTalents.FarBeyondDriven</c> constant.</summary>
    public static Talent FarBeyondDriven { get; } = new Talent { Id = 79, Name = "Far Beyond Driven", Icon = "T_Nhance_RPG_Energy_13.jpg" };
    /// <summary>The <c>Mouth for War</c> talent; its id backs the generated <c>TariqTalents.MouthForWar</c> constant.</summary>
    public static Talent MouthForWar { get; } = new Talent { Id = 80, Name = "Mouth for War", Icon = "T_Ink_FocusedWrath.jpg" };
    /// <summary>The <c>Pneuma</c> talent; its id backs the generated <c>TariqTalents.Pneuma</c> constant.</summary>
    public static Talent Pneuma { get; } = new Talent { Id = 81, Name = "Pneuma", Icon = "T_Nhance_RPG_Energy_18.jpg" };
    /// <summary>The <c>Killing in the Name</c> talent; its id backs the generated <c>TariqTalents.KillingInTheName</c> constant.</summary>
    public static Talent KillingInTheName { get; } = new Talent { Id = 82, Name = "Killing in the Name", Icon = "T_Ink_Cullingstrike.jpg" };
    /// <summary>The <c>Schism</c> talent; its id backs the generated <c>TariqTalents.Schism</c> constant.</summary>
    public static Talent Schism { get; } = new Talent { Id = 85, Name = "Schism", Icon = "T_Nhance_RPG_Icons_TwoFace.jpg" };
    /// <summary>The <c>Ride the Lightning</c> talent; its id backs the generated <c>TariqTalents.RideTheLightning</c> constant.</summary>
    public static Talent RideTheLightning { get; } = new Talent { Id = 87, Name = "Ride the Lightning", Icon = "T_Ink_Selfbuff.jpg" };
    /// <summary>The <c>Thunderstruck</c> talent; its id backs the generated <c>TariqTalents.Thunderstruck</c> constant.</summary>
    public static Talent Thunderstruck { get; } = new Talent { Id = 89, Name = "Thunderstruck", Icon = "Electromancer1.jpg" };
    /// <summary>The <c>High Road</c> talent; its id backs the generated <c>TariqTalents.HighRoad</c> constant.</summary>
    public static Talent HighRoad { get; } = new Talent { Id = 441, Name = "High Road", Icon = "Barbarian8.jpg" };
    /// <summary>The <c>Kill Em All</c> talent; its id backs the generated <c>TariqTalents.KillEmAll</c> constant.</summary>
    public static Talent KillEmAll { get; } = new Talent { Id = 671, Name = "Kill Em All", Icon = "Berserker6.jpg" };
    /// <summary>The <c>The Motherload</c> talent; its id backs the generated <c>TariqTalents.TheMotherload</c> constant.</summary>
    public static Talent TheMotherload { get; } = new Talent { Id = 672, Name = "The Motherload", Icon = "T_Nhance_RPG_Icons_SoulGrave.jpg" };
    /// <summary>The <c>Square Hammer</c> talent; its id backs the generated <c>TariqTalents.SquareHammer</c> constant.</summary>
    public static Talent SquareHammer { get; } = new Talent { Id = 673, Name = "Square Hammer", Icon = "T_Ink_TimerAbility.jpg" };
    /// <summary>The <c>Ace of Spades</c> talent; its id backs the generated <c>TariqTalents.AceOfSpades</c> constant.</summary>
    public static Talent AceOfSpades { get; } = new Talent { Id = 675, Name = "Ace of Spades", Icon = "T_Nhance_RPG_Icons_GhostHand.jpg" };
}
