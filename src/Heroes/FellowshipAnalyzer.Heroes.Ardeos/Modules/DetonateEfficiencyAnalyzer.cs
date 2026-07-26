using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

using ArdeosTalents = FellowshipAnalyzer.Core.Common.Spells.ArdeosTalents;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Measures how much of Ardeos's damage-over-time pressure each Detonate cast actually cashes in, plus
/// the free-cast economy the Apocalyptic Surge talent adds. A Detonate is only as strong as the DoTs
/// standing across the pack when it lands. Each cast is snapshotted against the six Ardeos DoTs (Searing Blaze, Engulfing Flames, Fire
/// Ball, Fire Frogs, Incinerate and Apocalypse): which of them were standing, the enemies carrying at
/// least one, and the total unique active instances across them, counted once per concurrent application
/// so a stacked Incinerate weighs the same as one window and each concurrent Engulfing Flames application
/// weighs one. <see cref="LayerTimeline"/> reconstructs the same coverage continuously across the pull so
/// the layering can be charted against the casts that cashed it in.
/// </summary>
/// <remarks>
/// Coverage is sampled per cast from the all-units aura registry, which closes every open window at an
/// enemy's death, so a dead enemy contributes nothing to casts after it dies. Because Detonate carries no
/// target and lands on every enemy at once, a cast's per-effect counts are aggregated across every enemy
/// carrying the effect, which is the payload the cast actually detonates and which reduces to the single
/// enemy's own counts on a single-target pull. The headline <see cref="AverageInstancesPerTarget"/> is the
/// mean of each cast's average instances per target and counts a Detonate fired with no DoTs standing as a
/// real sample at zero, since that is a wasted cast rather than an absent one; the well-layered and
/// under-layered shares use the same all-casts denominator.
/// <para>
/// Per-effect magnitude is read at the cast, while the parse is dispatching, because a tracked window
/// carries its live stack count rather than its count at an arbitrary timestamp. Window starts and ends
/// are historical, so <see cref="LayerTimeline"/> is reconstructed lazily instead, clipped to the pull
/// because the aura registry spans the whole fight.
/// </para>
/// <para>
/// Apocalyptic Surge grants stacking charges of a free Detonate: casting Apocalypse applies the buff and
/// every Detonate cast while it stands is free, spending a charge rather than an Ember. A cast is marked
/// free when the player carries the Surge buff at the moment it lands, read straight from the tracked
/// player aura; <see cref="SurgeBufferMs"/> keeps the final charge's Detonate free even when its stack
/// removal is logged a hair before the cast. Every charge gained that no Detonate cashes in is charged as
/// waste, so <see cref="SurgeStacksWasted"/> is simply the charges gained less the free casts and needs no
/// per-removal correlation. Surge metrics are meaningful only for a talented player, surfaced through
/// <see cref="ApocalypticSurgeTalented"/> so the guide can hide them otherwise.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
[Uses<Combatants>]
public sealed partial class DetonateEfficiencyAnalyzer : Analyzer
{
    /// <summary>Per-cast average instances per target at or above which a Detonate is well layered.</summary>
    public const int WellLayeredAverage = 3;

    /// <summary>Per-cast average instances per target at or below which a Detonate is under layered.</summary>
    public const int UnderLayeredAverage = 1;

    /// <summary>Tolerance by which the Surge buff still counts as active at a Detonate cast, absorbing a final-charge stack removal logged just before it.</summary>
    public const int SurgeBufferMs = 100;

    private readonly List<DetonateCast> _casts = [];

    private IReadOnlyList<DotLayerSample>? _layerTimeline;

    private int _surgeStacks;

    /// <summary>Every player Detonate cast in the pull with its DoT-layering snapshot, in cast order.</summary>
    public IReadOnlyList<DetonateCast> Casts => _casts;

    /// <summary>Total Detonate casts in the pull.</summary>
    public int TotalCasts => _casts.Count;

    /// <summary>Mean per-cast average instances per target across every cast, a cast with no DoTs standing counting as zero.</summary>
    public double AverageInstancesPerTarget =>
        _casts.Count == 0 ? 0 : _casts.Average(cast => cast.AverageInstances);

    /// <summary>Casts whose average instances per target reached <see cref="WellLayeredAverage"/>.</summary>
    public int WellLayeredCasts => _casts.Count(cast => cast.AverageInstances >= WellLayeredAverage);

