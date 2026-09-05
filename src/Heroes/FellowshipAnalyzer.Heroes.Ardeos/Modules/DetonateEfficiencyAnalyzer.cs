using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Ardeos.Core;

using ArdeosTalents = FellowshipAnalyzer.Core.Common.Spells.ArdeosTalents;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<ArdeosDotTracker>]
public sealed partial class DetonateEfficiencyAnalyzer : Analyzer
{
    public const int WellLayeredAverage = 3;

    public const int UnderLayeredAverage = 1;

    public const int SurgeBufferMs = 100;

    private readonly List<DetonateCast> _casts = [];

    private int _surgeStacks;

    public List<DetonateCast> Casts => _casts;

    public int TotalCasts => _casts.Count;

    public double AverageInstancesPerTarget => _casts.Count == 0 ? 0 : _casts.Average(cast => cast.AverageInstances);

    public int WellLayeredCasts => _casts.Count(cast => cast.AverageInstances >= WellLayeredAverage);

    public int UnderLayeredCasts => _casts.Count(cast => cast.AverageInstances <= UnderLayeredAverage);

    public double WellLayeredShare => _casts.Count == 0 ? 0 : (double)WellLayeredCasts / _casts.Count;

    public double UnderLayeredShare => _casts.Count == 0 ? 0 : (double)UnderLayeredCasts / _casts.Count;

    public int MaxInstances => _casts.Count == 0 ? 0 : _casts.Max(cast => cast.MaxTargetInstances);

    public double AverageDistinctDots => _casts.Count == 0 ? 0 : _casts.Average(cast => cast.DistinctDots);

    public SortedDictionary<int, int> InstanceDistribution
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

    public List<DotLayerSample> LayerTimeline => field ??= ArdeosDotTracker.LayerTimeline(Pull.StartTime, Pull.EndTime);

    public int PeakLayeredInstances => LayerTimeline.Count == 0 ? 0 : LayerTimeline.Max(sample => sample.Total);

    public bool ApocalypticSurgeTalented => Owner.SelectedCombatant.HasTalent(ArdeosTalents.ApocalypticSurge);

    public int FreeCasts => _casts.Count(cast => cast.Free);

    public int NotFreeCasts => _casts.Count - FreeCasts;

    public int SurgeStacksGained { get; private set; }

    public int SurgeStacksWasted => Math.Max(0, SurgeStacksGained - FreeCasts);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Detonate))]
    private void OnDetonate(CastEvent e)
    {
        var targets = ArdeosDotTracker.EnemiesWithAnyDot(e.Timestamp);

        var totalInstances = 0;
        var maxTargetInstances = 0;
        foreach (var key in targets)
        {
            var perTarget = ArdeosDotTracker.InstancesOn(key, e.Timestamp);
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
            Coverage = ArdeosDotTracker.CoverageAcross(targets, e.Timestamp),
            Free = Owner.SelectedCombatant.HasBuff(Spells.ApocalypticSurge, e.Timestamp, bufferTime: SurgeBufferMs),
        });
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ApocalypticSurge))]
    private void OnSurgeApply()
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
    private void OnSurgeRemove() => _surgeStacks = 0;

    public sealed class DetonateCast
    {
        public required int Timestamp { get; init; }

        public required int TargetsWithDoTs { get; init; }

        public required int TotalInstances { get; init; }

        public required int MaxTargetInstances { get; init; }

        public required List<DotCoverage> Coverage { get; init; }

        public double AverageInstances => TargetsWithDoTs == 0 ? 0 : (double)TotalInstances / TargetsWithDoTs;

        public int DistinctDots => Coverage.Count(entry => entry.Active);

        public required bool Free { get; init; }
    }
}
