namespace FellowshipAnalyzer.Core.Game;

/// <summary>
/// The damage school an ability deals in, resolved at compile time from <c>data/spelldb.json</c>.
/// These two members are the complete vocabulary the game data dump carries, which writes
/// <c>Physical</c>, <c>Magic</c>, or <c>Magic/Physical</c> for an ability that deals both.
/// <para>
/// The default value names no member and means the school is unresolved: an ability the dump does not
/// classify. A school test therefore fails closed rather than reading as Physical.
/// </para>
/// </summary>
[Flags]
public enum MagicSchool
{
    /// <summary>Damage armour and Toughness reduce.</summary>
    Physical = 1,
    /// <summary>Damage resistance reduces.</summary>
    Magic = 2,
}
