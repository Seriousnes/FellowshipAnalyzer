using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Fabricated when an ability cast consumes resource, pairing the spend with the ability that caused it.</summary>
[Fabricated]
public class SpendResourceEvent : Event, IAbilityEvent
{
    /// <summary>Unique identifier for the unit that spent the resource.</summary>
    public int SourceId { get; set; }
    /// <summary>The amount of resource spent.</summary>
    public int ResourceChange { get; set; }
    /// <summary>The id of the resource type spent.</summary>
    public int ResourceChangeType { get; set; }
    /// <summary>The ability that consumed the resource.</summary>
    public Ability Ability { get; set; }
    /// <summary>The <see cref="FSLID"/> of <see cref="Ability"/>.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <inheritdoc/>
    public override bool? Fabricated => true;
}
