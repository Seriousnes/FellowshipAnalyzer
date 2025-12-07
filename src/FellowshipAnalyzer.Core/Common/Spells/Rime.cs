namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// All Rime hero spells. Contains spell identity and icon data.
/// Gameplay metadata (cooldowns, GCD, etc.) is defined in the Abilities module.
/// </summary>
public class RimeSpells : ISpellRegistry
{
    // Core
    public static Spell BrainFreeze { get; } = new(1019);
    public static Spell BurstingIce { get; } = new(1031, "Bursting Ice", "T_Rime_CastedDebuffAOEdamage.jpg");
    public static Spell BurstingIceDamage { get; } = new(1001396, "Bursting Ice", "T_Rime_CastedDebuffAOEdamage.jpg");
    public static Spell ColdSnap { get; } = new(1020, "Cold Snap", "T_Rime_InstantHit.jpg");
    public static Spell FlightOfTheNavir { get; } = new(1009, "Flight of the Navir", "T_Rime_BirdCD.jpg");
    public static Spell FreezingTorrent { get; } = new(1027, "Freezing Torrent", "T_Rime_ChanneledBeam.jpg");
    public static Spell FrigidWinds { get; } = new(1021);
    public static Spell FrostBolt { get; } = new(1029, "Frost Bolt", "T_Rime_SingleTargetBolt.jpg");
    public static Spell FrostWard { get; } = new(1015);
    public static Spell GlacialBlast { get; } = new(1028, "Glacial Blast", "T_Rime_AnimaBolt.jpg");
    public static Spell IceBlitz { get; } = new(1032);
    public static Spell IceComet { get; } = new(1018, "Ice Comet", "T_Rime_OnTargetPulsatingAOE.jpg");
    public static Spell IceDash { get; } = new(1024);
    public static Spell WintersBlessing { get; } = new(1026, "Winter's Blessing", "T_Rime_HealingBuff.jpg");
    public static Spell WrathOfWinter { get; } = new(1023, "Wrath of Winter", "T_Rime_SpiritAbility.jpg");


    // Other
    public static Spell FrostSwallows { get; } = new(1033, "Frost Swallows", "T_Rime_BirdCD.jpg");
    public static Spell FrostSwallowsDamage { get; } = new(1001365, "Frost Swallows", "T_Rime_BirdCD.jpg");    
}
