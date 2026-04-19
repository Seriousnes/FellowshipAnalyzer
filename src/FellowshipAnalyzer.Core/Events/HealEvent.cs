namespace FellowshipAnalyzer.Core.Events;

[FSLEventDiscriminator("heal")]
public record HealEvent : Event, IAbilityEvent, IHasSourceWithInstanceEvent, IHasTargetWithInstanceEvent, IHitpointsEvent, ILocationEvent, IAdvancedDetailsEvent, IAmountEvent, ISpellPowerEvent
{
    /// <summary>
    /// Unique Identifier for the source. Nobody else will have this ID
    /// </summary>
    public virtual int SourceId { get; set; }
    public virtual int? SourceInstance { get; set; }
    /// <summary>
    /// If the person who is doing the healing friendly
    /// </summary>
    public virtual bool? SourceIsFriendly { get; set; }
    /// <summary>
    /// Unique Identifier for the target. Nobody else will have this ID
    /// </summary>
    public virtual int TargetId { get; set; }
    public virtual int? TargetInstance { get; set; }
    /// <summary>
    /// Is the target you're healing a friendly
    /// </summary>
    public virtual bool? TargetIsFriendly { get; set; }
    /// <summary>
    /// The ability that is healing the target
    /// </summary>
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    /// <summary>
    /// This describes if the spell Hit/Missed/Crit/etc. Look at <see cref="HitTypeEnum"/> all types of hits
    /// </summary>
    public virtual HitTypeEnum HitType { get; set; }
    /// <summary>
    /// The effective healing the event did
    /// </summary>
    public virtual long Amount { get; set; }
    /// <summary>
    /// The overheal the event did
    /// </summary>
    public virtual long? Overheal { get; set; }
    /// <summary>
    /// If the event is a tick of a HoT or HoT like object
    /// </summary>
    public virtual bool? Tick { get; set; }
    public virtual ResourceActorEnum ResourceActor { get; set; }
    /// <summary>
    /// A list of resources on the target
    /// </summary>
    public virtual List<ClassResource>? ClassResources { get; set; }
    /// <summary>
    /// Hit points of the target AFTER the heal is done if you want hp before you need to do (hitpoints - amount)
    /// </summary>
    public virtual long HitPoints { get; set; }
    /// <summary>
    /// The max hitpoints of the target
    /// </summary>
    public virtual long MaxHitPoints { get; set; }
    /// <summary>
    /// How much attack power the target has
    /// </summary>
    public virtual int AttackPower { get; set; }
    /// <summary>
    /// How much Spell power the target has
    /// </summary>
    public virtual int SpellPower { get; set; }
    /// <summary>
    /// How much Armor the target has
    /// </summary>
    public virtual int Armor { get; set; }
    /// <summary>
    /// The current total absorb shields on the target
    /// </summary>
    public virtual long Absorb { get; set; }
    /// <summary>
    /// The amount of healing absorbed by a healing taken-debuff
    /// </summary>
    public virtual long? Absorbed { get; set; }
    /// <summary>
    /// The x location of the player
    /// </summary>
    public virtual double X { get; set; }
    /// <summary>
    /// The y location of the player
    /// </summary>
    public virtual double Y { get; set; }
    /// <summary>
    /// The direction the plaeyr is facing
    /// </summary>
    public virtual double Facing { get; set; }
    /// <summary>
    /// The map they are in. This is a unique ID for every zone in wow
    /// </summary>
    public virtual MapIdEnum MapId { get; set; }
    /// <summary>
    /// The Item level of the target 
    /// </summary>
    public virtual int ItemLevel { get; set; }
}

public record BeaconHealEvent : HealEvent
{
    public virtual HealEvent OriginalHeal { get; set; }
}

public record BeaconTransferFailedEvent : HealEvent { }

public record FeedHealEvent : HealEvent
{
    public virtual int Feed { get; set; }
}