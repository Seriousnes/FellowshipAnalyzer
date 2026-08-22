namespace FellowshipAnalyzer.Core.Game;

/// <summary>
/// The damage school an ability deals in, resolved at compile time from <c>data/spelldb.json</c>.
/// These two members are the analyzer's complete vocabulary. The game-data export writes a school
/// and an optional subschool, such as <c>Magic / Fire</c> or <c>Physical / Bleed</c>, and lists an
/// ability that deals both as two entries; the merge maps each entry onto the member its leading
/// school names and discards the subschool. <c>spelldb.json</c> writes the result slash-joined, so an
/// ability that deals both reads <c>Magic/Physical</c>.
/// <para>
/// The default value names no member and means the school is unresolved: an ability the export does
/// not classify. A school test therefore fails closed rather than reading as Physical.
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
