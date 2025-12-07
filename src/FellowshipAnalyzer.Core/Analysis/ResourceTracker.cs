using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Base class for tracking a specific resource type by subscribing to
/// <see cref="ResourceChangeEvent"/> and <see cref="CastEvent"/> events.
/// Subclasses set <see cref="ResourceTypeId"/> and <see cref="MaxResource"/>
/// to configure which resource to track.
/// </summary>
public abstract class ResourceTracker : Analyzer
{
    protected int ResourceTypeId { get; set; }
    protected int MaxResource { get; set; }
    protected int InitialResource { get; set; }

    public override void Initialize()
    {
        Current = InitialResource;
        AddEventListener(Events.ResourceChange.By(SELECTED_PLAYER), OnResourceChange);
        AddEventListener(Events.Cast.By(SELECTED_PLAYER), OnCast);
    }

    public int Generated { get; private set; }
    public int Wasted { get; private set; }
    public int Spent { get; private set; }
    public int Current { get; private set; }

    private readonly Dictionary<int, int> _generatorCasts = [];
    private readonly Dictionary<int, int> _spenderCasts = [];
    private readonly List<ResourceEvent> _events = [];

    public IReadOnlyDictionary<int, int> GeneratorCastCounts => _generatorCasts;
    public IReadOnlyDictionary<int, int> SpenderCastCounts => _spenderCasts;
    public IReadOnlyList<ResourceEvent> ResourceEvents => _events;

    private void OnResourceChange(ResourceChangeEvent e)
    {
        if (e.ResourceChangeType != ResourceTypeId)
        {
            return;
        }

        var gained = (int)(e.ResourceChange - e.Waste);
        var wasted = (int)e.Waste;

        Generated += gained;
        Wasted += wasted;
        Current = Math.Min(Current + gained, MaxResource);

        IncrementDict(_generatorCasts, e.AbilityGameId);

        _events.Add(new ResourceEvent(
            e.Timestamp,
            e.AbilityGameId,
            ResourceEventKind.Gain,
            gained,
            wasted,
            Current));
    }

    private void OnCast(CastEvent e)
    {
        if (e.ClassResources is null)
        {
            return;
        }

        var resource = e.ClassResources.FirstOrDefault(cr => cr.Type == ResourceTypeId);
        if (resource is null || resource.Cost is null or 0)
        {
            return;
        }

        var cost = resource.Cost.Value;
        Spent += cost;
        Current = Math.Max(0, Current - cost);

        IncrementDict(_spenderCasts, e.AbilityGameId);

        _events.Add(new ResourceEvent(
            e.Timestamp,
            e.AbilityGameId,
            ResourceEventKind.Spend,
            cost,
            Wasted: 0,
            Current));
    }

    private static void IncrementDict(Dictionary<int, int> dict, int key)
    {
        dict[key] = dict.TryGetValue(key, out var existing) ? existing + 1 : 1;
    }
}

public sealed record ResourceEvent(
    int Timestamp,
    int AbilityId,
    ResourceEventKind Kind,
    int Amount,
    int Wasted,
    int CurrentAfter);

public enum ResourceEventKind
{
    Gain,
    Spend,
}
