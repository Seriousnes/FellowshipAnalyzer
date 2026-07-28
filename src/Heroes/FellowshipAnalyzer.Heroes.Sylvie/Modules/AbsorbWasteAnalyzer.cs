using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;

using SylvieTalents = FellowshipAnalyzer.Core.Common.Spells.SylvieTalents;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>
/// What Natural Protector's shields absorbed against what expired still on them. The removal event
/// carries the absorb that was left, so the waste is a reading rather than an inference; a shield that
/// was consumed to nothing carries none.
/// <para>
/// Only Natural Protector is measured. Ironleaf Ward reduces damage rather than absorbing it and its
/// removals carry no absorb at all, so it has nothing to waste.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[RequiresTalent(SylvieTalents.NaturalProtector)]
public sealed partial class AbsorbWasteAnalyzer : Analyzer
{
    private readonly List<ShieldCapture> _shields = [];
    private readonly Dictionary<int, ShieldCapture> _open = [];

    private Computed Result => field ??= Compute();

    /// <summary>Every shield laid this pull, in order.</summary>
    public IReadOnlyList<ShieldUse> Shields => Result.Shields;

    /// <summary>Shields laid this pull, counting a reapplication onto a live one as a fresh shield.</summary>
    public int ShieldsLaid => Result.Shields.Count;

    /// <summary>Damage the shields consumed this pull.</summary>
    public long AbsorbUsed => Result.Used;

    /// <summary>Absorb still on a shield when it came off.</summary>
    public long AbsorbWasted => Result.Wasted;

    /// <summary>Shields that came off still holding absorb.</summary>
    public int ShieldsExpiredUnspent => Result.ExpiredUnspent;

    /// <summary>Share (0-1) of the absorb the shields offered that was consumed rather than left to expire.</summary>
    public double AbsorbEfficiency
    {
        get
        {
            var offered = Result.Used + Result.Wasted;
            return offered > 0 ? Result.Used / (double)offered : 0;
        }
    }

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.NaturalProtectorAbsorb))]
    private void OnShieldApplied(ApplyBuffEvent buffEvent) => Open(buffEvent.TargetId, buffEvent.Timestamp);

    [On<RefreshBuffEvent>(By = Actor.Player, Spell = nameof(Spells.NaturalProtectorAbsorb))]
    private void OnShieldRefreshed(RefreshBuffEvent buffEvent) => Open(buffEvent.TargetId, buffEvent.Timestamp);

    [On<RemoveBuffEvent>(By = Actor.Player, Spell = nameof(Spells.NaturalProtectorAbsorb))]
    private void OnShieldRemoved(RemoveBuffEvent buffEvent)
    {
        if (!_open.Remove(buffEvent.TargetId, out var shield)) return;

        shield.End = buffEvent.Timestamp;
        shield.Remaining = buffEvent.Absorb ?? 0;
    }

    [On<AbsorbedEvent>(Spell = nameof(Spells.NaturalProtectorAbsorb))]
    private void OnAbsorbed(AbsorbedEvent absorbedEvent)
    {
        if (_open.TryGetValue(absorbedEvent.TargetId, out var shield))
            shield.Absorbed += absorbedEvent.Amount;
    }

    private void Open(int targetId, int timestamp)
    {
        if (_open.TryGetValue(targetId, out var running))
        {
            running.End = timestamp;
            running.Reapplied = true;
        }

        var shield = new ShieldCapture { TargetId = targetId, Start = timestamp, End = timestamp };
        _shields.Add(shield);
        _open[targetId] = shield;
    }

    private Computed Compute()
    {
        var shields = new List<ShieldUse>(_shields.Count);
        long used = 0, wasted = 0;
        var expiredUnspent = 0;

        foreach (var capture in _shields)
        {
            var truncated = _open.ContainsValue(capture);
            var end = truncated ? Math.Max(capture.End, Pull.EndTime) : capture.End;

            shields.Add(new ShieldUse(
                capture.TargetId,
                capture.Start,
                end,
                capture.Absorbed,
                capture.Remaining,
                truncated,
                capture.Reapplied));

            used += capture.Absorbed;
            wasted += capture.Remaining;
            if (capture.Remaining > 0) expiredUnspent++;
        }

        return new Computed(shields, used, wasted, expiredUnspent);
    }

    private sealed class ShieldCapture
    {
        public int TargetId { get; init; }
        public int Start { get; init; }
        public int End { get; set; }
        public long Absorbed { get; set; }
        public long Remaining { get; set; }
        public bool Reapplied { get; set; }
    }

    private sealed record Computed(
        IReadOnlyList<ShieldUse> Shields,
        long Used,
        long Wasted,
        int ExpiredUnspent);
}

/// <summary>One Natural Protector shield and what became of it.</summary>
/// <param name="TargetId">The ally it was laid on.</param>
/// <param name="Start">When it went up.</param>
/// <param name="End">When it came off, or the pull's end when <paramref name="Truncated"/>.</param>
/// <param name="Absorbed">Damage it consumed.</param>
/// <param name="Remaining">Absorb still on it when it came off.</param>
/// <param name="Truncated">Whether the pull ended before the shield did.</param>
/// <param name="Reapplied">Whether a fresh shield replaced this one before it expired.</param>
public sealed record ShieldUse(
    int TargetId,
    int Start,
    int End,
    long Absorbed,
    long Remaining,
    bool Truncated,
    bool Reapplied)
{
    /// <summary>How long the shield was up, in milliseconds.</summary>
    public int DurationMs => End - Start;

    /// <summary>Whether the shield came off with absorb still on it.</summary>
    public bool ExpiredUnspent => Remaining > 0;
}
