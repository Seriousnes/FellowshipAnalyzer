namespace FellowshipAnalyzer.Core.Common.Spells.Elarion;

public partial class Spells : ISpellRegistry
{
    public static Spell Multishot { get; } = new(1318, "Multishot", "Bowguy_Multishot.jpg");
    public static Spell HeartseekerBarrage { get; } = new(1312, "Heartseeker Barrage", "Bowguy_Spray.jpg");
    public static Spell FocusedShot { get; } = new(1315, "Focused Shot", "Bowguy_FocusShot.jpg");
    public static Spell HighwindArrow { get; } = new(1313, "Highwind Arrow", "Bowguy_Ricochet.jpg");
    public static Spell CelestialShot { get; } = new(1301, "Celestial Shot", "Bowguy_Shot.jpg");
    public static Spell StarfallVolley { get; } = new(126, "Starfall Volley", "Bowguy_Rain.jpg");
    public static Spell SkystridersSupremacy { get; } = new(1398, "Skystrider's Supremacy", "Bowguy_Supremacy.jpg");
    public static Spell LunarlightMark { get; } = new(1310, "Lunarlight Mark", "Bowguy_Mark.jpg");
    public static Spell Roll { get; } = new(1311, "Roll", "Bowguy_Abilityicon_Roll.jpg");
    public static Spell Disrupt { get; } = new(1308, "Disrupt", "Bowguy_Interrupt.jpg");
    public static Spell PathfindersResillience { get; } = new(1302, "Pathfinder's Resilience", "Bowguy_Abilityicon_Defensive.jpg");
    public static Spell SkystridersGrace { get; } = new(1304, "Skystrider's Grace", "Bowguy_HasteBuff.jpg");
    public static Spell EventHorizon { get; } = new(1584, "Event Horizon", "Bowguy_Spirit.jpg");
    public static Spell GrapplingArrow { get; } = new(125, "Grappling Arrow", "Bowguy_GrappleShot.jpg");
    public static Spell VoidbringerTouch { get; } = new(155, "Voidbringer's Touch", "T_Weapon_VoidTouch.jpg");

    public static Effect EventHorizonBuff { get; } = new(2312, "Event Horizon", "");
    public static Effect SkystridersGraceBuff { get; } = new(1869, "Skystrider's Grace", "");
    public static Effect CelestialImpetus { get; } = new(1867, "", "");
    public static Effect ImpendingHeartseeker { get; } = new(2317, "", "");
    public static Effect SpiritOfHeroism { get; } = new(2253, "Spirit of Heroism", "Tex_b_02.jpg");

    public static Effect EmpoweredMultishotBuff { get; } = new(-1, "Empowered Multishot", "");
}
