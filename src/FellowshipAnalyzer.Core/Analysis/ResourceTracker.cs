using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Tracks all resource types for the selected player by subscribing to
/// <see cref="ResourceChangeEvent"/>, <see cref="CastEvent"/>, and <see cref="DrainEvent"/>.
/// Handles both source and target roles via <see cref="ResourceActorEnum"/>.
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

    public override void Initialize()
    {
        AddEventListener(Events.ResourceChange.By(SELECTED_PLAYER), OnResourceChangeByPlayer);
        AddEventListener(Events.ResourceChange.To(SELECTED_PLAYER), OnResourceChangeToPlayer);
        AddEventListener(Events.Cast.By(SELECTED_PLAYER), OnCast);
        AddEventListener(Events.Drain.To(SELECTED_PLAYER), OnDrainToPlayer);
    }

    // Named convenience properties — null if the resource type has not appeared in any event.
    public ResourceState? Mana => GetResourceState(ResourceTypes.Mana);
    public ResourceState? Primary => GetResourceState(ResourceTypes.Primary);
    public ResourceState? Secondary => GetResourceState(ResourceTypes.Secondary);
    public ResourceState? Spirit => GetResourceState(ResourceTypes.Spirit);
    public ResourceState? Stagger => GetResourceState(ResourceTypes.Stagger);

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

    private void OnResourceChangeByPlayer(ResourceChangeEvent e)
    {
        // ResourceActor == Source means ClassResources shows the source (player's) resources.
        if (e.ResourceActor != ResourceActorEnum.Source) return;

        var classResource = e.ClassResources?.FirstOrDefault(cr => cr.Type == e.ResourceChangeType);
        RecordGain(
            e.ResourceChangeType,
            e.Ability.Id,
            gained: (int)(e.ResourceChange - e.Waste),
            wasted: (int)e.Waste,
            currentAfterFromEvent: classResource?.Amount,
            maxFromEvent: classResource?.Max,
            e.Timestamp);
    }

    private void OnResourceChangeToPlayer(ResourceChangeEvent e)
    {
        // ResourceActor == Target means ClassResources shows the target (player's) resources.
        if (e.ResourceActor != ResourceActorEnum.Target) return;

        var classResource = e.ClassResources?.FirstOrDefault(cr => cr.Type == e.ResourceChangeType);
        RecordGain(
            e.ResourceChangeType,
            e.Ability.Id,
            gained: (int)(e.ResourceChange - e.Waste),
            wasted: (int)e.Waste,
            currentAfterFromEvent: classResource?.Amount,
            maxFromEvent: classResource?.Max,
            e.Timestamp);
    }

    private void OnCast(CastEvent e)
    {
        // Player is always source for Cast.By(SELECTED_PLAYER).
        var resources = e.SourceResources?.Resources;
        if (resources is null || resources.Count == 0) return;

        foreach (var resource in resources)
        {
            var state = GetOrCreateState(resource.Type, resource.Max);

            if (resource.Cost is > 0)
            {
                var cost = resource.Cost.Value;
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

    private void OnDrainToPlayer(DrainEvent e)
    {
        // ResourceActor == Target means ClassResources shows the target (player's) resources.
        if (e.ResourceActor != ResourceActorEnum.Target) return;

        var resourceType = (ResourceTypes)e.ResourceChangeType;
        var amount = (int)e.ResourceChange;
        var classResource = e.ClassResources?.FirstOrDefault(cr => cr.Type == resourceType);

        var state = GetOrCreateState(resourceType, classResource?.Max);
        state.Drained += amount;
        // ClassResource.Amount for a drain is the amount BEFORE the drain.
        state.Current = classResource?.Amount is { } before
            ? Math.Max(0, before - amount)
            : Math.Max(0, state.Current - amount);

        var ev = new ResourceEvent(e.Timestamp, e.Ability.Id, resourceType, ResourceEventKind.Drain, amount, Wasted: 0, state.Current);
        state.Events.Add(ev);
        _allEvents.Add(ev);
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