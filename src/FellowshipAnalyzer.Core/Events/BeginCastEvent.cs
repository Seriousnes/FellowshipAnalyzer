using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Core.Events;

/// <summary>Fired when an actor begins casting an ability that has a cast time.</summary>
public class BeginCastEvent : Event, IAbilityEvent, IHasSourceEvent
{
    /// <summary>The ability being cast.</summary>
    public virtual Ability Ability { get; set; }
    /// <summary>The FSLID of the ability being cast.</summary>
    public virtual FSLID AbilityGameId { get; set; }
    /// <summary>The completed cast, once this begin-cast finishes casting.</summary>
    public virtual CastEvent? CastEvent { get; set; }
    /// <summary>The channel this begin-cast started, if the ability is channeled rather than cast normally.</summary>
    public virtual BeginChannelEvent? Channel { get; set; }
    /// <summary>Whether the cast was cancelled before it could complete.</summary>
    public virtual bool IsCancelled { get; set; }
    /// <summary>The id of the actor performing the cast.</summary>
    public virtual int SourceId { get; set; }
    /// <summary>The target of the cast.</summary>
    public virtual ICastTarget? Target { get; set; }
}
