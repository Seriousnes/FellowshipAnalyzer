namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// A static spell definition used in spell registries (the equivalent of WoWAnalyzer's SPELLS entries).
/// Contains identity and display metadata. Gameplay metadata (cooldowns, GCD, etc.) lives in
/// <see cref="Analysis.SpellbookAbility"/>.
/// </summary>
public sealed record Spell(int Id, string Name = "", string Icon = "");

public interface ISpellRegistry
{
}

public class Spells : ISpellRegistry
{
    public static Spell Kindling { get; } = new(1000104, "Kindling", "T_Nhance_RPG_Gold_10.jpg");    
    public static Spell VoidbringerTouch { get; } = new(155, "Voidbringer's Touch", "T_Weapon_VoidTouch.jpg");
}