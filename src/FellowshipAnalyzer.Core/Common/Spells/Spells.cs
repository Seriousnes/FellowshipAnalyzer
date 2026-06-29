namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// Merged, named spell lookup across all heroes. Cross-hero spells are declared directly here;
/// hero-specific spell properties are source-generated from every <see cref="ISpellRegistry"/>
/// implementor visible in this assembly.
/// </summary>
[GenerateRegistry<ISpellRegistry>]
public static partial class Spells
{
    public static Spell Chronoshift { get; } = new() { Id = 1558, Name = "Chronoshift", Icon = "T_Nhance_RPG_Icons_ArcaneLoad.jpg" };
    public static Spell EpochBreak { get; } = new() { Id = 1881, Name = "Epoch Break" };

    public static Effect Kindling { get; } = new() { Id = 104, Name = "Kindling", Icon = "T_Nhance_RPG_Gold_10.jpg" };
    public static Effect EpochBreakBuff { get; } = new() { Id = 2613, Name = "Epoch Break" };
}