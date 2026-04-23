namespace FellowshipAnalyzer.Core.Events;

[Fabricated]
public class PhaseEvent : Event
{
    public virtual PhaseConfig Phase { get; set; }
    public override bool? Fabricated => true;
}

public class PhaseConfig
{
    public string Name { get; set; }
    public string Key { get; set; }
    public List<int> Difficulties { get; set; } = [];
    public IPhaseFilter? Filter { get; set; }
    public bool? Multiple { get; set; }
    public int? Instance { get; set; }
    public bool? Intermission { get; set; }
}

public abstract class PhaseFilter<T> where T : Event
{
    public T Type { get; set; }
    public int? EventInstance { get; set; }
}

public class HealthPhaseFilter : PhaseFilter<HealthEvent>, IPhaseFilter<HealthEvent>
{
    public virtual int Guid { get; set; }
    public int Health { get; set; }
}

public abstract class AbilityPhaseFilter<T> : PhaseFilter<T>, IPhaseFilter<T>
    where T : Event
{
    public Ability Ability { get; set; }
}

public class ApplyBuffPhaseFilter : AbilityPhaseFilter<ApplyBuffEvent> { }
public class RemoveBuffPhaseFilter : AbilityPhaseFilter<RemoveBuffEvent> { }
public class ApplyDebuffPhaseFilter : AbilityPhaseFilter<ApplyDebuffEvent> { }
public class CastPhaseFilter : AbilityPhaseFilter<CastEvent> { }
public class BeginCastPhaseFilter : AbilityPhaseFilter<BeginCastEvent> { }