using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>
/// §4 of the redesign doc — moves the snapshot-delta fabrication that previously lived inside
/// <see cref="ResourceTracker.OnEvent"/> into the pre-dispatch normalizer pass.
/// </summary>
/// <remarks>
/// <para>
/// Today: <see cref="ResourceTracker"/> would fabricate a <see cref="ResourceChangeEvent"/>
/// mid-dispatch when it observed a positive snapshot delta on the player's resources. That
/// conflated mutation with observation — modules saw a stream the parser was still rewriting.
/// </para>
/// <para>
/// Now: this normalizer inspects every event with player resources, tracks a per-resource-type
/// "last observed" amount, and emits a fabricated <see cref="ResourceChangeEvent"/> immediately
/// after any non-cast event that increased the amount. Cast events update the running snapshot
/// (subtracting <see cref="ClassResource.Cost"/> when present) but never trigger fabrication.
/// </para>
/// <para>
/// Modules become pure observers of the post-normalized stream — the dispatch loop no longer
/// mutates events. Runs at <see cref="Priority"/> 50, after the existing normalizers.
/// </para>
/// </remarks>
public sealed class ResourceFabricationNormalizer : IEventNormalizer
{
    public int Priority => 50;

    public List<Event> Normalize(List<Event> events, int playerId)
    {
        var lastKnown = new Dictionary<ResourceTypes, int>();
        var output = new List<Event>(events.Count);

        foreach (var e in events)
        {
            output.Add(e);

            ActorResources? playerResources = null;
            if (e is IHasSourceEvent src && src.SourceId == playerId)
                playerResources = e.SourceResources;
            else if (e is IHasTargetEvent tgt && tgt.TargetId == playerId)
                playerResources = e.TargetResources;

            if (playerResources is not { Resources.Count: > 0 }) continue;

            // ResourceChangeEvents already carry the delta; the tracker observes them directly.
            // BaseCastEvent has its own OnCast handler in the tracker (spend semantics).
            var shouldFabricate = e is not (ResourceChangeEvent or BaseCastEvent);
            var ability = (e as IAbilityEvent)?.Ability ?? new Ability();

            foreach (var resource in playerResources.Resources)
            {
                if (!lastKnown.TryGetValue(resource.Type, out var last)) last = 0;
                var delta = resource.Amount - last;

                if (shouldFabricate && delta > 0)
                {
                    output.Add(new ResourceChangeEvent
                    {
                        Timestamp = e.Timestamp,
                        SourceId = playerId,
                        TargetId = playerId,
                        Ability = ability,
                        ResourceChangeType = resource.Type,
                        ResourceChange = delta,
                        Waste = 0,
                        Fabricated = true,
                        Trigger = e,
                    });
                }

                // Cast events report the PRE-cost amount; subtract any explicit cost so the
                // running snapshot reflects the player's true post-cast state. Hero-specific
                // virtual cost overrides still flow through ResourceTracker.OnCast.
                var costToSubtract = e is BaseCastEvent && resource.Cost > 0 ? resource.Cost.Value : 0;
                lastKnown[resource.Type] = System.Math.Max(0, resource.Amount - costToSubtract);
            }
        }

        return output;
    }
}
