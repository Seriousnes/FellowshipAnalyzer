#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace FellowshipAnalyzer.Core.Common.Items;

/// <summary>Generic legendary items whose Chronoshift-granting behavior is shared across the heroes that can equip them. The generated partial (built from the <c>items</c> scope of <c>data/spelldb.json</c>) adds the spells those items grant.</summary>
public partial class Items : IItemRegistry
{    
    public static Item AshasChronoshiftSpire_Ardeos { get; } = new(5326, "Asha's Chronoshift Spire", "Icon_Ardeos_Weapon_ArcaneTime_Weapon_R1_T0.jpg");
    public static Item AshasChronoshiftSpire_Rime { get; } = new(5333, "Asha's Chronoshift Spire", "Icon_Rime_Weapon_ArcaneTime_Weapon_R2_T0_Heroic.jpg");
    public static Item AshasChronoshiftBow { get; } = new(5325, "Asha's Chronoshift Bow", "Icon_Bowguy_Weapon_ArcaneTime_Weapon_R2_T0_Heroic.jpg");

    public static Item DraconicBracersOfTheDevouringFlame { get; } = new(5225, "Draconic Bracers of the Devouring Flame", "Tex_bracers_01_b.jpg");
    public static Item ExecutionersUnsanitaryBands { get; } = new(5225, "Executioner's Unsanitary Bands", "Tex_bracers_01_b.jpg");
}
