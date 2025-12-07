namespace FellowshipAnalyzer.Core.Events;

[Fabricated]
public record PhaseEvent : Event
{
    public virtual PhaseConfig Phase { get; set; }
    public override bool? Fabricated => true;
}

public record PhaseConfig
{
    public string Name { get; set; }
    public string Key { get; set; }
    public List<int> Difficulties { get; set; } = [];
    public IPhaseFilter? Filter { get; set; }
    public bool? Multiple { get; set; }
    public int? Instance { get; set; }
    public bool? Intermission { get; set; }
}

public abstract record PhaseFilter<T> where T : Event
{
    public T Type { get; set; }
    public int? EventInstance { get; set; }
}

public record HealthPhaseFilter : PhaseFilter<HealthEvent>, IPhaseFilter<HealthEvent>
{
    public virtual int Guid { get; set; }
    public int Health { get; set; }
}

public abstract record AbilityPhaseFilter<T> : PhaseFilter<T>, IPhaseFilter<T>
    where T : Event
{
    public Ability Ability { get; set; }
}

public record ApplyBuffPhaseFilter : AbilityPhaseFilter<ApplyBuffEvent> { }
public record RemoveBuffPhaseFilter : AbilityPhaseFilter<RemoveBuffEvent> { }
public record ApplyDebuffPhaseFilter : AbilityPhaseFilter<ApplyDebuffEvent> { }
public record CastPhaseFilter : AbilityPhaseFilter<CastEvent> { }
public record BeginCastPhaseFilter : AbilityPhaseFilter<BeginCastEvent> { }