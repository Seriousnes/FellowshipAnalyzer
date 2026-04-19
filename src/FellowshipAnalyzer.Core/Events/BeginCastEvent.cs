namespace FellowshipAnalyzer.Core.Events;

[FSLEventDiscriminator("begincast")]
public record BeginCastEvent : Event, IAbilityEvent
{
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public virtual CastEvent? CastEvent { get; set; }
    public virtual BeginChannelEvent? Channel { get; set; }
    public virtual bool IsCancelled { get; set; }
    public virtual int SourceId { get; set; }
    public virtual bool? SourceIsFriendly { get; set; }
    public virtual ICastTarget? Target { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
}
