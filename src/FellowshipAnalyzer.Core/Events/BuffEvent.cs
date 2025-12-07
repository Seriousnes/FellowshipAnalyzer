namespace FellowshipAnalyzer.Core.Events;

public abstract class BuffEvent : Event, IAbilityEvent, IHasTargetWithInstanceEvent, IHasSourceWithInstanceEvent
{
    public virtual Ability Ability { get; set; } = new();
    public virtual int AbilityGameId { get; set; }
    public virtual int? TargetInstance { get; set; }
    public virtual int TargetId { get; set; }
    public virtual int? SourceInstance { get; set; }
    public virtual int SourceId { get; set; }
}

[Fabricated]
public class TrackedBuffEvent : BuffEvent
{
    public int Start { get; set; }
    public int? End { get; set; }
    public List<StackHistoryElement> StackHistory { get; set; } = [];
    public List<int> RefreshHistory { get; set; } = [];
    public int Stacks { get; set; }
    public bool IsDebuff { get; set; }
    public override bool? Fabricated => true;

    public class StackHistoryElement
    {
        public int Stacks { get; set; }
        public int Timestamp { get; set; }
    }
}

#region Buffs

public class ApplyBuffEvent : BuffEvent
{
    public virtual int? Absorb { get; set; }
    public virtual bool? FromCombatantInfo { get; set; }
}

public class ApplyBuffStackEvent : BuffEvent, IBuffStackEvent
{
    public virtual int Stack { get; set; }
}

public class RemoveBuffEvent : BuffEvent
{
    public virtual int? Absorb { get; set; }
}

public class RemoveBuffStackEvent : BuffEvent, IBuffStackEvent
{
    public virtual int Stack { get; set; }
}

public class RefreshBuffEvent : BuffEvent
{
    public virtual int? Absorb { get; set; }
    public virtual ICastTarget? Source { get; set; }
}

#endregion


#region Debuffs

public class ApplyDebuffEvent : BuffEvent
{
    public virtual Unit? Source { get; set; } = DefaultActors.Environment;
    public virtual int? Absorb { get; set; }
    public virtual bool? FromCombatantInfo { get; set; }
}

public class ApplyDebuffStackEvent : BuffEvent, IBuffStackEvent
{
    public virtual int Stack { get; set; }
}

public class RemoveDebuffEvent : BuffEvent
{
    public virtual Unit? Source { get; set; } = DefaultActors.Environment;
    public virtual int? Absorb { get; set; }
}

public class RemoveDebuffStackEvent : BuffEvent, IBuffStackEvent
{
    public virtual int Stack { get; set; }
}

public class RefreshDebuffEvent : BuffEvent
{
    public virtual ICastTarget? Source { get; set; }
}

#endregion
