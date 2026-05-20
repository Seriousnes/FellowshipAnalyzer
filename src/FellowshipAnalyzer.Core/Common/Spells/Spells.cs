namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// Merged, named spell lookup across all heroes. Cross-hero spells are declared directly here;
/// hero-specific spell properties are source-generated from every <see cref="ISpellRegistry"/>
/// implementor visible in this assembly.
/// </summary>
[GenerateRegistry<ISpellRegistry>]
public static partial class Spells
{
    public static Spell Chronoshift { get; } = new(1558, "Chronoshift", "T_Nhance_RPG_Icons_ArcaneLoad.jpg");
    public static Spell EpochBreak { get; } = new(1881, "Epoch Break", "");



    public static Effect Kindling { get; } = new(104, "Kindling", "T_Nhance_RPG_Gold_10.jpg");
    public static Effect EpochBreakBuff { get; } = new(2613, "Epoch Break", "");
}