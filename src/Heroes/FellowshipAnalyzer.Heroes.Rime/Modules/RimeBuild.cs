namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/// <summary>
/// The Winter Orb spender loadout a Rime player is running, detected from which spenders they
/// actually cast. <see cref="Default"/> spends orbs on Glacial Blast (single target) and Ice Comet
/// (AoE); <see cref="IcyTalons"/> spends them on Talon Strike (single target) and Rising Talons (AoE).
/// </summary>
public enum RimeBuild
{
    Default,
    IcyTalons,
}
