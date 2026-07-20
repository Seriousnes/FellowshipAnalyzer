using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

using ArdeosTalents = FellowshipAnalyzer.Core.Common.Spells.ArdeosTalents;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Measures how much of Ardeos's damage-over-time pressure each Detonate cast actually cashes in, plus
/// the free-cast economy the Apocalyptic Surge talent adds. Detonate re-triggers every active DoT window
/// on every enemy at the moment it is cast, so a Detonate is only as strong as the DoTs standing when it
/// lands. Each cast is snapshotted against the six Ardeos DoTs (Searing Blaze, Engulfing Flames, Fire
/// Ball, Fire Frogs, Incinerate and Apocalypse): the enemies carrying at least one of them and the total
/// unique active instances across them, counted once per concurrent application so a stacked Incinerate
/// weighs the same as one window and each concurrent Engulfing Flames application weighs one.
/// </summary>
/// <remarks>
/// Coverage is sampled per cast from the all-units aura registry, which closes every open window at an
/// enemy's death, so a dead enemy contributes nothing to casts after it dies. The headline
/// <see cref="AverageInstancesPerTarget"/> is the mean of each cast's average instances per target and
/// counts a Detonate fired with no DoTs standing as a real sample at zero, since that is a wasted cast
/// rather than an absent one; the well-layered and under-layered shares use the same all-casts
/// denominator.
/// <para>
/// Apocalyptic Surge grants stacking charges of a free Detonate: casting Apocalypse applies the buff and
/// each free Detonate consumes a stack, logged as a Surge stack removal about a millisecond after the
/// cast. A removal within <see cref="SurgeCorrelationWindowMs"/> of a Detonate marks that cast free (no
/// Ember spent); any other removal is the buff expiring with stacks unspent and is charged as waste. The
/// buff is never refreshed and its eighteen-second window is shorter than Apocalypse's cooldown, so a
/// fresh apply while stacks remain does not occur in practice; that overwrite path is still accounted as
/// waste defensively. Surge metrics are meaningful only for a talented player, surfaced through
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

    /// <summary>Maximum gap after a Detonate cast within which a Surge stack removal marks that cast free.</summary>
    public const int SurgeCorrelationWindowMs = 100;

    private static readonly int[] DotEffectIds =
    [
        Spells.SearingBlazeDot.FSLID,
        Spells.EngulfingFlamesDot.FSLID,
        Spells.FireBallDot.FSLID,
        Spells.FireFrogsDot.FSLID,
        Spells.IncinerateDot.FSLID,
        Spells.ApocalypseDot.FSLID,
    ];

    private readonly List<DetonateCast> _casts = [];

    private int _surgeStacks;
    private DetonateCast? _pendingDetonate;
    private int _pendingDetonateTimestamp;

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

    /// <summary>True when the analyzed player has the Apocalyptic Surge talent.</summary>
    public bool ApocalypticSurgeTalented => Combatants.Selected.HasTalent(ArdeosTalents.ApocalypticSurge);

    /// <summary>Detonate casts made free by consuming an Apocalyptic Surge stack.</summary>
    public int FreeCasts => _casts.Count(cast => cast.Free);

    /// <summary>Detonate casts that spent an Ember (every cast that was not free).</summary>
    public int PaidCasts => _casts.Count - FreeCasts;

    /// <summary>Total Apocalyptic Surge stacks gained across the pull.</summary>
    public int SurgeStacksGained { get; private set; }

    /// <summary>Apocalyptic Surge stacks lost to expiry or overwrite rather than a free Detonate.</summary>
    public int SurgeStacksWasted { get; private set; }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Detonate))]
    private void OnDetonate(CastEvent e)
    {
        var targets = new HashSet<UnitKey>();
        foreach (var effectId in DotEffectIds)
            foreach (var key in Combatants.EnemiesWithAura(effectId, e.Timestamp))
                targets.Add(key);

        var totalInstances = 0;
        var maxTargetInstances = 0;
        foreach (var key in targets)
        {
            var perTarget = 0;
            foreach (var effectId in DotEffectIds)
                perTarget += Combatants.AuraInstanceCount(key.ActorId, key.Instance, effectId, e.Timestamp);
            totalInstances += perTarget;
            if (perTarget > maxTargetInstances)
                maxTargetInstances = perTarget;
        }

        var cast = new DetonateCast
        {
            Timestamp = e.Timestamp,
            TargetsWithDoTs = targets.Count,
            TotalInstances = totalInstances,
            MaxTargetInstances = maxTargetInstances,
        };
        _casts.Add(cast);
        _pendingDetonate = cast;
        _pendingDetonateTimestamp = e.Timestamp;
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ApocalypticSurge))]
    private void OnSurgeApply(ApplyBuffEvent e)
    {
        if (_surgeStacks > 0)
            SurgeStacksWasted += _surgeStacks;
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
    private void OnSurgeRemoveStack(RemoveBuffStackEvent e)
    {
        var removed = Math.Max(0, _surgeStacks - e.Stack);
        _surgeStacks = e.Stack;
        AccountForSurgeRemoval(e.Timestamp, removed);
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ApocalypticSurge))]
    private void OnSurgeRemove(RemoveBuffEvent e)
    {
        var removed = _surgeStacks;
        _surgeStacks = 0;
        AccountForSurgeRemoval(e.Timestamp, removed);
    }

    private void AccountForSurgeRemoval(int timestamp, int removed)
    {
        if (removed <= 0)
            return;

        if (_pendingDetonate is { Free: false } pending
            && timestamp - _pendingDetonateTimestamp <= SurgeCorrelationWindowMs)
        {
            pending.Free = true;
            _pendingDetonate = null;
            removed -= 1;
        }

        SurgeStacksWasted += removed;
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

        /// <summary>Average active DoT instances per DoT-carrying target, or zero when none carried a DoT.</summary>
        public double AverageInstances => TargetsWithDoTs == 0 ? 0 : (double)TotalInstances / TargetsWithDoTs;

        /// <summary>True when this cast consumed an Apocalyptic Surge stack and spent no Ember.</summary>
        public bool Free { get; internal set; }
    }
}
