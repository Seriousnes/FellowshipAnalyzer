namespace FellowshipAnalyzer.Core.Events;

[FSLEventDiscriminator("endchannel")]
public record EndChannelEvent : Event, IAbilityEvent
{
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public virtual int SourceId { get; set; }
    public virtual int Start { get; set; }
    public virtual int Duration { get; set; }
    public virtual BeginChannelEvent BeginChannel { get; set; }
}