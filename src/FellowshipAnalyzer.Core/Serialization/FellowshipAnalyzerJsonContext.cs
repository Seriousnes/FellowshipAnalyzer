using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Core.Serialization;

/// <summary>Source-generated JSON serialization context covering every combat log event type and FellowshipLogs GraphQL response shape the parser consumes.</summary>
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(Event))]
[JsonSerializable(typeof(List<Event>))]
[JsonSerializable(typeof(EventsResult))]
[JsonSerializable(typeof(AnalysisPreload))]
[JsonSerializable(typeof(CharacterReports))]
[JsonSerializable(typeof(ReportSummary))]
[JsonSerializable(typeof(IReadOnlyList<ReportSummary>))]
[JsonSerializable(typeof(AbsorbedEvent))]
[JsonSerializable(typeof(ApplyBuffEvent))]
[JsonSerializable(typeof(ApplyBuffStackEvent))]
[JsonSerializable(typeof(ApplyDebuffEvent))]
[JsonSerializable(typeof(ApplyDebuffStackEvent))]
[JsonSerializable(typeof(AuraBrokenEvent))]
[JsonSerializable(typeof(AutoAttackCooldownEvent))]
[JsonSerializable(typeof(BeaconHealEvent))]
[JsonSerializable(typeof(BeaconTransferFailedEvent))]
[JsonSerializable(typeof(BeginCastEvent))]
[JsonSerializable(typeof(BeginChannelEvent))]
[JsonSerializable(typeof(CastEvent))]
[JsonSerializable(typeof(ChangeBuffStackEvent))]
[JsonSerializable(typeof(ChangeCooldownModifierEvent))]
[JsonSerializable(typeof(ChangeDebuffStackEvent))]
[JsonSerializable(typeof(ChangeHasteEvent))]
[JsonSerializable(typeof(ChangeStatsEvent))]
[JsonSerializable(typeof(CombatantInfoEvent))]
[JsonSerializable(typeof(DamageEvent))]
[JsonSerializable(typeof(DeathEvent))]
[JsonSerializable(typeof(DispelEvent))]
[JsonSerializable(typeof(EndChannelEvent))]
[JsonSerializable(typeof(ExtraAttacksEvent))]
[JsonSerializable(typeof(FeedHealEvent))]
[JsonSerializable(typeof(FightEndEvent))]
[JsonSerializable(typeof(FightStartEvent))]
[JsonSerializable(typeof(FilterBuffInfoEvent))]
[JsonSerializable(typeof(FilterCooldownInfoEvent))]
[JsonSerializable(typeof(FreeCastEvent))]
[JsonSerializable(typeof(GlobalCooldownEvent))]
[JsonSerializable(typeof(HealAbsorbed))]
[JsonSerializable(typeof(HealEvent))]
[JsonSerializable(typeof(HealthEvent))]
[JsonSerializable(typeof(InterruptEvent))]
[JsonSerializable(typeof(LeechEvent))]
[JsonSerializable(typeof(MaxChargesDecreasedEvent))]
[JsonSerializable(typeof(MaxChargesIncreasedEvent))]
[JsonSerializable(typeof(PhaseEvent))]
[JsonSerializable(typeof(PullStartEvent))]
[JsonSerializable(typeof(PullEndEvent))]
[JsonSerializable(typeof(RefreshBuffEvent))]
[JsonSerializable(typeof(RefreshDebuffEvent))]
[JsonSerializable(typeof(RemoveBuffEvent))]
[JsonSerializable(typeof(RemoveBuffStackEvent))]
[JsonSerializable(typeof(RemoveDebuffEvent))]
[JsonSerializable(typeof(RemoveDebuffStackEvent))]
[JsonSerializable(typeof(ResourceChangeEvent))]
[JsonSerializable(typeof(ResurrectEvent))]
[JsonSerializable(typeof(SpendResourceEvent))]
[JsonSerializable(typeof(SummonEvent))]
[JsonSerializable(typeof(TrackedBuffEvent))]
[JsonSerializable(typeof(UpdateSpellUsableEvent))]
public partial class FellowshipAnalyzerJsonContext : JsonSerializerContext
{
}
