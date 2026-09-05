using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>The pull read surface for Entropy's Claim.</summary>
public interface IEntropyClaimAnalyzer : IAnalyzerSurface;

/// <summary>
/// Entropy's Claim over one pull: the dot's windows on every enemy it was applied to, the ticks and
/// Chrona each cast returned, the time the ability sat available and uncast, and the Entropic Burst
/// the dot's expiry produced when the talent is taken.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<ChronaTracker>]
[Dependency<SpellUsable>]
public sealed partial class EntropyClaimAnalyzer : AllTargetUptimeAnalyzer, IEntropyClaimAnalyzer
{
    /// <summary>Milliseconds between an Entropy's Claim cast and the dot application credited to it.</summary>
    public const int CastLinkToleranceMs = 100;

    /// <summary>Milliseconds after a dot expiry within which an Entropic Burst application is credited to that cast.</summary>
    public const int EntropicBurstAttributionMs = 250;

    private readonly List<CastState> _casts = [];
    private readonly Dictionary<UnitKey, CastState> _openDots = [];
    private readonly Dictionary<UnitKey, List<StackSample>> _burstStacks = [];
    private readonly List<AvailabilityChange> _availability = [];

    private CastState? _lastExpired;
    private int _lastExpiredAt = int.MinValue;

    /// <summary>Every Entropy's Claim cast in the pull, in cast order.</summary>
    public IReadOnlyList<EntropyClaimCast> Casts => field ??= [.. _casts.Select(Build)];

    /// <summary>Entropy's Claim casts in the pull.</summary>
    public int CastCount => _casts.Count;

    /// <summary>Milliseconds of the pull with the dot active on at least one enemy.</summary>
    public int ActiveMs => AuraWindowLedger.ActiveMs(DotWindows);

    /// <summary>Share of the pull (0-1) with the dot active on at least one enemy.</summary>
    public double Uptime => Pull.Duration > 0 ? Math.Min(1d, ActiveMs / (double)Pull.Duration) : 0;

    /// <summary>Milliseconds of the pull Entropy's Claim was off cooldown.</summary>
    public int AvailableMs => AvailableWindows.Sum(window => window.Duration);

    /// <summary>
    /// Every wait between a charge of Entropy's Claim becoming available and the cast that spent it,
    /// with a charge still available when the pull ended contributing the wait running to the pull end.
    /// Exposed so a caller covering several pulls averages the waits themselves rather than their means.
    /// </summary>
    public IReadOnlyList<int> DelaysAfterReady => DelayEntries;

    /// <summary>Mean milliseconds of <see cref="DelaysAfterReady"/>.</summary>
    public double AverageDelayAfterReadyMs => DelayEntries.Count == 0 ? 0 : DelayEntries.Average();

    /// <summary>Dot damage ticks across every cast.</summary>
    public int TickCount => _casts.Sum(cast => cast.TickTimestamps.Count);

    /// <summary>Dot damage ticks per cast.</summary>
    public double TicksPerCast => _casts.Count == 0 ? 0 : (double)TickCount / _casts.Count;

    /// <summary>Chrona the ticks of every application generated.</summary>
    public int ChronaGenerated => Casts.Sum(cast => cast.ChronaGenerated);

    /// <summary>Chrona the ticks of every application generated above the maximum.</summary>
    public int ChronaOvercapped => Casts.Sum(cast => cast.ChronaOvercapped);

    /// <summary>Whether the player took the Entropic Burst talent.</summary>
    public bool EntropicBurstTaken => Owner.SelectedCombatant.HasTalent(AeonaTalents.EntropicBurst);

    /// <summary>Entropic Burst stacks applied at every expiry in the pull, or <c>null</c> without the talent.</summary>
    public int? EntropicBurstStacks => EntropicBurstTaken ? _casts.Sum(cast => cast.BurstStacks) : null;

