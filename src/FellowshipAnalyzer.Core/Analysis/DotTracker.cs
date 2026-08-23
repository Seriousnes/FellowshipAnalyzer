using FellowshipAnalyzer.Core.Common;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Reads a hero's damage-over-time effects off the aura state <see cref="Combatants"/> and
/// <see cref="Enemies"/> already keep, so an analyzer asks what was on an enemy at an instant rather
/// than rebuilding that from events.
/// <para>
/// This module declares no <c>[On&lt;&gt;]</c> handlers. A spell filter has to name a member of the
/// hero's own generated registry, which Core cannot see, so every reading here is derived by effect id
/// instead. A hero that needs event-driven bookkeeping of its own declares those handlers on its
/// analyzer, where the registry is visible.
/// </para>
/// <para>
/// Derive a per-hero tracker that returns the hero's list from <see cref="Dots"/> and register it
/// with <c>[AddModule&lt;T&gt;]</c>.
/// </para>
/// </summary>
public abstract class DotTracker : Module
{
    /// <summary>The effects this tracker reads, in the order a chart and a checklist show them.</summary>
    public abstract List<Dot> Dots { get; }

    /// <summary>How many effects <see cref="Dots"/> holds.</summary>
    public int Count => Dots.Count;

    private Combatants Combatants => field ??= Owner.Combatants
        ?? throw new InvalidOperationException(
            $"{GetType().Name} reads aura state from {nameof(Analysis.Combatants)}, which the parser did not construct.");

    private Enemies Enemies => field ??= Owner.Enemies
        ?? throw new InvalidOperationException(
            $"{GetType().Name} reads the enemy population from {nameof(Analysis.Enemies)}, which the parser did not construct.");

    /// <summary>Every enemy carrying at least one of <see cref="Dots"/> at <paramref name="timestamp"/>.</summary>
    public HashSet<UnitKey> EnemiesWithAnyDot(int timestamp)
    {
        var enemies = new HashSet<UnitKey>();
        foreach (var dot in Dots)
        {
            foreach (var key in Enemies.WithAura(dot.Effect, timestamp))
                enemies.Add(key);
        }
        return enemies;
    }

    /// <summary>Instances of every effect in <see cref="Dots"/> active on one enemy at <paramref name="timestamp"/>.</summary>
    public int InstancesOn(UnitKey enemy, int timestamp)
    {
        var instances = 0;
        foreach (var dot in Dots)
            instances += Combatants.AuraInstanceCount(enemy.ActorId, enemy.Instance, dot.Effect, timestamp);
        return instances;
    }

    /// <summary>
    /// The enemy carrying the most instances of <see cref="Dots"/> at <paramref name="timestamp"/>, or
    /// <c>null</c> when no enemy carries any. Use it to score an untargeted cast against the pack the
    /// player was working on.
    /// </summary>
    public UnitKey? MostDottedEnemy(int timestamp)
    {
        UnitKey? best = null;
        var bestInstances = 0;
        foreach (var key in EnemiesWithAnyDot(timestamp))
        {
            var instances = InstancesOn(key, timestamp);
            if (best is null || instances > bestInstances)
                (best, bestInstances) = (key, instances);
        }
        return best;
    }

    /// <summary>
    /// One reading per effect in <see cref="Dots"/> order, taken on a single enemy, so
    /// <see cref="DotCoverage.Targets"/> is 0 or 1.
    /// </summary>
    public List<DotCoverage> CoverageOn(UnitKey enemy, int timestamp) =>
        CoverageAcross([enemy], timestamp);

    /// <summary>
    /// One reading per effect in <see cref="Dots"/> order, summed across <paramref name="enemies"/>.
    /// </summary>
    public List<DotCoverage> CoverageAcross(HashSet<UnitKey> enemies, int timestamp)
    {
        var coverage = new List<DotCoverage>(Dots.Count);
        foreach (var dot in Dots)
        {
            var carriers = 0;
            var instances = 0;
            var stacks = 0;

            foreach (var key in enemies)
            {
                var onTarget = Combatants.AuraInstanceCount(key.ActorId, key.Instance, dot.Effect, timestamp);
                if (onTarget == 0) continue;

                carriers++;
                instances += onTarget;
                stacks += Combatants.AuraStackSum(key.ActorId, key.Instance, dot.Effect, timestamp);
            }

            coverage.Add(new DotCoverage
            {
                Dot = dot,
                Targets = carriers,
                Instances = instances,
                Stacks = stacks,
            });
        }
        return coverage;
    }

    /// <summary>
    /// How every effect layered across the whole pack through
    /// <paramref name="from"/>..<paramref name="to"/>, as a sample whenever any of them opened or
    /// closed a window. The range is bookended, so the first and last sample always sit on its edges.
    /// </summary>
    public List<DotLayerSample> LayerTimeline(int from, int to)
    {
        var deltas = new List<(int Timestamp, int Index, int Delta)>();
        for (var index = 0; index < Dots.Count; index++)
        {
            foreach (var window in Enemies.AuraWindows(Dots[index].Effect, from, to))
            {
                deltas.Add((window.Start, index, 1));
                deltas.Add((window.End + 1, index, -1));
            }
        }

        deltas.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        var live = new int[Dots.Count];
        var samples = new List<DotLayerSample> { new(from, [.. live]) };

        var position = 0;
        while (position < deltas.Count && deltas[position].Timestamp <= to)
        {
            var timestamp = deltas[position].Timestamp;
            while (position < deltas.Count && deltas[position].Timestamp == timestamp)
            {
                live[deltas[position].Index] += deltas[position].Delta;
                position++;
            }

            if (timestamp == samples[^1].Timestamp)
                samples[^1] = new DotLayerSample(timestamp, [.. live]);
            else
                samples.Add(new DotLayerSample(timestamp, [.. live]));
        }

        if (samples[^1].Timestamp < to)
            samples.Add(new DotLayerSample(to, [.. live]));

        return samples;
    }
}

/// <summary>
/// How many instances of each effect were active at one instant, indexed by the effect's position in
/// <see cref="DotTracker.Dots"/>.
/// </summary>
/// <param name="Timestamp">When the reading was taken.</param>
/// <param name="Instances">Active instances per effect, in <see cref="DotTracker.Dots"/> order.</param>
public sealed record DotLayerSample(int Timestamp, List<int> Instances)
{
    /// <summary>Active instances across every effect.</summary>
    public int Total => Instances.Sum();
}
