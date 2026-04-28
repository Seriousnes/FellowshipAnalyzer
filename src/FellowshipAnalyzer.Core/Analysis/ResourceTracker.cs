using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks all resource types for the selected player by subscribing to all events via
/// <see cref="Events.Any"/> and inspecting <see cref="Event.SourceResources"/> /
/// <see cref="Event.TargetResources"/> to find the selected player's resources.
/// Spend tracking is driven by <see cref="CastEvent"/> via <see cref="Events.Cast"/>.
/// </summary>
public class ResourceTracker : Analyzer
{
    private readonly Dictionary<ResourceTypes, ResourceState> _states = [];
    private readonly List<ResourceEvent> _allEvents = [];

    /// <summary>
    /// Override the maximum value for specific resource types before calling
    /// <c>base.Initialize()</c>. Events will still update max unless an override exists.
    /// </summary>
    protected Dictionary<ResourceTypes, int> MaxOverrides { get; } = [];

    /// <summary>
    /// Override the display name for specific resource types (e.g. <c>"Focus"</c> for
    /// <see cref="ResourceTypes.Primary"/>). Used by UI components to label series and
    /// statistics. If no override is registered, the enum name is used.
    /// </summary>
    protected Dictionary<ResourceTypes, string> DisplayNameOverrides { get; } = [];

    /// <summary>
    /// Returns the display name for <paramref name="type"/>: either the registered override
    /// from <see cref="DisplayNameOverrides"/> or the enum name as a fallback.
    /// </summary>
    public string GetDisplayName(ResourceTypes type) =>
        DisplayNameOverrides.TryGetValue(type, out var name) ? name : type.ToString();

    public override void Initialize()
    {
        AddEventListener(Events.Cast.By(SELECTED_PLAYER), OnCast);
        AddEventListener(Events.ResourceChange.By(SELECTED_PLAYER), OnResourceChange);
        AddEventListener(Events.Any, OnEvent);
    }

    // Named convenience properties — null if the resource type has not appeared in any event.
    public ResourceState? Mana => GetResourceState(ResourceTypes.Mana);
    public ResourceState? Primary => GetResourceState(ResourceTypes.Primary);
    public ResourceState? Secondary => GetResourceState(ResourceTypes.Secondary);
    public ResourceState? Spirit => GetResourceState(ResourceTypes.Spirit);
    public ResourceState? Stagger => GetResourceState(ResourceTypes.Stagger);

    /// <summary>The player's most recently observed current hit points.</summary>
    public long CurrentHealth { get; private set; }

    /// <summary>The player's most recently observed maximum hit points.</summary>
    public long MaxHealth { get; private set; }

    /// <summary>All resource events across all types, in chronological order.</summary>
    public IReadOnlyList<ResourceEvent> AllResourceEvents => _allEvents;

    /// <summary>
    /// Returns the resource state for <paramref name="type"/>, or <c>null</c> if that resource
    /// type has not yet appeared in any processed event.
    /// </summary>
    public ResourceState? GetResourceState(ResourceTypes type) =>
        _states.TryGetValue(type, out var state) ? state : null;

    // Query methods — create an empty state on demand so callers always get a valid value.
    public int GetCurrent(ResourceTypes type) => GetOrCreateState(type).Current;
    public int GetMax(ResourceTypes type) => GetOrCreateState(type).Max;
    public int GetGenerated(ResourceTypes type) => GetOrCreateState(type).Generated;
    public int GetWasted(ResourceTypes type) => GetOrCreateState(type).Wasted;
    public int GetSpent(ResourceTypes type) => GetOrCreateState(type).Spent;
    public int GetDrained(ResourceTypes type) => GetOrCreateState(type).Drained;
    public IReadOnlyDictionary<int, int> GetGeneratorCasts(ResourceTypes type) => GetOrCreateState(type).GeneratorCasts;
    public IReadOnlyDictionary<int, int> GetSpenderCasts(ResourceTypes type) => GetOrCreateState(type).SpenderCasts;
    public IReadOnlyList<ResourceEvent> GetResourceEvents(ResourceTypes type) => GetOrCreateState(type).Events;

    private void OnResourceChange(ResourceChangeEvent e)
    {
        var gained = (int)(e.ResourceChange - e.Waste);
        var wasted = (int)e.Waste;
        RecordGain(
            e.ResourceChangeType,
            e.Ability.Id,
            gained: gained,
            wasted: wasted,
            currentAfterFromEvent: null,
            maxFromEvent: null,
            e.Timestamp);
    }