    /// <summary>Casts whose average instances per target sat at or below <see cref="UnderLayeredAverage"/>.</summary>
    public int UnderLayeredCasts => _casts.Count(cast => cast.AverageInstances <= UnderLayeredAverage);

    /// <summary>Share of casts that were well layered.</summary>
    public double WellLayeredShare => _casts.Count == 0 ? 0 : (double)WellLayeredCasts / _casts.Count;

    /// <summary>Share of casts that were under layered.</summary>
    public double UnderLayeredShare => _casts.Count == 0 ? 0 : (double)UnderLayeredCasts / _casts.Count;

    /// <summary>Peak instances layered on a single target at any Detonate cast.</summary>
    public int MaxInstances => _casts.Count == 0 ? 0 : _casts.Max(cast => cast.MaxTargetInstances);

    /// <summary>Mean distinct damage-over-time effects standing at a cast, across every cast.</summary>
    public double AverageDistinctDots => _casts.Count == 0 ? 0 : _casts.Average(cast => cast.DistinctDots);

    /// <summary>
    /// Casts bucketed by rounded per-cast average instances per target; a cast fired with no DoTs
    /// standing buckets at zero. Keyed ascending by instance count.
    /// </summary>
    public IReadOnlyDictionary<int, int> InstanceDistribution
    {
        get
        {
            var distribution = new SortedDictionary<int, int>();
            foreach (var cast in _casts)
            {
                var bucket = (int)Math.Round(cast.AverageInstances, MidpointRounding.AwayFromZero);
                distribution[bucket] = distribution.GetValueOrDefault(bucket) + 1;
            }
            return distribution;
        }
    }

    /// <summary>
    /// The pull's damage-over-time layering as a step series: one sample at every instant a window opens
    /// or closes, each carrying the concurrently-open windows of every effect in
    /// <see cref="ArdeosDots.All"/> order summed across all enemies. The series is bracketed by a sample
    /// at the pull's start and end so it spans the pull whatever the log holds.
    /// </summary>
    public IReadOnlyList<DotLayerSample> LayerTimeline => _layerTimeline ??= BuildLayerTimeline();

    /// <summary>The peak concurrent DoT instances standing across all enemies at any instant in the pull.</summary>
    public int PeakLayeredInstances => LayerTimeline.Count == 0 ? 0 : LayerTimeline.Max(sample => sample.Total);

    /// <summary>True when the analyzed player has the Apocalyptic Surge talent.</summary>
    public bool ApocalypticSurgeTalented => Combatants.Selected.HasTalent(ArdeosTalents.ApocalypticSurge);

    /// <summary>Detonate casts made free by consuming an Apocalyptic Surge stack.</summary>
    public int FreeCasts => _casts.Count(cast => cast.Free);

    /// <summary>Detonate casts that spent an Ember (every cast that was not free).</summary>
    public int PaidCasts => _casts.Count - FreeCasts;

    /// <summary>Total Apocalyptic Surge stacks gained across the pull.</summary>
    public int SurgeStacksGained { get; private set; }

