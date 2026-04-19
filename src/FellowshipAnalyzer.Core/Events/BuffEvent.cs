namespace FellowshipAnalyzer.Core.Events;

public abstract record BuffEvent : Event, IAbilityEvent, IHasTargetWithInstanceEvent, IHasSourceWithInstanceEvent
{
    public virtual Ability Ability { get; set; }
    public virtual int AbilityGameId { get; set; }
    public virtual int? TargetInstance { get; set; }
    public virtual int TargetId { get; set; }
    public virtual bool? TargetIsFriendly { get; set; }
    public virtual int? SourceInstance { get; set; }
    public virtual int SourceId { get; set; }
    public virtual bool? SourceIsFriendly { get; set; }
}

[Fabricated]
public record TrackedBuffEvent : BuffEvent
{
    public int Start { get; set; }
    public int? End { get; set; }
    public List<StackHistoryElement> StackHistory { get; set; } = [];
    public List<int> RefreshHistory { get; set; } = [];
    public int Stacks { get; set; }
    public bool IsDebuff { get; set; }
    public override bool? Fabricated => true;

    public record StackHistoryElement
    {
        public int Stacks { get; set; }
        public int Timestamp { get; set; }
    }
}

#region Buffs

[FSLEventDiscriminator("applybuff")]
public record ApplyBuffEvent : BuffEvent
{
    public virtual int? Absorb { get; set; }
    public virtual bool? FromCombatantInfo { get; set; }
}

[FSLEventDiscriminator("applybuffstack")]
public record ApplyBuffStackEvent : BuffEvent, IBuffStackEvent
{
    public virtual int Stack { get; set; }
}

[FSLEventDiscriminator("removebuff")]
public record RemoveBuffEvent : BuffEvent
{
    public virtual int? Absorb { get; set; }
}

[FSLEventDiscriminator("removebuffstack")]
public record RemoveBuffStackEvent : BuffEvent, IBuffStackEvent
{
    public virtual int Stack { get; set; }
}

[FSLEventDiscriminator("refreshbuff")]
public record RefreshBuffEvent : BuffEvent
{
    public virtual int? Absorb { get; set; }
    public virtual ICastTarget? Source { get; set; }
}

#endregion


#region Debuffs

[FSLEventDiscriminator("applydebuff")]
public record ApplyDebuffEvent : BuffEvent
{
    public virtual Unit? Source { get; set; } = DefaultActors.Environment;
    public virtual int? Absorb { get; set; }
    public virtual bool? FromCombatantInfo { get; set; }
}

[FSLEventDiscriminator("applydebuffstack")]
public record ApplyDebuffStackEvent : BuffEvent, IBuffStackEvent
{
    public virtual int Stack { get; set; }
}

[FSLEventDiscriminator("removedebuff")]
public record RemoveDebuffEvent : BuffEvent
{
    public virtual Unit? Source { get; set; } = DefaultActors.Environment;
    public virtual int? Absorb { get; set; }
}

[FSLEventDiscriminator("removedebuffstack")]
public record RemoveDebuffStackEvent : BuffEvent, IBuffStackEvent
{
    public virtual int Stack { get; set; }
}

[FSLEventDiscriminator("refreshdebuff")]
public record RefreshDebuffEvent : BuffEvent
{
    public virtual ICastTarget? Source { get; set; }
}

#endregion