    private void OnEvent(Event e)
    {
        // Determine which ActorResources belong to the selected player.
        ActorResources? playerResources = null;
        if (e is IHasSourceEvent src && Owner.ByPlayer(src))
            playerResources = e.SourceResources;
        else if (e is IHasTargetEvent tgt && Owner.ToPlayer(tgt))
            playerResources = e.TargetResources;

        if (e is ResourceChangeEvent or BaseCastEvent || playerResources is not { Resources.Count: > 0 })
        {
            return;
        }
        
        UpdateHealth(playerResources);

        var ability = (e as IAbilityEvent)?.Ability ?? new Ability();
        foreach (var resource in playerResources.Resources)
        {
            var state = GetOrCreateState(resource.Type, resource.Max);
            var delta = resource.Amount - state.Current;
            if (delta <= 0) continue;

            // Only fabricate — OnResourceChange is the single place RecordGain is called.
            Owner.EventEmitter.FabricateEvent(new ResourceChangeEvent
            {
                Timestamp = e.Timestamp,
                SourceId = PlayerId,
                TargetId = PlayerId,
                Ability = ability,
                ResourceChangeType = resource.Type,
                ResourceChange = delta,
                Waste = 0,
            }, e);
        }
    }

    /// <summary>
    /// Override to supply a resource cost from spell definitions when <see cref="ClassResource.Cost"/>
    /// is null. Called for each resource type on every cast by the selected player.
    /// Return null to indicate the spell has no cost for <paramref name="type"/>.
    /// </summary>
    protected virtual int? GetResourceCost(CastEvent e, ResourceTypes type) => null;

    private void OnCast(CastEvent e)
    {
        // Player is always source for Cast.By(SELECTED_PLAYER).
        if (e.SourceResources is not null)
            UpdateHealth(e.SourceResources);

        var resources = e.SourceResources?.Resources;
        if (resources is null || resources.Count == 0) return;

        foreach (var resource in resources)
        {
            var state = GetOrCreateState(resource.Type, resource.Max);

            // Prefer the cost reported by the event; fall back to the spell-definition cost
            // (Fellowship logs record pre-cast resource amounts, not spend deltas).
            var effectiveCost = resource.Cost ?? GetResourceCost(e, resource.Type);

            if (effectiveCost is > 0)
            {
                var cost = effectiveCost.Value;
                state.Spent += cost;
                // ClassResource.Amount for a spend is the amount BEFORE the spend.
                state.Current = Math.Max(0, resource.Amount - cost);
                IncrementDict(state.SpenderCasts, e.Ability.Id);

                var ev = new ResourceEvent(e.Timestamp, e.Ability.Id, resource.Type, ResourceEventKind.Spend, cost, Wasted: 0, state.Current);
                state.Events.Add(ev);
                _allEvents.Add(ev);
            }
            else
            {
                // Snapshot update only (no spend event).
                state.Current = resource.Amount;
            }
        }
    }

    private void RecordGain(
        ResourceTypes type,
        int spellId,
        int gained,
        int wasted,
        int? currentAfterFromEvent,
        int? maxFromEvent,
        int timestamp)
    {
        var state = GetOrCreateState(type, maxFromEvent);
        state.Generated += gained;
        state.Wasted += wasted;

        // ClassResource.Amount for an energize is the amount AFTER the gain.
        state.Current = currentAfterFromEvent
            ?? Math.Min(state.Current + gained, state.Max > 0 ? state.Max : int.MaxValue);

        IncrementDict(state.GeneratorCasts, spellId);

        var ev = new ResourceEvent(timestamp, spellId, type, ResourceEventKind.Gain, gained, wasted, state.Current);
        state.Events.Add(ev);
        _allEvents.Add(ev);
    }

    private ResourceState GetOrCreateState(ResourceTypes type, int? maxFromEvent = null)
    {
        if (!_states.TryGetValue(type, out var state))
        {
            state = new ResourceState();
            state.Max = MaxOverrides.TryGetValue(type, out var maxOverride)
                ? maxOverride
                : maxFromEvent ?? 0;
            _states[type] = state;
        }
        else if (maxFromEvent.HasValue && !MaxOverrides.ContainsKey(type))
        {
            state.Max = maxFromEvent.Value;
        }

        return state;
    }

    private void UpdateHealth(ActorResources resources)
    {
        // MaxHitPoints > 0 guards against unpopulated ActorResources objects.
        if (resources.MaxHitPoints > 0)
        {
            CurrentHealth = resources.HitPoints;
            MaxHealth = resources.MaxHitPoints;
        }
    }

    private static void IncrementDict(Dictionary<int, int> dict, int key)
    {
        dict[key] = dict.TryGetValue(key, out var existing) ? existing + 1 : 1;
    }
}

public sealed record ResourceEvent(
    int Timestamp,
    int Id,
    ResourceTypes ResourceType,
    ResourceEventKind Kind,
    int Amount,
    int Wasted,
    int CurrentAfter);

public enum ResourceEventKind
{
    Gain,
    Spend,
    Drain,
}

/// <summary>
/// Snapshot of a single resource type's state tracked by <see cref="ResourceTracker"/>.
/// </summary>
public sealed class ResourceState
{
    public int Current { get; internal set; }
    public int Max { get; internal set; }
    public int Generated { get; internal set; }
    public int Wasted { get; internal set; }
    public int Spent { get; internal set; }
    public int Drained { get; internal set; }

    internal Dictionary<int, int> GeneratorCasts { get; } = [];
    internal Dictionary<int, int> SpenderCasts { get; } = [];
    internal List<ResourceEvent> Events { get; } = [];
}