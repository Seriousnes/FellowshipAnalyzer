namespace FellowshipAnalyzer.Core.Common.Spells.Rime;

/// <summary>
/// All Rime hero spells. Contains spell identity and icon data.
/// Gameplay metadata (cooldowns, GCD, etc.) is defined in the Abilities module.
/// </summary>
public class Spells : ISpellRegistry
{
    // Core
    public static Spell BrainFreeze { get; } = new(1019, "Brain Freeze", "T_RimeIcon_Interrupt.jpg");
    public static Spell BurstingIce { get; } = new(1031, "Bursting Ice", "T_Rime_CastedDebuffAOEdamage.jpg");
    public static Effect BurstingIceDamage { get; } = new(1396, "Bursting Ice", "T_Rime_CastedDebuffAOEdamage.jpg");
    public static Spell ColdSnap { get; } = new(1020, "Cold Snap", "T_Rime_InstantHit.jpg");
    public static Spell FlightOfTheNavir { get; } = new(1009, "Flight of the Navir", "T_Rime_BirdCD.jpg");
    public static Spell FreezingTorrent { get; } = new(1027, "Freezing Torrent", "T_Rime_ChanneledBeam.jpg");
    public static Spell FrigidWinds { get; } = new(1021);
    public static Spell FrostBolt { get; } = new(1029, "Frost Bolt", "T_Rime_SingleTargetBolt.jpg");
    public static Spell FrostWard { get; } = new(1015, "Frost Ward", "T_Rime_SelfDefenceBuff.jpg");
    public static Spell GlacialBlast { get; } = new(1028, "Glacial Blast", "T_Rime_AnimaBolt.jpg");
    public static Spell IceBlitz { get; } = new(1032, "Ice Blitz", "T_Nhance_RPG_Icons_IcySpikes.jpg");
    public static Spell IceComet { get; } = new(1018, "Ice Comet", "T_Rime_OnTargetPulsatingAOE.jpg");
    public static Spell IceDash { get; } = new(1024, "Ice Dash", "T_Rime_Dash.jpg");
    public static Spell WintersBlessing { get; } = new(1026, "Winter's Blessing", "T_Rime_HealingBuff.jpg");
    public static Effect WintersBlessingBuff { get; } = new(1026, "Winter's Blessing", "T_Rime_HealingBuff.jpg");
    public static Spell WrathOfWinter { get; } = new(1023, "Wrath of Winter", "T_Rime_SpiritAbility.jpg");
    public static Spell FlightOfTheNavirBuff { get; } = new(2446, "Flight Of the Navir", "T_Rime_BirdCD.jpg");

    // Talents
    public static Effect WintersEmbrace { get; } = new(2303, "Winter's Embrace", "Cryomancer8.jpg");


    // Other
    public static Spell FrostSwallows { get; } = new(1033, "Frost Swallows", "T_Rime_BirdCD.jpg");
    public static Effect FrostSwallowsDamage { get; } = new(1365, "Frost Swallows", "T_Rime_BirdCD.jpg");
}
