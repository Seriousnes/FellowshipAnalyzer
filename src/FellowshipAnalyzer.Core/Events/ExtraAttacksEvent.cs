namespace FellowshipAnalyzer.Core.Events;

public record ExtraAttacksEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetEvent
{
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public virtual int SourceId { get; set; }
    public virtual bool? SourceIsFriendly { get; set; }
    public virtual int? SourceMarker { get; set; }
    public virtual int TargetId { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
    public virtual int? TargetMarker { get; set; }
    public virtual int Fight { get; set; }
    public virtual int ExtraAttacks { get; set; }
}

public record ResourceChangeEvent : Event, IAbilityEvent, IHasSourceEvent, IHasTargetEvent, IHitpointsEvent, ILocationEvent, IAdvancedDetailsEvent
{
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public virtual int SourceId { get; set; }
    public virtual bool? SourceIsFriendly { get; set; }
    public virtual int TargetId { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
    /// <summary>
    /// The id for the resource. See the <see cref="Resource"/> file for all available resource types.
    /// </summary>
    public virtual int ResourceChangeType { get; set; }
    /// <summary>
    /// The amount of resource gained. This includes any wasted gain, see <see cref="Waste"/>
    /// </summary>
    public virtual double ResourceChange { get; set; }
    /// <summary>
    /// The amount of wasted resource gain (overcapped).
    /// </summary>
    public virtual double Waste { get; set; }
    public virtual double OtherResourceChange { get; set; } = 0;
    /// <summary>
    /// Shows whether the source or the target is being referred to by the classResources, hitPoints, etc. See <see cref="ClassResources"/>.
    /// </summary>
    public virtual ResourceActorEnum ResourceActor { get; set; }
    /// <summary>
    /// A list of resources on either the source or target, depending on <see cref="ResourceActor"/> choice.
    /// </summary>
    public List<ClassResource> ClassResources { get; set; }
    public virtual long HitPoints { get; set; }
    public virtual long MaxHitPoints { get; set; }
    public virtual double X { get; set; }
    public virtual double Y { get; set; }
    public virtual double Facing { get; set; }
    public virtual MapIdEnum MapId { get; set; }
    public virtual int ItemLevel { get; set; }
    public virtual int AttackPower { get; set; }
    public virtual int SpellPower { get; set; }
    public virtual int Armor { get; set; }
}