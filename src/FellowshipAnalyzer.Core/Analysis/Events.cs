using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Fluent entry points for building <see cref="EventFilter{T}"/> instances, one static property per
/// concrete <see cref="Event"/> subtype. Chain <see cref="EventFilter{T}.By"/>,
/// <see cref="EventFilter{T}.To"/>, <see cref="EventFilter{T}.Spell"/>, or
/// <see cref="EventFilter{T}.ExtraSpell"/> and pass the result to
/// <see cref="EventSubscriber.AddEventListener{T}(EventFilter{T}, Action{T})"/>.
/// </summary>
public static class Events
{
    /// <summary>Matches every event regardless of type, for subscriptions that filter only by <see cref="EventFilter{T}.By"/> or <see cref="EventFilter{T}.To"/>.</summary>
    public static AnyEventFilter Any => new();
    /// <summary>Matches <see cref="AbsorbedEvent"/>s, raised when damage or healing is absorbed by a shield.</summary>
    public static EventFilter<AbsorbedEvent> Absorbed => new();
    /// <summary>Matches <see cref="ApplyBuffEvent"/>s, raised when a buff is first applied to a unit.</summary>
    public static EventFilter<ApplyBuffEvent> ApplyBuff => new();
    /// <summary>Matches <see cref="ApplyBuffStackEvent"/>s, raised when an additional stack of a buff is applied.</summary>
    public static EventFilter<ApplyBuffStackEvent> ApplyBuffStack => new();
    /// <summary>Matches <see cref="ApplyDebuffEvent"/>s, raised when a debuff is first applied to a unit.</summary>
    public static EventFilter<ApplyDebuffEvent> ApplyDebuff => new();
    /// <summary>Matches <see cref="ApplyDebuffStackEvent"/>s, raised when an additional stack of a debuff is applied.</summary>
    public static EventFilter<ApplyDebuffStackEvent> ApplyDebuffStack => new();
    /// <summary>Matches <see cref="BeginCastEvent"/>s, raised when a unit starts casting an ability that has a cast time.</summary>
    public static EventFilter<BeginCastEvent> BeginCast => new();
    /// <summary>Matches <see cref="BeginChannelEvent"/>s, raised when a unit starts channeling an ability.</summary>
    public static EventFilter<BeginChannelEvent> BeginChannel => new();
    /// <summary>Matches <see cref="EndChannelEvent"/>s, raised when a channeled ability finishes or is cut short.</summary>
    public static EventFilter<EndChannelEvent> EndChannel => new();
    /// <summary>Matches <see cref="CastEvent"/>s, raised when an ability finishes casting.</summary>
    public static EventFilter<CastEvent> Cast => new();
    /// <summary>Matches <see cref="ChangeBuffStackEvent"/>s, raised when a buff's stack count changes.</summary>
    public static EventFilter<ChangeBuffStackEvent> ChangeBuffStack => new();
    /// <summary>Matches <see cref="ChangeDebuffStackEvent"/>s, raised when a debuff's stack count changes.</summary>
    public static EventFilter<ChangeDebuffStackEvent> ChangeDebuffStack => new();
    /// <summary>Matches <see cref="ChangeCooldownModifierEvent"/>s, fabricated when a cooldown-rate modifier is added to or removed from a tracked stat pool.</summary>
    public static EventFilter<ChangeCooldownModifierEvent> ChangeCooldownModifier => new();
    /// <summary>Matches <see cref="ChangeHasteEvent"/>s, raised when the player's haste stat changes.</summary>
    public static EventFilter<ChangeHasteEvent> ChangeHaste => new();
    /// <summary>Matches <see cref="ChangeStatsEvent"/>s, raised when one of the player's tracked combat stats changes.</summary>
    public static EventFilter<ChangeStatsEvent> ChangeStats => new();
    /// <summary>Matches <see cref="DamageEvent"/>s, raised on every damaging hit.</summary>
    public static EventFilter<DamageEvent> Damage => new();
    /// <summary>Matches <see cref="DeathEvent"/>s, raised when a unit dies.</summary>
    public static EventFilter<DeathEvent> Death => new();
    /// <summary>Matches <see cref="DispelEvent"/>s, raised when a buff or debuff is dispelled.</summary>
    public static EventFilter<DispelEvent> Dispel => new();
    /// <summary>Matches <see cref="FightEndEvent"/>s, fabricated when a pull ends.</summary>
    public static EventFilter<FightEndEvent> FightEnd => new();
    /// <summary>Matches <see cref="FightStartEvent"/>s, fabricated when a pull starts.</summary>
    public static EventFilter<FightStartEvent> FightStart => new();
    /// <summary>Matches <see cref="HealEvent"/>s, raised on every healing tick.</summary>
    public static EventFilter<HealEvent> Heal => new();
    /// <summary>Matches <see cref="InterruptEvent"/>s, raised when a cast or channel is interrupted.</summary>
    public static EventFilter<InterruptEvent> Interrupt => new();
    /// <summary>Matches <see cref="RefreshBuffEvent"/>s, raised when a buff's duration is refreshed without changing its stack count.</summary>
    public static EventFilter<RefreshBuffEvent> RefreshBuff => new();
    /// <summary>Matches <see cref="RefreshDebuffEvent"/>s, raised when a debuff's duration is refreshed without changing its stack count.</summary>
    public static EventFilter<RefreshDebuffEvent> RefreshDebuff => new();
    /// <summary>Matches <see cref="RemoveBuffEvent"/>s, raised when a buff falls off or is removed entirely.</summary>
    public static EventFilter<RemoveBuffEvent> RemoveBuff => new();
    /// <summary>Matches <see cref="RemoveBuffStackEvent"/>s, raised when a single stack of a buff is removed.</summary>
    public static EventFilter<RemoveBuffStackEvent> RemoveBuffStack => new();
    /// <summary>Matches <see cref="RemoveDebuffEvent"/>s, raised when a debuff falls off or is removed entirely.</summary>
    public static EventFilter<RemoveDebuffEvent> RemoveDebuff => new();
    /// <summary>Matches <see cref="RemoveDebuffStackEvent"/>s, raised when a single stack of a debuff is removed.</summary>
    public static EventFilter<RemoveDebuffStackEvent> RemoveDebuffStack => new();
    /// <summary>Matches <see cref="ResourceChangeEvent"/>s, fabricated whenever a tracked resource's amount changes.</summary>
    public static EventFilter<ResourceChangeEvent> ResourceChange => new();
    /// <summary>Matches <see cref="ResurrectEvent"/>s, raised when a unit is resurrected.</summary>
    public static EventFilter<ResurrectEvent> Resurrect => new();
    /// <summary>Matches <see cref="SpendResourceEvent"/>s, fabricated when a cast spends a resource cost.</summary>
    public static EventFilter<SpendResourceEvent> SpendResource => new();
    /// <summary>Matches <see cref="FilterCooldownInfoEvent"/>s, used by <see cref="SpellUsable"/> to begin an ability's cooldown from cast-cooldown metadata ahead of the full <see cref="CastEvent"/>.</summary>
    public static EventFilter<FilterCooldownInfoEvent> PrefilterCD => new();
    /// <summary>Matches <see cref="UpdateSpellUsableEvent"/>s, fabricated whenever an ability's cooldown or charge state changes.</summary>
    public static EventFilter<UpdateSpellUsableEvent> UpdateSpellUsable => new();
}
