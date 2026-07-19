using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

public static class Events
{
    public static AnyEventFilter Any => new();
    public static EventFilter<AbsorbedEvent> Absorbed => new();
    public static EventFilter<ApplyBuffEvent> ApplyBuff => new();
    public static EventFilter<ApplyBuffStackEvent> ApplyBuffStack => new();
    public static EventFilter<ApplyDebuffEvent> ApplyDebuff => new();
    public static EventFilter<ApplyDebuffStackEvent> ApplyDebuffStack => new();
    public static EventFilter<BeginCastEvent> BeginCast => new();
    public static EventFilter<BeginChannelEvent> BeginChannel => new();
    public static EventFilter<EndChannelEvent> EndChannel => new();
    public static EventFilter<CastEvent> Cast => new();
    public static EventFilter<ChangeBuffStackEvent> ChangeBuffStack => new();
    public static EventFilter<ChangeDebuffStackEvent> ChangeDebuffStack => new();
    public static EventFilter<ChangeCooldownModifierEvent> ChangeCooldownModifier => new();
    public static EventFilter<ChangeHasteEvent> ChangeHaste => new();
    public static EventFilter<ChangeStatsEvent> ChangeStats => new();
    public static EventFilter<DamageEvent> Damage => new();
    public static EventFilter<DeathEvent> Death => new();
    public static EventFilter<DispelEvent> Dispel => new();
    public static EventFilter<FightEndEvent> FightEnd => new();
    public static EventFilter<FightStartEvent> FightStart => new();
    public static EventFilter<HealEvent> Heal => new();
    public static EventFilter<InterruptEvent> Interrupt => new();
    public static EventFilter<RefreshBuffEvent> RefreshBuff => new();
    public static EventFilter<RefreshDebuffEvent> RefreshDebuff => new();
    public static EventFilter<RemoveBuffEvent> RemoveBuff => new();
    public static EventFilter<RemoveBuffStackEvent> RemoveBuffStack => new();
    public static EventFilter<RemoveDebuffEvent> RemoveDebuff => new();
    public static EventFilter<RemoveDebuffStackEvent> RemoveDebuffStack => new();
    public static EventFilter<ResourceChangeEvent> ResourceChange => new();
    public static EventFilter<ResurrectEvent> Resurrect => new();
    public static EventFilter<SpendResourceEvent> SpendResource => new();
    public static EventFilter<FilterCooldownInfoEvent> PrefilterCD => new();
    public static EventFilter<UpdateSpellUsableEvent> UpdateSpellUsable => new();
}
