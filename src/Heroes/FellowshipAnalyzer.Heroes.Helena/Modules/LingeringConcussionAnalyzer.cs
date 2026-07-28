using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>
/// The read surface Lingering Concussion analysis is published under, so it is indexed under its own
/// name rather than under the shared <see cref="DebuffUptimeAnalyzer"/> base.
/// </summary>
public interface ILingeringConcussionAnalyzer : IAnalyzerSurface;

/// <summary>
/// Measures Lingering Concussion, the Shield Slam debuff that takes 3% off everything the target
/// deals to Helena per stack, up to five. Uptime and gaps come from the shared debuff machinery,
/// which scopes them to whichever target carried the debuff longest, so adds passing through a pull
/// never dilute the reading. Stack time is scoped to that same target, because a stack count only
/// means anything against the enemy actually hitting her.
/// <para>
/// The log carries only the debuff itself: the <c>1002476</c> self-buff and <c>1002477</c> stacker
/// that the spell data lists alongside it never appear, so every stack reading here comes from the
/// debuff's own apply, refresh and stack events.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class LingeringConcussionAnalyzer : DebuffUptimeAnalyzer, ILingeringConcussionAnalyzer
{
    /// <summary>The stack count at which the debuff's damage reduction is at its full 15%.</summary>
    public const int MaxStacks = 5;

    /// <summary>The damage reduction each stack applies to what the target deals to Helena.</summary>
    public const double ReductionPerStack = 0.03;

    private readonly List<StackSample> _samples = [];

    /// <summary>Milliseconds the primary target carried the debuff at each stack count, indexed by stack.</summary>
    public IReadOnlyDictionary<int, int> MsAtStacks => Result.MsAtStacks;

    /// <summary>Milliseconds the primary target carried the debuff at all.</summary>
    public int CoveredMs => Result.CoveredMs;

    /// <summary>Milliseconds the primary target carried the debuff below its five-stack cap.</summary>
    public int BelowCapMs => Result.CoveredMs - Result.AtCapMs;

    /// <summary>Milliseconds the primary target carried the debuff at its five-stack cap.</summary>
    public int AtCapMs => Result.AtCapMs;

    /// <summary>Share (0-1) of the covered time spent at the five-stack cap.</summary>
    public double AtCapShare => Result.CoveredMs > 0 ? (double)Result.AtCapMs / Result.CoveredMs : 0;

    /// <summary>Share (0-1) of the pull spent at the five-stack cap.</summary>
    public double AtCapUptime
    {
        get
        {
            var duration = Pull.EndTime - Pull.StartTime;
            return duration > 0 ? Math.Clamp((double)Result.AtCapMs / duration, 0, 1) : 0;
        }
    }

    /// <summary>
    /// The damage reduction the debuff averaged over the pull, weighted by time at each stack count.
    /// Time with the debuff off the target counts as zero, so this reads against the 15% the five-stack
    /// cap allows.
    /// </summary>
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

    /// <summary>The highest stack count the primary target reached.</summary>
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
        if (PrimaryTarget is not { } primary) return new Computed(new Dictionary<int, int>(), 0, 0, 0);

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

        var covered = 0;
        var atCap = 0;
        foreach (var (stacks, ms) in msAtStacks)
        {
            covered += ms;
            if (stacks >= MaxStacks) atCap += ms;
        }

        return new Computed(msAtStacks, covered, atCap, peak);
    }

    private int LastWindowEnd() => Windows.Count > 0 ? Windows[^1].End : Pull.EndTime;

    private static void Add(Dictionary<int, int> target, int stacks, int elapsed)
    {
        if (elapsed <= 0) return;
        target[stacks] = target.GetValueOrDefault(stacks) + elapsed;
    }

    private readonly record struct StackSample(int TargetId, int TargetInstance, int Timestamp, int? Stacks);

    private sealed record Computed(
        IReadOnlyDictionary<int, int> MsAtStacks,
        int CoveredMs,
        int AtCapMs,
        int PeakStacks);
}
