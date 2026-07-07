namespace FellowshipAnalyzer.Core.Events;

[Fabricated]
public abstract class ChangeStackEvent : BuffEvent
{
    public record class History(int Stacks, long Timestamp);

    public virtual int? End { get; set; }
    public virtual bool? IsDebuff { get; set; }
    public virtual int NewStacks { get; set; }
    public virtual int OldStacks { get; set; }
    public virtual int? Stack { get; set; }
    public virtual List<History> StackHistory { get; set; } = [];
    public virtual int Stacks { get; set; }
    public virtual int StacksGained { get; set; }
    public virtual int Start { get; set; }
    public override bool? Fabricated => true;
}

public class ChangeBuffStackEvent : ChangeStackEvent { }

public class ChangeDebuffStackEvent : ChangeStackEvent { }