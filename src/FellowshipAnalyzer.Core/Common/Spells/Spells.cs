namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// Merged, named spell lookup across all heroes. Cross-hero spells are declared directly here;
/// hero-specific spell properties are source-generated from every <see cref="ISpellRegistry"/>
/// implementor visible in this assembly.
/// </summary>
[GenerateRegistry<ISpellRegistry>]
public static partial class Spells
{
    /// <summary>A combat-log effect entry used to exclude it from a hero's spellbook category groupings.</summary>
    public static Effect Kindling { get; } = new() { Id = 104, Name = "Kindling", Icon = "T_Nhance_RPG_Gold_10.jpg" };
}