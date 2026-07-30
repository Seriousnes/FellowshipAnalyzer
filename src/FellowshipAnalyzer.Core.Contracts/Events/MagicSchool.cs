namespace FellowshipAnalyzer.Core.Events;

/// <summary>
/// The damage school an ability deals in. <see cref="Physical"/> and <see cref="Magic"/> come from the
/// game data dump, which carries <c>Physical</c>, <c>Magic</c>, or <c>Magic/Physical</c> for an ability
/// that deals both.
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
    /// <summary>Healing, the value carried by the healing abilities in older report master data.</summary>
    Healing = 8,
    /// <summary>Stagger damage.</summary>
    Stagger = 1024,
}
