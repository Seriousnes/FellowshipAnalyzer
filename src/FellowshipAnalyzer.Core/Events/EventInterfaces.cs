namespace FellowshipAnalyzer.Core.Events;

public interface IAbilityEvent
{
    Ability Ability { get; set; }
    int AbilityGameId { get; set; }
}

public interface IExtraAbilityEvent
{
    Ability ExtraAbility { get; set; }
    int ExtraAbilityGameId { get; set; }
}

public interface IAmountEvent
{
    long Amount { get; set; }
}

public interface ISpellPowerEvent
{
    int SpellPower { get; set; }
}

public interface IHasSourceEvent
{
    int SourceId { get; set; }
    bool? SourceIsFriendly { get; set; }
}

public interface IHasSourceWithInstanceEvent : IHasSourceEvent
{
    int? SourceInstance { get; set; }
}

public interface IHasTargetEvent
{
    int TargetId { get; set; }
    bool? TargetIsFriendly { get; set; }
}

public interface IHasTargetWithInstanceEvent : IHasTargetEvent
{
    int? TargetInstance { get; set; }
}

public interface IHitpointsEvent
{
    long HitPoints { get; set; }
    long MaxHitPoints { get; set; }
}

public interface ILocationEvent
{
    double X { get; set; }
    double Y { get; set; }
    double Facing { get; set; }
    MapIdEnum MapId { get; set; }
}

public interface IAdvancedDetailsEvent
{
    int AttackPower { get; set; }
    int SpellPower { get; set; }
    int Armor { get; set; }
    int ItemLevel { get; set; }
}

public interface ICastTarget
{
    string Name { get; set; }
    int Id { get; set; }
    int Guid { get; set; }
    string Type { get; set; }
    string Icon { get; set; }
}

public interface IBuffStackEvent
{
    int Stack { get; set; }
}

public interface ICooldownTriggerEvent
{
}

public interface IPhaseFilter { }
public interface IPhaseFilter<T> : IPhaseFilter where T : Event
{
    T Type { get; set; }
}