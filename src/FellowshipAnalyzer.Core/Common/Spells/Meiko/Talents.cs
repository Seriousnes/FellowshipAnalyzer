namespace FellowshipAnalyzer.Core.Common.Spells.Meiko;

/// <summary>Meiko's talent tree nodes as spell metadata (id, name, icon) for display; the source the generator reads to emit the <c>MeikoTalents</c> id constants.</summary>
public class Talents
{
    /// <summary>The <c>Resonance of Earth</c> talent; its id backs the generated <c>MeikoTalents.ResonanceOfEarth</c> constant.</summary>
    public static Talent ResonanceOfEarth { get; } = new Talent { Id = 90, Name = "Resonance of Earth", Icon = "T_Nhance_RPG_Energy_19.jpg" };
    /// <summary>The <c>Perfect Storm</c> talent; its id backs the generated <c>MeikoTalents.PerfectStorm</c> constant.</summary>
    public static Talent PerfectStorm { get; } = new Talent { Id = 91, Name = "Perfect Storm", Icon = "Tex_blue_6.jpg" };
    /// <summary>The <c>Earthwell</c> talent; its id backs the generated <c>MeikoTalents.Earthwell</c> constant.</summary>
    public static Talent Earthwell { get; } = new Talent { Id = 96, Name = "Earthwell", Icon = "T_Icon_Cosmic_01.jpg" };
    /// <summary>The <c>Will of Stone</c> talent; its id backs the generated <c>MeikoTalents.WillOfStone</c> constant.</summary>
    public static Talent WillOfStone { get; } = new Talent { Id = 92, Name = "Will of Stone", Icon = "T_Meiko_SlamStun.jpg" };
    /// <summary>The <c>Harsh Winds</c> talent; its id backs the generated <c>MeikoTalents.HarshWinds</c> constant.</summary>
    public static Talent HarshWinds { get; } = new Talent { Id = 98, Name = "Harsh Winds", Icon = "Electromancer5.jpg" };
    /// <summary>The <c>Rumbling Stone</c> talent; its id backs the generated <c>MeikoTalents.RumblingStone</c> constant.</summary>
    public static Talent RumblingStone { get; } = new Talent { Id = 95, Name = "Rumbling Stone", Icon = "T_Nhance_RPG_Icons_NatureAmbush.jpg" };
    /// <summary>The <c>Guardian's Fluidity</c> talent; its id backs the generated <c>MeikoTalents.GuardiansFluidity</c> constant.</summary>
    public static Talent GuardiansFluidity { get; } = new Talent { Id = 514, Name = "Guardian's Fluidity", Icon = "T_Nhance_RPG_Icons_NatureStone.jpg" };
    /// <summary>The <c>Debilitating Vortex</c> talent; its id backs the generated <c>MeikoTalents.DebilitatingVortex</c> constant.</summary>
    public static Talent DebilitatingVortex { get; } = new Talent { Id = 97, Name = "Debilitating Vortex", Icon = "Meiko_AbilityIcon_07.jpg" };
    /// <summary>The <c>Whipping Apex</c> talent; its id backs the generated <c>MeikoTalents.WhippingApex</c> constant.</summary>
    public static Talent WhippingApex { get; } = new Talent { Id = 626, Name = "Whipping Apex", Icon = "T_Nhance_RPG_Tech_26.jpg" };
    /// <summary>The <c>Slipstream</c> talent; its id backs the generated <c>MeikoTalents.Slipstream</c> constant.</summary>
    public static Talent Slipstream { get; } = new Talent { Id = 99, Name = "Slipstream", Icon = "Meiko_AbilityIcon_10.jpg" };
    /// <summary>The <c>Magic Ward</c> talent; its id backs the generated <c>MeikoTalents.MagicWard</c> constant.</summary>
    public static Talent MagicWard { get; } = new Talent { Id = 100, Name = "Magic Ward", Icon = "T_Arcane_Scroll.jpg" };
    /// <summary>The <c>Thundering Kicks</c> talent; its id backs the generated <c>MeikoTalents.ThunderingKicks</c> constant.</summary>
    public static Talent ThunderingKicks { get; } = new Talent { Id = 93, Name = "Thundering Kicks", Icon = "Meiko_AbilityIcon_08.jpg" };
    /// <summary>The <c>Warden of the Temple</c> talent; its id backs the generated <c>MeikoTalents.WardenOfTheTemple</c> constant.</summary>
    public static Talent WardenOfTheTemple { get; } = new Talent { Id = 102, Name = "Warden of the Temple", Icon = "Meiko_AbilityIcon_01.jpg" };
    /// <summary>The <c>Earthbourne</c> talent; its id backs the generated <c>MeikoTalents.Earthbourne</c> constant.</summary>
    public static Talent Earthbourne { get; } = new Talent { Id = 103, Name = "Earthbourne", Icon = "T_Meiko_TauntTotem.jpg" };
    /// <summary>The <c>Forbidden Technique</c> talent; its id backs the generated <c>MeikoTalents.ForbiddenTechnique</c> constant.</summary>
    public static Talent ForbiddenTechnique { get; } = new Talent { Id = 104, Name = "Forbidden Technique", Icon = "Meiko_AbilityIcon_04.jpg" };
    /// <summary>The <c>Peacefield</c> talent; its id backs the generated <c>MeikoTalents.Peacefield</c> constant.</summary>
    public static Talent Peacefield { get; } = new Talent { Id = 174, Name = "Peacefield", Icon = "T_Icon_Elements_77.jpg" };
    /// <summary>The <c>Stone Guard</c> talent; its id backs the generated <c>MeikoTalents.StoneGuard</c> constant.</summary>
    public static Talent StoneGuard { get; } = new Talent { Id = 446, Name = "Stone Guard", Icon = "T_Nhance_RPG_Icons_EarthTranquility.jpg" };
    /// <summary>The <c>Conclusive Strikes</c> talent; its id backs the generated <c>MeikoTalents.ConclusiveStrikes</c> constant.</summary>
    public static Talent ConclusiveStrikes { get; } = new Talent { Id = 107, Name = "Conclusive Strikes", Icon = "T_Icon_Elements_71.jpg" };
}
