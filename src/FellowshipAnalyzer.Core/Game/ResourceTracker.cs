using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Core.Resources;

/// <summary>
/// Tracks all resource types for the selected player by subscribing to all events via
/// <see cref="FellowshipAnalyzer.Core.Analysis.Events.Any"/> and inspecting <see cref="Event.SourceResources"/> /
/// <see cref="Event.TargetResources"/> to find the selected player's resources.
/// Spend tracking is driven by <see cref="CastEvent"/> via <see cref="FellowshipAnalyzer.Core.Analysis.Events.Cast"/>.
/// </summary>
public partial class ResourceTracker(ILogger<ResourceTracker> logger) : Analyzer
{
    private readonly ILogger<ResourceTracker> _logger = logger;


    private readonly Dictionary<ResourceTypes, ResourceState> _states = [];
    private readonly List<ResourceEvent> _allEvents = [];

    /// <summary>
    /// Override the maximum value for specific resource types before the module starts
    /// observing events. Events will still update max unless an override exists.
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

    /// <summary>The tracked state for <see cref="ResourceTypes.Mana"/>, or <c>null</c> if not yet observed.</summary>
    public ResourceState? Mana => GetResourceState(ResourceTypes.Mana);
    /// <summary>The tracked state for <see cref="ResourceTypes.Primary"/>, or <c>null</c> if not yet observed.</summary>
    public ResourceState? Primary => GetResourceState(ResourceTypes.Primary);
    /// <summary>The tracked state for <see cref="ResourceTypes.Secondary"/>, or <c>null</c> if not yet observed.</summary>
    public ResourceState? Secondary => GetResourceState(ResourceTypes.Secondary);
    /// <summary>The tracked state for <see cref="ResourceTypes.Spirit"/>, or <c>null</c> if not yet observed.</summary>
    public ResourceState? Spirit => GetResourceState(ResourceTypes.Spirit);
    /// <summary>The tracked state for <see cref="ResourceTypes.Stagger"/>, or <c>null</c> if not yet observed.</summary>
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

    /// <summary>The current amount of <paramref name="type"/> the player has, defaulting to zero if unobserved.</summary>
    public int GetCurrent(ResourceTypes type) => GetOrCreateState(type).Current;
    /// <summary>The maximum amount of <paramref name="type"/> observed so far, defaulting to zero if unobserved.</summary>
    public int GetMax(ResourceTypes type) => GetOrCreateState(type).Max;
    /// <summary>The total amount of <paramref name="type"/> generated across the parse.</summary>
    public int GetGenerated(ResourceTypes type) => GetOrCreateState(type).Generated;
    /// <summary>The total amount of <paramref name="type"/> generated and lost to being at or over the cap.</summary>
    public int GetWasted(ResourceTypes type) => GetOrCreateState(type).Wasted;
    /// <summary>The total amount of <paramref name="type"/> spent on casts across the parse.</summary>
    public int GetSpent(ResourceTypes type) => GetOrCreateState(type).Spent;
    /// <summary>The total amount of <paramref name="type"/> lost to drains rather than spent on casts.</summary>
    public int GetDrained(ResourceTypes type) => GetOrCreateState(type).Drained;
    /// <summary>Counts of <paramref name="type"/>-generating casts, keyed by spell id.</summary>
    public IReadOnlyDictionary<int, int> GetGeneratorCasts(ResourceTypes type) => GetOrCreateState(type).GeneratorCasts;
    /// <summary>Counts of <paramref name="type"/>-spending casts, keyed by spell id.</summary>
    public IReadOnlyDictionary<int, int> GetSpenderCasts(ResourceTypes type) => GetOrCreateState(type).SpenderCasts;
    /// <summary>All gain, spend, and drain events recorded for <paramref name="type"/>, in chronological order.</summary>
    public IReadOnlyList<ResourceEvent> GetResourceEvents(ResourceTypes type) => GetOrCreateState(type).Events;

    [On<ResourceChangeEvent>(By = Actor.Player)]
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

    [On<Event>]
    private void OnEvent(Event e)
    {
        ActorResources? playerResources = null;
        if (e is IHasSourceEvent src && Owner.ByPlayer(src))
            playerResources = e.SourceResources;
        else if (e is IHasTargetEvent tgt && Owner.ToPlayer(tgt))
            playerResources = e.TargetResources;

        if (e is ResourceChangeEvent or BaseCastEvent || playerResources is not { Resources.Count: > 0 })
            return;

        UpdateHealth(playerResources);

        var spellId = (e as IAbilityEvent)?.Ability.Id ?? 0;

        foreach (var resource in playerResources.Resources)
        {
            var state = GetOrCreateState(resource.Type, resource.Max);
            var delta = resource.Amount - state.Current;

            if (delta > 0)
            {
                RecordGain(
                    resource.Type,
                    spellId,
                    gained: delta,
                    wasted: 0,
                    currentAfterFromEvent: resource.Amount,
                    maxFromEvent: null,
                    e.Timestamp);
            }
            else if (delta < 0)
            {
                state.Current = resource.Amount;
            }
        }
    }

