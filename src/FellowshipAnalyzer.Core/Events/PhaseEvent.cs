namespace FellowshipAnalyzer.Core.Events;

/// <summary>Marks a boss phase transition inside a pull, fabricated when the event stream satisfies a <see cref="PhaseConfig"/>'s <see cref="PhaseConfig.Filter"/>.</summary>
[Fabricated]
public class PhaseEvent : Event
{
    /// <summary>The phase configuration that was matched to produce this event.</summary>
    public virtual PhaseConfig Phase { get; set; }
    /// <inheritdoc/>
    public override bool? Fabricated => true;
}

/// <summary>Describes one boss phase and how to detect its transition in the event stream.</summary>
public class PhaseConfig
{
    /// <summary>The display name of the phase.</summary>
    public string Name { get; set; }
    /// <summary>The short, unique identifier used to key this phase's data.</summary>
    public string Key { get; set; }
    /// <summary>The difficulty tier ids this phase config applies to.</summary>
    public List<int> Difficulties { get; set; } = [];
    /// <summary>The criteria used to detect when this phase begins.</summary>
    public IPhaseFilter? Filter { get; set; }
    /// <summary>Whether this phase can occur more than once within a pull.</summary>
    public bool? Multiple { get; set; }
    /// <summary>Which occurrence of a repeating phase this config represents, when <see cref="Multiple"/> is set.</summary>
    public int? Instance { get; set; }
    /// <summary>Whether this phase is an intermission rather than a main boss phase.</summary>
    public bool? Intermission { get; set; }
}

/// <summary>Base config for detecting a phase transition from an event of type <typeparamref name="T"/>.</summary>
public abstract class PhaseFilter<T> where T : Event
{
    /// <summary>The event that this filter matches against to detect the phase transition.</summary>
    public T Type { get; set; }
    /// <summary>Which occurrence of the matching event triggers the phase, when more than one can match.</summary>
    public int? EventInstance { get; set; }
}

/// <summary>Detects a phase transition from a unit's health crossing a threshold.</summary>
public class HealthPhaseFilter : PhaseFilter<HealthEvent>, IPhaseFilter<HealthEvent>
{
    /// <summary>The guid of the unit whose health is being watched.</summary>
    public virtual int Guid { get; set; }
    /// <summary>The health value that triggers the phase transition.</summary>
    public int Health { get; set; }
}

/// <summary>Base config for detecting a phase transition from an ability-related event of type <typeparamref name="T"/>.</summary>
public abstract class AbilityPhaseFilter<T> : PhaseFilter<T>, IPhaseFilter<T>
    where T : Event
{
    /// <summary>The ability whose event marks the phase transition.</summary>
    public Ability Ability { get; set; }
}

/// <summary>Detects a phase transition when the configured <see cref="AbilityPhaseFilter{T}.Ability"/> is buffed onto its target.</summary>
public class ApplyBuffPhaseFilter : AbilityPhaseFilter<ApplyBuffEvent> { }
/// <summary>Detects a phase transition when the configured <see cref="AbilityPhaseFilter{T}.Ability"/> buff is removed from its target.</summary>
public class RemoveBuffPhaseFilter : AbilityPhaseFilter<RemoveBuffEvent> { }
/// <summary>Detects a phase transition when the configured <see cref="AbilityPhaseFilter{T}.Ability"/> is applied as a debuff.</summary>
public class ApplyDebuffPhaseFilter : AbilityPhaseFilter<ApplyDebuffEvent> { }
/// <summary>Detects a phase transition when the configured <see cref="AbilityPhaseFilter{T}.Ability"/> finishes casting.</summary>
public class CastPhaseFilter : AbilityPhaseFilter<CastEvent> { }
/// <summary>Detects a phase transition when the configured <see cref="AbilityPhaseFilter{T}.Ability"/> begins casting.</summary>
public class BeginCastPhaseFilter : AbilityPhaseFilter<BeginCastEvent> { }