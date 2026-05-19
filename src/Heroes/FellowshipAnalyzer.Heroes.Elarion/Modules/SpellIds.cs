namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Compile-time integer GUID constants for Elarion spells. The values mirror
/// <see cref="Core.Common.Spells.Spell.Guid"/> so they can be used in
/// <see cref="Core.Analysis.OnAttribute{TEvent}"/> arguments,
/// which require compile-time constants.
/// Effect GUIDs follow the <c>1_000_000 + Id</c> encoding used by the combat-log API.
/// </summary>
internal static class SpellIds
{
    public const int Multishot = 1318;
    public const int HeartseekerBarrage = 1312;
    public const int FocusedShot = 1315;
    public const int HighwindArrow = 1313;
    public const int CelestialShot = 1301;
    public const int StarfallVolley = 126;
    public const int SkystridersSupremacy = 1398;
    public const int LunarlightMark = 1310;
    public const int SkystridersGrace = 1304;
    public const int EventHorizon = 1584;
    public const int VoidbringerTouch = 155;

    public const int EventHorizonBuff = 1_002_312;
    public const int SkystridersGraceBuff = 1_001_869;
    public const int CelestialImpetus = 1_001_867;
    public const int ImpendingHeartseekerBuff = 1_002_317;
    public const int SpiritOfHeroismBuff = 1_002_253;
}