    /// <summary>
    /// Override to supply a resource cost from spell definitions when <see cref="ClassResource.Cost"/>
    /// is null. Called for each resource type on every cast by the selected player.
    /// Return null to indicate the spell has no cost for <paramref name="type"/>.
    /// </summary>
    protected virtual int? GetResourceCost(CastEvent e, ResourceTypes type) => null;

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent e)
    {
        if (e.SourceResources is not null)
            UpdateHealth(e.SourceResources);

        var resources = e.SourceResources?.Resources;
        if (resources is null || resources.Count == 0) return;

        foreach (var resource in resources)
        {
            var state = GetOrCreateState(resource.Type, resource.Max);
            var effectiveCost = resource.Cost ?? GetResourceCost(e, resource.Type);
            var trackerBefore = state.Current;

            var implicitGain = resource.Amount - state.Current;
            if (implicitGain > 0)
            {
                RecordGain(
                    resource.Type,
                    spellId: 0,
                    gained: implicitGain,
                    wasted: 0,
                    currentAfterFromEvent: resource.Amount,
                    maxFromEvent: null,
                    e.Timestamp);
            }
            else if (implicitGain < 0)
            {
                state.Current = resource.Amount;
            }

            if (effectiveCost is > 0 && effectiveCost.Value > state.Current)
            {
                _logger.LogError(
                    "{Tracker} overspend: cast of {AbilityName} ({AbilityId}) at {Timestamp} spends {Cost} {ResourceType} but player has only {Available} (tracker before reconcile: {TrackerBefore}).",
                    GetType().Name,
                    e.Ability.Name,
                    e.Ability.Id,
                    this.Owner.FormatTimestamp(e.Timestamp, 3),
                    effectiveCost.Value,
                    resource.Type,
                    state.Current,
                    trackerBefore);
            }

            if (effectiveCost is > 0)
            {
                var cost = effectiveCost.Value;
                state.Spent += cost;
                state.Current = Math.Max(0, state.Current - cost);
                IncrementDict(state.SpenderCasts, e.Ability.Id);

                var ev = new ResourceEvent(e.Timestamp, e.Ability.Id, resource.Type, ResourceEventKind.Spend, cost, Wasted: 0, state.Current);
                state.Events.Add(ev);
                _allEvents.Add(ev);
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
            state = new ResourceState
            {
                Max = MaxOverrides.TryGetValue(type, out var maxOverride)
                    ? maxOverride
                    : maxFromEvent ?? 0
            };
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

/// <summary>A single gain, spend, or drain observed for one resource type on <see cref="ResourceTracker"/>.</summary>
/// <param name="Timestamp">The event's timestamp within the report.</param>
/// <param name="Id">The spell id responsible for the change, or <c>0</c> if none applies.</param>
/// <param name="ResourceType">The resource type this event affects.</param>
/// <param name="Kind">Whether the resource was gained, spent, or drained.</param>
/// <param name="Amount">The amount gained, spent, or drained.</param>
/// <param name="Wasted">The portion of a gain lost to being at or over the cap.</param>
/// <param name="CurrentAfter">The tracker's current amount immediately after this event.</param>
public sealed record ResourceEvent(
    int Timestamp,
    int Id,
    ResourceTypes ResourceType,
    ResourceEventKind Kind,
    int Amount,
    int Wasted,
    int CurrentAfter);

/// <summary>Classifies how a <see cref="ResourceEvent"/> changed a resource's current amount.</summary>
public enum ResourceEventKind
{
    /// <summary>The resource increased.</summary>
    Gain,
    /// <summary>The resource decreased because a cast spent it.</summary>
    Spend,
    /// <summary>The resource decreased outside of a cast cost, e.g. a mechanic that drains it directly.</summary>
    Drain,
}

/// <summary>
/// Snapshot of a single resource type's state tracked by <see cref="ResourceTracker"/>.
/// </summary>
public sealed class ResourceState
{
    /// <summary>The player's current amount of this resource.</summary>
    public int Current { get; internal set; }
    /// <summary>The highest maximum observed for this resource so far.</summary>
    public int Max { get; internal set; }
    /// <summary>The total amount generated across the parse.</summary>
    public int Generated { get; internal set; }
    /// <summary>The total amount generated and lost to being at or over the cap.</summary>
    public int Wasted { get; internal set; }
    /// <summary>The total amount spent on casts across the parse.</summary>
    public int Spent { get; internal set; }
    /// <summary>The total amount lost to drains rather than spent on casts.</summary>
    public int Drained { get; internal set; }

    internal Dictionary<int, int> GeneratorCasts { get; } = [];
    internal Dictionary<int, int> SpenderCasts { get; } = [];
    internal List<ResourceEvent> Events { get; } = [];
}