namespace FellowshipAnalyzer.Core.Events;

public record EndChannelEvent : Event
{
    public virtual Ability Ability { get; set; }
    public virtual int SourceId { get; set; }
    public virtual int Start { get; set; }
    public virtual int Duration { get; set; }
    public virtual BeginChannelEvent BeginChannel { get; set; }
}