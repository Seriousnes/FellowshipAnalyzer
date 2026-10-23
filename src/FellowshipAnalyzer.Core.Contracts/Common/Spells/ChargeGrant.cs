namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>What the player holds to receive a <see cref="ChargeGrant"/>.</summary>
public enum GrantSource
{
    /// <summary>A selected talent, named by its native talent id.</summary>
    Talent,

    /// <summary>An equipped legendary item, named by its item id.</summary>
    LegendaryItem,
}

/// <summary>
/// The charges a talent or legendary gives an ability, and where Haste enters the ability's cooldown
/// while the player holds it. The game-data export names the grant in <c>cooldownHasteGrantedBy</c>.
/// </summary>
public sealed record ChargeGrant
{
    /// <summary>Whether <see cref="Id"/> names a talent or a legendary item.</summary>
    public GrantSource Source { get; init; }

    /// <summary>The native talent id or the legendary item id.</summary>
    public int Id { get; init; }

    /// <summary>The ability's charges while the player holds the grant.</summary>
    public int Charges { get; init; }

    /// <summary>Where Haste enters the cooldown while the player holds the grant.</summary>
    public CooldownHasteScaling CooldownHaste { get; init; }
}
