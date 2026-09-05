using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

public interface ILingeringConcussionAnalyzer : IAnalyzerSurface;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class LingeringConcussionAnalyzer : DebuffUptimeAnalyzer, ILingeringConcussionAnalyzer
{
    public const int MaxStacks = 5;

    public const double ReductionPerStack = 0.03;

    private readonly List<StackSample> _samples = [];

    public Dictionary<int, int> MsAtStacks => Result.MsAtStacks;

    public int ActiveMs => Result.ActiveMs;

    public int BelowCapMs => Result.ActiveMs - Result.AtCapMs;

    public int AtCapMs => Result.AtCapMs;

    public double AtCapShare => Result.ActiveMs > 0 ? (double)Result.AtCapMs / Result.ActiveMs : 0;

    public double AtCapUptime
    {
        get
        {
            var duration = Pull.EndTime - Pull.StartTime;
            return duration > 0 ? Math.Clamp((double)Result.AtCapMs / duration, 0, 1) : 0;
        }
    }

    public double AverageReduction
    {
        get
        {
            var duration = Pull.EndTime - Pull.StartTime;
            if (duration <= 0) return 0;

            var weighted = 0d;
            foreach (var (stacks, ms) in Result.MsAtStacks)
                weighted += Math.Min(stacks, MaxStacks) * ReductionPerStack * ms;

            return weighted / duration;
        }
    }

    public int PeakStacks => Result.PeakStacks;

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.ShieldSlamReducedIncomingDamageFromTargetDebuff))]
    private void OnApplied(ApplyDebuffEvent debuffEvent)
    {
        OpenWindow(debuffEvent, debuffEvent.Timestamp);
        Sample(debuffEvent, debuffEvent.Timestamp, 1);
    }

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.ShieldSlamReducedIncomingDamageFromTargetDebuff))]
    private void OnRefreshed(RefreshDebuffEvent debuffEvent)
    {
        OpenWindow(debuffEvent, debuffEvent.Timestamp);
        Sample(debuffEvent, debuffEvent.Timestamp, null);
    }

    [On<ApplyDebuffStackEvent>(By = Actor.Player, Spell = nameof(Spells.ShieldSlamReducedIncomingDamageFromTargetDebuff))]
    private void OnStackGained(ApplyDebuffStackEvent debuffEvent)
    {
        ObserveTarget(debuffEvent, debuffEvent.Timestamp);
        Sample(debuffEvent, debuffEvent.Timestamp, debuffEvent.Stack);
    }

    [On<RemoveDebuffStackEvent>(By = Actor.Player, Spell = nameof(Spells.ShieldSlamReducedIncomingDamageFromTargetDebuff))]
    private void OnStackLost(RemoveDebuffStackEvent debuffEvent)
    {
        ObserveTarget(debuffEvent, debuffEvent.Timestamp);
        Sample(debuffEvent, debuffEvent.Timestamp, debuffEvent.Stack);
    }

    [On<RemoveDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.ShieldSlamReducedIncomingDamageFromTargetDebuff))]
    private void OnRemoved(RemoveDebuffEvent debuffEvent)
    {
        CloseWindow(debuffEvent, debuffEvent.Timestamp);
        Sample(debuffEvent, debuffEvent.Timestamp, 0);
    }

    private void Sample(IHasTargetWithInstanceEvent target, int timestamp, int? stacks) =>
        _samples.Add(new StackSample(target.TargetId, target.TargetInstance ?? 0, timestamp, stacks));

    private Computed Result => field ??= Compute();

    private Computed Compute()
    {
        if (PrimaryTarget is not { } primary) return new Computed([], 0, 0, 0);

        var msAtStacks = new Dictionary<int, int>();
        var peak = 0;
        var current = 0;
        int? since = null;

        foreach (var sample in _samples)
        {
            if (sample.TargetId != primary.TargetId || sample.TargetInstance != primary.TargetInstance) continue;

            if (since is { } from && current > 0)
                Add(msAtStacks, current, Math.Clamp(sample.Timestamp, Pull.StartTime, Pull.EndTime) - from);

            current = sample.Stacks ?? Math.Max(current, 1);
            peak = Math.Max(peak, current);
            since = Math.Clamp(sample.Timestamp, Pull.StartTime, Pull.EndTime);
        }

        if (since is { } last && current > 0)
            Add(msAtStacks, current, Math.Max(0, LastWindowEnd() - last));

        var active = 0;
        var atCap = 0;
        foreach (var (stacks, ms) in msAtStacks)
        {
            active += ms;
            if (stacks >= MaxStacks) atCap += ms;
        }

        return new Computed(msAtStacks, active, atCap, peak);
    }

    private int LastWindowEnd() => Windows.Count > 0 ? Windows[^1].End : Pull.EndTime;

    private static void Add(Dictionary<int, int> target, int stacks, int elapsed)
    {
        if (elapsed <= 0) return;
        target[stacks] = target.GetValueOrDefault(stacks) + elapsed;
    }

    private readonly record struct StackSample(int TargetId, int TargetInstance, int Timestamp, int? Stacks);

    private sealed record Computed(
        Dictionary<int, int> MsAtStacks,
        int ActiveMs,
        int AtCapMs,
        int PeakStacks);
}