    /// <summary>Entropic Burst stacks per Entropy's Claim cast, or <c>null</c> without the talent.</summary>
    public double? EntropicBurstStacksPerCast => EntropicBurstStacks is { } stacks && _casts.Count > 0
        ? (double)stacks / _casts.Count
        : null;

    /// <summary>
    /// Milliseconds of the pull Entropic Burst was active on at least one enemy, counting a moment once,
    /// or <c>null</c> without the talent. The numerator <see cref="EntropicBurstUptime"/> divides, exposed
    /// so a caller covering several pulls can weight them by length instead of averaging their shares.
    /// </summary>
    public long? EntropicBurstActiveMs => EntropicBurstTaken ? AuraWindowLedger.ActiveMs(Burst.Windows) : null;

    /// <summary>
    /// Milliseconds Entropic Burst was active summed across enemies, counting a moment once per enemy
    /// carrying it, or <c>null</c> without the talent. The denominator
    /// <see cref="EntropicBurstAverageStacks"/> divides, exposed so a caller covering several pulls can
    /// weight them by carried time instead of averaging their means.
    /// </summary>
    public long? EntropicBurstUnitActiveMs => EntropicBurstTaken ? Burst.UnitActiveMs : null;

    /// <summary>Share of the pull (0-1) Entropic Burst was active on at least one enemy, or <c>null</c> without the talent.</summary>
    public double? EntropicBurstUptime => EntropicBurstActiveMs is { } activeMs && Pull.Duration > 0
        ? Math.Min(1d, activeMs / (double)Pull.Duration)
        : null;

    /// <summary>
    /// Stack-weighted active time in millisecond-stacks: each enemy's active milliseconds multiplied by
    /// the stacks it carried through them, summed. <c>null</c> without the talent.
    /// </summary>
    public long? EntropicBurstStackMs => EntropicBurstTaken ? Burst.StackMs : null;

    /// <summary>Mean Entropic Burst stacks carried while it was active, or <c>null</c> without the talent.</summary>
    public double? EntropicBurstAverageStacks => EntropicBurstTaken && Burst.UnitActiveMs > 0
        ? Burst.StackMs / (double)Burst.UnitActiveMs
        : null;

    private List<AuraWindow> DotWindows => field ??= [.. TargetUptimes.SelectMany(target => target.Windows)];

    private List<AuraWindow> AvailableWindows => field ??= BuildAvailableWindows();

    private List<int> DelayEntries => field ??= BuildDelayEntries();

    private BurstSummary Burst => field ??= SummariseBurst();

    [On<PullStartEvent>]
    private void OnPullStart(PullStartEvent e) =>
        _availability.Add(new AvailabilityChange(
            e.StartTime,
            SpellUsable.CooldownRemaining(Spells.EntropyClaim.FSLID, e.StartTime) <= 0));

