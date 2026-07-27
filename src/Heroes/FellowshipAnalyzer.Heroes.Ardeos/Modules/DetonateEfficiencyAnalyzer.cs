using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

using ArdeosTalents = FellowshipAnalyzer.Core.Common.Spells.ArdeosTalents;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<Combatants>]
public sealed partial class DetonateEfficiencyAnalyzer : Analyzer
{
    public const int WellLayeredAverage = 3;

    public const int UnderLayeredAverage = 1;

    public const int SurgeBufferMs = 100;

    private readonly List<DetonateCast> _casts = [];

    private int _surgeStacks;

    public IReadOnlyList<DetonateCast> Casts => _casts;

    public int TotalCasts => _casts.Count;

    public double AverageInstancesPerTarget => _casts.Count == 0 ? 0 : _casts.Average(cast => cast.AverageInstances);

    public int WellLayeredCasts => _casts.Count(cast => cast.AverageInstances >= WellLayeredAverage);

    public int UnderLayeredCasts => _casts.Count(cast => cast.AverageInstances <= UnderLayeredAverage);

    public double WellLayeredShare => _casts.Count == 0 ? 0 : (double)WellLayeredCasts / _casts.Count;

    public double UnderLayeredShare => _casts.Count == 0 ? 0 : (double)UnderLayeredCasts / _casts.Count;

    public int MaxInstances => _casts.Count == 0 ? 0 : _casts.Max(cast => cast.MaxTargetInstances);

    public double AverageDistinctDots => _casts.Count == 0 ? 0 : _casts.Average(cast => cast.DistinctDots);

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

    public IReadOnlyList<DotLayerSample> LayerTimeline => field ??= BuildLayerTimeline();

    public int PeakLayeredInstances => LayerTimeline.Count == 0 ? 0 : LayerTimeline.Max(sample => sample.Total);

    public bool ApocalypticSurgeTalented => Combatants.Selected.HasTalent(ArdeosTalents.ApocalypticSurge);

    public int FreeCasts => _casts.Count(cast => cast.Free);

    public int PaidCasts => _casts.Count - FreeCasts;

    public int SurgeStacksGained { get; private set; }

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

    public sealed record DotCoverage : ArdeosDotCoverage
    {
        public required int Targets { get; init; }
    }

    public sealed record DotLayerSample(int Timestamp, IReadOnlyList<int> Instances)
    {
        public int Total => Instances.Sum();
    }

    public sealed class DetonateCast
    {
        public required int Timestamp { get; init; }

        public required int TargetsWithDoTs { get; init; }

        public required int TotalInstances { get; init; }

        public required int MaxTargetInstances { get; init; }

        public required IReadOnlyList<DotCoverage> Coverage { get; init; }

        public double AverageInstances => TargetsWithDoTs == 0 ? 0 : (double)TotalInstances / TargetsWithDoTs;

        public int DistinctDots => Coverage.Count(entry => entry.Active);

        public required bool Free { get; init; }
    }
}
