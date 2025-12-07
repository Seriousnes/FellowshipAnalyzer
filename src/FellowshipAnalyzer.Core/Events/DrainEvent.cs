namespace FellowshipAnalyzer.Core.Events;

public record DrainEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetEvent, ILocationEvent, IAdvancedDetailsEvent, IHitpointsEvent
{
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public virtual int SourceId { get; set; }
    public virtual bool? SourceIsFriendly { get; set; }
    public virtual int TargetId { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
    public virtual double ResourceChange { get; set; }
    public virtual int ResourceChangeType { get; set; }
    public virtual int OtherResourceChange { get; set; }
    /// <summary>
    /// Shows whether the source or the target is being referred to by the classResources, hitPoints, etc. See <see cref="ClassResources"/>.
    /// </summary>
    public virtual ResourceActorEnum ResourceActor { get; set; }
    /// <summary>
    /// A list of resources on either the source or target, depending on <see cref="ResourceActor"/> choice.
    /// </summary>
    public List<ClassResource> ClassResources { get; set; }
    public virtual double X { get; set; }
    public virtual double Y { get; set; }
    public virtual double Facing { get; set; }
    public virtual MapIdEnum MapId { get; set; }
    public virtual int AttackPower { get; set; }
    public virtual int SpellPower { get; set; }
    public virtual int Armor { get; set; }
    public virtual long HitPoints { get; set; }
    public virtual long MaxHitPoints { get; set; }
    public virtual int ItemLevel { get; set; }
}