    [On<UpdateSpellUsableEvent>(By = Actor.Player, Spell = nameof(Spells.EntropyClaim))]
    private void OnUsableChanged(UpdateSpellUsableEvent e) =>
        _availability.Add(new AvailabilityChange(e.Timestamp, e.IsAvailable));

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.EntropyClaim))]
    private void OnCast(CastEvent e) => _casts.Add(new CastState(e.Timestamp));

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.EntropyClaimDot))]
    private void OnDotApplied(ApplyDebuffEvent e)
    {
        OpenWindow(e, e.Timestamp);
        OpenDot(e, e.Timestamp);
    }

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.EntropyClaimDot))]
    private void OnDotRefreshed(RefreshDebuffEvent e)
    {
        OpenWindow(e, e.Timestamp);
        OpenDot(e, e.Timestamp);
    }

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.EntropyClaimDot))]
    private void OnDotTicked(DamageEvent e)
    {
        ObserveTarget(e, e.Timestamp);

        if (!_openDots.TryGetValue(AuraWindowLedger.KeyOf(e), out var state)) return;

        state.TickTimestamps.Add(e.Timestamp);
        state.DotEnd = Math.Max(state.DotEnd, e.Timestamp);
    }

    [On<RemoveDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.EntropyClaimDot))]
    private void OnDotRemoved(RemoveDebuffEvent e)
    {
        CloseWindow(e, e.Timestamp);

        if (!_openDots.Remove(AuraWindowLedger.KeyOf(e), out var state)) return;

        state.DotEnd = Math.Max(state.DotEnd, e.Timestamp);
        _lastExpired = state;
        _lastExpiredAt = e.Timestamp;
    }

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.EntropicBurst))]
    private void OnBurstApplied(ApplyDebuffEvent e)
    {
        RecordBurstStacks(e, e.Timestamp, 1);
        CreditBurst(e);
    }

    [On<ApplyDebuffStackEvent>(By = Actor.Player, Spell = nameof(Spells.EntropicBurst))]
    private void OnBurstStacked(ApplyDebuffStackEvent e)
    {
        RecordBurstStacks(e, e.Timestamp, e.Stack);
        CreditBurst(e);
    }

    [On<RemoveDebuffStackEvent>(By = Actor.Player, Spell = nameof(Spells.EntropicBurst))]
    private void OnBurstStackRemoved(RemoveDebuffStackEvent e) =>
        RecordBurstStacks(e, e.Timestamp, e.Stack);

    [On<RemoveDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.EntropicBurst))]
    private void OnBurstRemoved(RemoveDebuffEvent e) =>
        RecordBurstStacks(e, e.Timestamp, 0);

    private void OpenDot(IHasTargetWithInstanceEvent target, int timestamp)
    {
        var unit = AuraWindowLedger.KeyOf(target);

        if (_openDots.TryGetValue(unit, out var open))
        {
            open.DotEnd = Math.Max(open.DotEnd, timestamp);
            return;
        }

        if (PendingCast(timestamp) is not { } state) return;

        state.Unit = unit;
        state.DotStart = timestamp;
        state.DotEnd = timestamp;
        _openDots[unit] = state;
    }

    private CastState? PendingCast(int timestamp) =>
        _casts.Count > 0
        && _casts[^1].DotStart is null
        && timestamp - _casts[^1].Timestamp <= CastLinkToleranceMs
            ? _casts[^1]
            : null;

    private void CreditBurst(BuffEvent e)
    {
        if (_lastExpired is not { } state) return;
        if (e.Timestamp - _lastExpiredAt > EntropicBurstAttributionMs) return;

        state.BurstStacks++;
    }

    private void RecordBurstStacks(IHasTargetWithInstanceEvent target, int timestamp, int stacks)
    {
        var unit = AuraWindowLedger.KeyOf(target);

        if (!_burstStacks.TryGetValue(unit, out var samples))
        {
            samples = [];
            _burstStacks[unit] = samples;
        }

        samples.Add(new StackSample(timestamp, stacks));
    }

    private EntropyClaimCast Build(CastState state)
    {
        var (generated, overcapped) = ChronaFor(state);

        return new EntropyClaimCast(
            state.Timestamp,
            state.Unit,
            state.DotStart,
            state.DotStart is null ? null : state.DotEnd,
            DelayFor(state.Timestamp),
            state.TickTimestamps.Count,
            generated,
            overcapped,
            state.BurstStacks);
    }

    private (int Generated, int Overcapped) ChronaFor(CastState state)
    {
        if (state.DotStart is not { } start || state.Unit is not { } unit) return (0, 0);

        var generated = 0;
        var overcapped = 0;

        foreach (var gain in ChronaTracker.GainsBetween(ResourceTypes.Primary, start, state.DotEnd))
        {
            if (gain.AbilityId != Spells.EntropyClaim.FSLID) continue;
            if (gain.Target != unit) continue;

            generated += gain.Usable;
            overcapped += gain.Overcap;
        }

        return (generated, overcapped);
    }

    private int DelayFor(int timestamp)
    {
        foreach (var window in AvailableWindows)
            if (timestamp >= window.Start && timestamp <= window.End)
                return timestamp - window.Start;

        return 0;
    }

    private List<int> BuildDelayEntries()
    {
        var entries = new List<int>();

        foreach (var window in AvailableWindows)
        {
            var cast = _casts.Find(state => state.Timestamp >= window.Start && state.Timestamp <= window.End);

            if (cast is not null) entries.Add(cast.Timestamp - window.Start);
            else if (window.End >= Pull.EndTime) entries.Add(window.Duration);
        }

        return entries;
    }

    private List<AuraWindow> BuildAvailableWindows()
    {
        var start = Pull.StartTime;
        var end = Pull.EndTime;
        var windows = new List<AuraWindow>();
        var open = false;
        var openedAt = start;

        foreach (var change in _availability)
        {
            if (change.Available == open) continue;

            var at = Math.Clamp(change.Timestamp, start, end);
            open = change.Available;

            if (open) openedAt = at;
            else if (at > openedAt) windows.Add(new AuraWindow(openedAt, at));
        }

        if (open && end > openedAt) windows.Add(new AuraWindow(openedAt, end));

        return windows;
    }

    private BurstSummary SummariseBurst()
    {
        var windows = new List<AuraWindow>();
        long unitActiveMs = 0;
        long stackMs = 0;

        foreach (var samples in _burstStacks.Values)
        {
            for (var i = 0; i < samples.Count; i++)
            {
                if (samples[i].Stacks <= 0) continue;

                var start = samples[i].Timestamp;
                var end = i + 1 < samples.Count ? samples[i + 1].Timestamp : Pull.EndTime;
                if (end <= start) continue;

                windows.Add(new AuraWindow(start, end));
                unitActiveMs += end - start;
                stackMs += (long)samples[i].Stacks * (end - start);
            }
        }

        return new BurstSummary(windows, unitActiveMs, stackMs);
    }

    private readonly record struct StackSample(int Timestamp, int Stacks);

    private readonly record struct AvailabilityChange(int Timestamp, bool Available);

    private sealed record BurstSummary(List<AuraWindow> Windows, long UnitActiveMs, long StackMs);

    private sealed class CastState(int timestamp)
    {
        public int Timestamp { get; } = timestamp;
        public UnitKey? Unit { get; set; }
        public int? DotStart { get; set; }
        public int DotEnd { get; set; }
        public List<int> TickTimestamps { get; } = [];
        public int BurstStacks { get; set; }
    }
}

