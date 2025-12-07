namespace FellowshipAnalyzer.Core.Events;

[Fabricated]
public record ChangeStatsEvent : Event
{
    public virtual int SourceId { get; set; }
    public virtual int TargetId { get; set; }
    public virtual bool? TargetIsFriendly => true;
    public virtual Stats After { get; set; }
    public virtual Stats Before { get; set; }
    public virtual Stats Delta { get; set; }
    public override bool? Fabricated => true;
}

public record ChangeHasteEvent : ChangeStatsEvent
{
    public virtual double? OldHaste { get; set; }
    public virtual double? NewHaste { get; set; }
}