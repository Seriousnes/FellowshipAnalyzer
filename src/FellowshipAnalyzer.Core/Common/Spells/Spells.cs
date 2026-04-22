namespace FellowshipAnalyzer.Core.Common.Spells;

public class Spells : ISpellRegistry
{
    public static Effect Kindling { get; } = new(104, "Kindling", "T_Nhance_RPG_Gold_10.jpg");
    public static Spell VoidbringerTouch { get; } = new(155, "Voidbringer's Touch", "T_Weapon_VoidTouch.jpg");
    public static Spell Chronoshift { get; } = new(1558, "Chronoshift", "T_Nhance_RPG_Icons_ArcaneLoad.jpg");
}