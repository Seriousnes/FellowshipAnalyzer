namespace FellowshipAnalyzer.Core.Events;

[WCLEventDiscriminator("damage")]
public record DamageEvent : Event, IAbilityEvent, IHasSourceWithInstanceEvent, IHasTargetWithInstanceEvent, IHitpointsEvent, ILocationEvent, IAdvancedDetailsEvent, IAmountEvent
{
    public virtual Unit? Actor { get; set; } = DefaultActors.Environment;
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public virtual int? SourceInstance { get; set; }
    public virtual int SourceId { get; set; }
    public virtual bool? SourceIsFriendly { get; set; }
    public virtual long HitPoints { get; set; }
    public virtual long MaxHitPoints { get; set; }
    public virtual int? TargetInstance { get; set; }
    public virtual int TargetId { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
    public virtual double X { get; set; }
    public virtual double Y { get; set; }
    public virtual double Facing { get; set; }
    public virtual MapIdEnum MapId { get; set; }
    public virtual HitTypeEnum HitType { get; set; }
    public virtual long Amount { get; set; }
    public virtual long? Absorbed { get; set; }
    public virtual ResourceActorEnum? ResourceActor { get; set; }
    public virtual List<ClassResource>? ClassResources { get; set; }
    public virtual int AttackPower { get; set; }
    public virtual int SpellPower { get; set; }
    public virtual int Armor { get; set; }
    public virtual long Absorb { get; set; }
    public virtual int ItemLevel { get; set; }
    public virtual long Mitigated { get; set; }
    public virtual long UnmitigatedAmount { get; set; }
    public virtual bool? Tick { get; set; }
    public virtual int? Overkill { get; set; }
    public virtual int? Blocked { get; set; }
    public virtual bool? SubtractsFromSupportedActor { get; set; }
}