    /// <summary>Apocalyptic Surge charges gained that no free Detonate cashed in.</summary>
    public int SurgeStacksWasted => Math.Max(0, SurgeStacksGained - FreeCasts);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Detonate))]
    private void OnDetonate(CastEvent e)
    {
        var targets = new HashSet<UnitKey>();
        foreach (var dot in ArdeosDots.All)
            foreach (var key in Combatants.EnemiesWithAura(dot.EffectId, e.Timestamp))
                targets.Add(key);

        var totalInstances = 0;
        var maxTargetInstances = 0;
        foreach (var key in targets)
        {
            var perTarget = 0;
            foreach (var dot in ArdeosDots.All)
                perTarget += Combatants.AuraInstanceCount(key.ActorId, key.Instance, dot.EffectId, e.Timestamp);
            totalInstances += perTarget;
            if (perTarget > maxTargetInstances)
                maxTargetInstances = perTarget;
        }

        _casts.Add(new DetonateCast
        {
            Timestamp = e.Timestamp,
            TargetsWithDoTs = targets.Count,
            TotalInstances = totalInstances,
            MaxTargetInstances = maxTargetInstances,
            Coverage = SnapshotCoverage(targets, e.Timestamp),
            Free = Owner.SelectedCombatant.HasBuff(Spells.ApocalypticSurge.FSLID, e.Timestamp, bufferTime: SurgeBufferMs),
        });
    }

    private IReadOnlyList<DotCoverage> SnapshotCoverage(HashSet<UnitKey> targets, int timestamp)
    {
        var coverage = new List<DotCoverage>(ArdeosDots.Count);
        foreach (var dot in ArdeosDots.All)
        {
            var effectId = dot.EffectId;
            var carriers = 0;
            var instances = 0;
            var stacks = 0;

            foreach (var key in targets)
            {
                var onTarget = Combatants.AuraInstanceCount(key.ActorId, key.Instance, effectId, timestamp);
                if (onTarget == 0) continue;

                carriers++;
                instances += onTarget;
                stacks += Combatants.AuraStackSum(key.ActorId, key.Instance, effectId, timestamp);
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

    private IReadOnlyList<DotLayerSample> BuildLayerTimeline()
    {
        var from = Pull.StartTime;
        var to = Pull.EndTime;

        var deltas = new List<(int Timestamp, int Index, int Delta)>();
        for (var index = 0; index < ArdeosDots.Count; index++)
        {
            foreach (var window in Combatants.EnemyAuraWindows(ArdeosDots.All[index].EffectId, from, to))
            {
                deltas.Add((window.Start, index, 1));
                deltas.Add((window.End + 1, index, -1));
            }
        }

        deltas.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        var live = new int[ArdeosDots.Count];
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

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ApocalypticSurge))]
    private void OnSurgeApply(ApplyBuffEvent e)
    {
        _surgeStacks = 1;
        SurgeStacksGained += 1;
    }

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ApocalypticSurge))]
    private void OnSurgeApplyStack(ApplyBuffStackEvent e)
    {
        if (e.Stack > _surgeStacks)
            SurgeStacksGained += e.Stack - _surgeStacks;
        _surgeStacks = e.Stack;
    }

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ApocalypticSurge))]
    private void OnSurgeRemoveStack(RemoveBuffStackEvent e) => _surgeStacks = e.Stack;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ApocalypticSurge))]
    private void OnSurgeRemove(RemoveBuffEvent e) => _surgeStacks = 0;

    /// <summary>
    /// One damage-over-time effect's coverage at a Detonate cast, aggregated across every enemy, since
    /// Detonate lands on all of them at once.
    /// </summary>
    public sealed record DotCoverage : ArdeosDotCoverage
    {
        /// <summary>Enemies carrying at least one open window of this effect at the cast.</summary>
        public required int Targets { get; init; }
    }

    /// <summary>
    /// The layering standing at one instant in the pull: the concurrently open windows of each effect in
    /// <see cref="ArdeosDots.All"/> order, summed across every enemy.
    /// </summary>
    public sealed record DotLayerSample(int Timestamp, IReadOnlyList<int> Instances)
    {
        /// <summary>Every effect's open windows at this instant, across all enemies.</summary>
        public int Total => Instances.Sum();
    }

    /// <summary>A single Detonate cast and the DoT layering standing on its targets when it landed.</summary>
    public sealed class DetonateCast
    {
        public required int Timestamp { get; init; }

        /// <summary>Enemies carrying at least one of the six Ardeos DoTs at the cast.</summary>
        public required int TargetsWithDoTs { get; init; }

        /// <summary>Total unique active DoT instances summed across every target at the cast.</summary>
        public required int TotalInstances { get; init; }

        /// <summary>The most active DoT instances standing on any single target at the cast.</summary>
        public required int MaxTargetInstances { get; init; }

        /// <summary>Per-effect coverage at the cast, in <see cref="ArdeosDots.All"/> order.</summary>
        public required IReadOnlyList<DotCoverage> Coverage { get; init; }

        /// <summary>Average active DoT instances per DoT-carrying target, or zero when none carried a DoT.</summary>
        public double AverageInstances => TargetsWithDoTs == 0 ? 0 : (double)TotalInstances / TargetsWithDoTs;

        /// <summary>How many of the six damage-over-time effects were standing anywhere at the cast.</summary>
        public int DistinctDots => Coverage.Count(entry => entry.Active);

        /// <summary>True when the player carried an Apocalyptic Surge charge as this cast landed, spending no Ember.</summary>
        public required bool Free { get; init; }
    }
}
