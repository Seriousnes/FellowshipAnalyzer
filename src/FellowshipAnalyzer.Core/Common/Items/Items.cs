#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.Common.Items;

/// <summary>Weapons granting Chronoshift or Repository of Frozen Light. The generated partial (built from the <c>items</c> scope of <c>data/spelldb.json</c>) adds the spells those items grant.</summary>
public partial class Items : IItemRegistry
{    
    public static Item AshasChronoshiftSpire_Ardeos { get; } = new(5984, "Asha's Chronoshift Spire", "Icon_Ardeos_Weapon_ArcaneTime_Weapon_R2_T0-heroic", HeroName.Ardeos);
    public static Item AshasChronoshiftSpire_Rime { get; } = new(5984, "Asha's Chronoshift Spire", "Icon_Rime_Weapon_ArcaneTime_Weapon_R2_T0-heroic", HeroName.Rime);
    public static Item AshasChronoshiftBow { get; } = new(5984, "Asha's Chronoshift Bow", "Icon_Bowguy_Weapon_ArcaneTime_Weapon_R2_T0-heroic", HeroName.Elarion);
    public static Item FragmentsOfFrozenTime { get; } = new(5986, "Fragments of Frozen Time", "Icon_Lisa_Weapon_FrostDragon_Weapon_R2_T0-heroic", HeroName.Aeona);
    public static Item IceshapersBrand { get; } = new(5986, "Iceshaper's Brand", "Icon_Mosse_FrostDrake_Weapon_R2_T0-heroic", HeroName.Sylvie);
    public static Item AncientIceshapersTome { get; } = new(5986, "Ancient Iceshaper's Tome", "Icon_Vigour_Weapon_FrostDrake_Weapon_R2_T0-heroic", HeroName.Vigour);

    public static IReadOnlyList<Item> ChronoshiftWeapons { get; } = [AshasChronoshiftSpire_Ardeos, AshasChronoshiftSpire_Rime, AshasChronoshiftBow];
    public static IReadOnlyList<Item> RepositoryOfFrozenLightWeapons { get; } = [FragmentsOfFrozenTime, IceshapersBrand, AncientIceshapersTome];
}