/// <summary>One Entropy's Claim cast and the dot it applied.</summary>
/// <param name="Timestamp">When the cast completed.</param>
/// <param name="Target">The enemy the dot was applied to, or <c>null</c> when no application followed the cast.</param>
/// <param name="DotStart">When the dot was applied, or <c>null</c> when no application followed the cast.</param>
/// <param name="DotEnd">The dot's last observed moment: its expiry, or its last tick when it outlived the pull.</param>
/// <param name="DelayAfterReadyMs">Milliseconds the charge sat available before this cast.</param>
/// <param name="Ticks">Dot damage ticks recorded between application and expiry.</param>
/// <param name="ChronaGenerated">Chrona the ticks of this application generated.</param>
/// <param name="ChronaOvercapped">Chrona the ticks of this application generated above the maximum.</param>
/// <param name="EntropicBurstStacks">Entropic Burst stacks applied across every enemy when this application expired.</param>
public sealed record EntropyClaimCast(
    int Timestamp,
    UnitKey? Target,
    int? DotStart,
    int? DotEnd,
    int DelayAfterReadyMs,
    int Ticks,
    int ChronaGenerated,
    int ChronaOvercapped,
    int EntropicBurstStacks)
{
    /// <summary>Whether the cast applied the dot.</summary>
    public bool DotApplied => DotStart is not null;
}
