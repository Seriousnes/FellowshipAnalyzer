using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>The pull read surface for Entropy's Claim.</summary>
public interface IEntropyClaimAnalyzer : IAnalyzerSurface;

/// <summary>One Entropy's Claim cast, the application it made, and what its expiry did to Entropic Burst.</summary>
/// <param name="Timestamp">When the cast completed.</param>
/// <param name="Target">The enemy the dot was applied to, or null when no application followed the cast.</param>
/// <param name="DotStart">When the dot was applied, or null when no application followed the cast.</param>
/// <param name="DotEnd">The dot's expiry, or its last tick when it outlived the pull.</param>
/// <param name="DelayAfterReadyMs">Milliseconds a charge was available before this cast.</param>
/// <param name="LeadBeforeExpiryMs">Milliseconds from this cast to the expiry of the latest application still active, or null when none was active.</param>
/// <param name="BurstApplications">Entropic Burst applications at this application's expiry.</param>
/// <param name="BurstRollovers">Entropic Burst stack increments at this application's expiry.</param>
public sealed record EntropyClaimCast(
    int Timestamp,
    UnitKey? Target,
    int? DotStart,
    int? DotEnd,
    int DelayAfterReadyMs,
    int? LeadBeforeExpiryMs,
    int BurstApplications,
    int BurstRollovers)
{
    /// <summary>Whether the cast applied the dot.</summary>
    public bool DotApplied => DotStart is not null;

    /// <summary>Whether this application's expiry incremented an Entropic Burst stack.</summary>
    public bool RolledOver => BurstRollovers > 0;
}

/// <summary>How an Entropic Burst chain ended.</summary>
public enum EntropicBurstChainEnd
{
    /// <summary>The debuff expired.</summary>
    Lapsed,

    /// <summary>The enemy died.</summary>
    Died,

    /// <summary>The pull ended with the debuff active.</summary>
    PullEnded,
}

/// <summary>Entropic Burst on one enemy from its first application to its removal.</summary>
/// <param name="Unit">The enemy.</param>
/// <param name="Start">When the debuff was applied.</param>
/// <param name="End">When the debuff was removed, or the pull's end.</param>
/// <param name="PeakStacks">The highest stack the chain reached.</param>
/// <param name="Rollovers">Stack increments inside the chain.</param>
/// <param name="EndedBy">How the chain ended.</param>
public sealed record EntropicBurstChain(
    UnitKey Unit,
    int Start,
    int End,
    int PeakStacks,
    int Rollovers,
    EntropicBurstChainEnd EndedBy);

/// <summary>One lapse of Entropic Burst, across the enemies it expired on together.</summary>
/// <param name="Timestamp">When the debuff expired.</param>
/// <param name="Units">Enemies it expired on.</param>
/// <param name="PeakStacks">The highest stack among those chains.</param>
/// <param name="ChargeAvailable">Whether an Entropy's Claim charge was available between the previous cast and <see cref="EntropyClaimAnalyzer.RolloverLeadMs"/> before this lapse.</param>
public sealed record EntropicBurstLapse(int Timestamp, int Units, int PeakStacks, bool ChargeAvailable);

/// <summary>
/// Entropy's Claim over one pull: each cast and the application it made, the charges' availability, and
/// the Entropic Burst chains from those applications' expiries.
/// </summary>
/// <remarks>
/// <para>
/// A chain rolls over when an expiry increments its stack. A chain lapses when the debuff is removed
/// from a living enemy. A lapse could have been rolled over when a charge was available between the
/// latest Entropy's Claim completion before the lapse and <see cref="RolloverLeadMs"/> before the lapse:
/// a cast inside that span completes and expires before the debuff does.
/// </para>
/// <para>
/// Only completions are casts.
/// </para>
/// <para>
/// The analyzer runs only with Mass Entropy equipped, because rollover is unreachable on one charge.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
[ActiveWhen<HasMassEntropy>]
[Dependency<SpellUsable>]
[Dependency<AeonaBuild>]
public sealed partial class EntropyClaimAnalyzer : AllTargetUptimeAnalyzer, IEntropyClaimAnalyzer
{
    /// <summary>Milliseconds between an Entropy's Claim completion and the dot application credited to it.</summary>
    public const int CastLinkToleranceMs = 100;

    /// <summary>Milliseconds after a dot expiry within which an Entropic Burst application or stack is credited to that cast.</summary>
    public const int EntropicBurstAttributionMs = 250;

    /// <summary>Milliseconds within which Entropic Burst changes on several enemies count as one event.</summary>
    public const int BurstGroupMs = 100;

    /// <summary>Milliseconds within which an enemy's death and its Entropic Burst removal are the same event.</summary>
    public const int DeathToleranceMs = 100;

    private readonly List<CastState> _casts = [];
    private readonly Dictionary<UnitKey, CastState> _openDots = [];
    private readonly Dictionary<UnitKey, List<StackSample>> _burstStacks = [];
    private readonly Dictionary<UnitKey, ChainState> _openChains = [];
    private readonly List<ChainState> _closedChains = [];
    private readonly List<int> _rolloverInstants = [];
    private readonly List<AvailabilityChange> _availability = [];
    private readonly Dictionary<UnitKey, int> _deaths = [];

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

    /// <summary>Milliseconds of the pull with a charge of Entropy's Claim available.</summary>
    public int AvailableMs => AvailableWindows.Sum(window => window.Duration);

    /// <summary>
    /// Every wait between a charge becoming available and the cast that spent it, with a charge still
    /// available when the pull ended contributing the wait running to the pull end.
    /// </summary>
    public IReadOnlyList<int> DelaysAfterReady => DelayEntries;

    /// <summary>Mean milliseconds of <see cref="DelaysAfterReady"/>.</summary>
    public double AverageDelayAfterReadyMs => DelayEntries.Count == 0 ? 0 : DelayEntries.Average();

    /// <summary>Whether the player took Entropic Burst.</summary>
    public bool EntropicBurstTaken => Owner.SelectedCombatant.HasTalent(AeonaTalents.EntropicBurst);

    /// <summary>
    /// Milliseconds before a lapse by which a charge has to be available for a cast then to expire
    /// before the lapse: the application's duration plus the cast time.
    /// </summary>
    public int RolloverLeadMs => AeonaBuild.EntropyClaimDurationMs + AeonaBuild.EntropyClaimCastTimeMs;

    /// <summary>Every Entropic Burst chain in the pull, in the order they started. Empty without the talent.</summary>
    public IReadOnlyList<EntropicBurstChain> Chains => field ??= BuildChains();

    /// <summary>Every lapse in the pull, in order. Empty without the talent.</summary>
    public IReadOnlyList<EntropicBurstLapse> Lapses => field ??= BuildLapses();

    /// <summary>Lapses with a charge available early enough to have rolled the chain over.</summary>
    public int LapsesWithChargeAvailable => Lapses.Count(lapse => lapse.ChargeAvailable);

    /// <summary>Expiries that incremented Entropic Burst, counting one expiry once across every enemy it stacked on.</summary>
    public int Rollovers => GroupInstants(_rolloverInstants).Count;

    /// <summary>
    /// Rollovers as a share (0-1) of rollovers plus lapses with a charge available, or null when the pull
    /// offered neither.
    /// </summary>
    public double? RolloverShare =>
        Rollovers + LapsesWithChargeAvailable is var total && total > 0 ? (double)Rollovers / total : null;

    /// <summary>The highest Entropic Burst stack any enemy reached in the pull.</summary>
    public int PeakStacks => Chains.Count == 0 ? 0 : Chains.Max(chain => chain.PeakStacks);

    /// <summary>Casts whose expiry incremented an Entropic Burst stack.</summary>
    public int CastsRolledOver => Casts.Count(cast => cast.RolledOver);

    /// <summary>Mean <see cref="EntropyClaimCast.LeadBeforeExpiryMs"/> over the casts that had one, or null when none did.</summary>
    public double? AverageLeadBeforeExpiryMs =>
        Casts.Where(cast => cast.LeadBeforeExpiryMs is not null).Select(cast => (double)cast.LeadBeforeExpiryMs!.Value) is var leads && leads.Any()
            ? leads.Average()
            : null;

    /// <summary>Milliseconds of the pull Entropic Burst was active on at least one enemy, or null without the talent.</summary>
    public long? EntropicBurstActiveMs => EntropicBurstTaken ? AuraWindowLedger.ActiveMs(Burst.Windows) : null;

    /// <summary>Share of the pull (0-1) Entropic Burst was active on at least one enemy, or null without the talent.</summary>
    public double? EntropicBurstUptime => EntropicBurstActiveMs is { } activeMs && Pull.Duration > 0
        ? Math.Min(1d, activeMs / (double)Pull.Duration)
        : null;

    /// <summary>
    /// Milliseconds Entropic Burst was active summed across enemies, counting a moment once per enemy it
    /// was active on, or null without the talent. The denominator of <see cref="EntropicBurstAverageStacks"/>.
    /// </summary>
    public long? EntropicBurstUnitActiveMs => EntropicBurstTaken ? Burst.UnitActiveMs : null;

    /// <summary>
    /// Stack-weighted active time in millisecond-stacks: each stretch of active time multiplied by its
    /// stack count, summed over every enemy. Null without the talent. The numerator of <see cref="EntropicBurstAverageStacks"/>.
    /// </summary>
    public long? EntropicBurstStackMs => EntropicBurstTaken ? Burst.StackMs : null;

    /// <summary>Mean Entropic Burst stacks on each enemy, weighted by the time it was active on them, or null without the talent.</summary>
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
    private void OnCast(CastEvent e)
    {
        if (e.Activation) return;

        _casts.Add(new CastState(e.Timestamp));
    }

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
        var unit = AuraWindowLedger.KeyOf(e);
        RecordBurstStacks(unit, e.Timestamp, 1);

        if (_openChains.Remove(unit, out var open))
        {
            open.End = e.Timestamp;
            open.EndedBy = EntropicBurstChainEnd.Lapsed;
            _closedChains.Add(open);
        }

        _openChains[unit] = new ChainState(unit, e.Timestamp);
        CreditBurst(e.Timestamp, rollover: false);
    }

    [On<ApplyDebuffStackEvent>(By = Actor.Player, Spell = nameof(Spells.EntropicBurst))]
    private void OnBurstStacked(ApplyDebuffStackEvent e)
    {
        var unit = AuraWindowLedger.KeyOf(e);
        RecordBurstStacks(unit, e.Timestamp, e.Stack);

        if (!_openChains.TryGetValue(unit, out var chain))
            _openChains[unit] = chain = new ChainState(unit, e.Timestamp);

        chain.Peak = Math.Max(chain.Peak, e.Stack);
        chain.Rollovers++;
        _rolloverInstants.Add(e.Timestamp);
        CreditBurst(e.Timestamp, rollover: true);
    }

    [On<RemoveDebuffStackEvent>(By = Actor.Player, Spell = nameof(Spells.EntropicBurst))]
    private void OnBurstStackRemoved(RemoveDebuffStackEvent e) =>
        RecordBurstStacks(AuraWindowLedger.KeyOf(e), e.Timestamp, e.Stack);

    [On<RemoveDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.EntropicBurst))]
    private void OnBurstRemoved(RemoveDebuffEvent e)
    {
        var unit = AuraWindowLedger.KeyOf(e);
        RecordBurstStacks(unit, e.Timestamp, 0);

        if (!_openChains.Remove(unit, out var chain)) return;

        chain.End = e.Timestamp;
        chain.EndedBy = DiedBy(unit, e.Timestamp) ? EntropicBurstChainEnd.Died : EntropicBurstChainEnd.Lapsed;
        _closedChains.Add(chain);
    }

    [On<DeathEvent>]
    private void OnDeath(DeathEvent e) => _deaths[new UnitKey(e.TargetId, e.TargetInstance ?? 0)] = e.Timestamp;

    private bool DiedBy(UnitKey unit, int timestamp) =>
        _deaths.TryGetValue(unit, out var died) && died <= timestamp + DeathToleranceMs;

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

    private void CreditBurst(int timestamp, bool rollover)
    {
        if (_lastExpired is not { } state) return;
        if (timestamp - _lastExpiredAt > EntropicBurstAttributionMs) return;

        if (rollover) state.BurstRollovers++;
        else state.BurstApplications++;
    }

    private void RecordBurstStacks(UnitKey unit, int timestamp, int stacks)
    {
        if (!_burstStacks.TryGetValue(unit, out var samples))
        {
            samples = [];
            _burstStacks[unit] = samples;
        }

        samples.Add(new StackSample(timestamp, stacks));
    }

    private EntropyClaimCast Build(CastState state) => new(
        state.Timestamp,
        state.Unit,
        state.DotStart,
        state.DotStart is null ? null : state.DotEnd,
        DelayFor(state.Timestamp),
        LeadFor(state),
        state.BurstApplications,
        state.BurstRollovers);

    private int? LeadFor(CastState cast)
    {
        int? lead = null;
        foreach (var other in _casts)
        {
            if (ReferenceEquals(other, cast) || other.DotStart is not { } start) continue;
            if (start >= cast.Timestamp || other.DotEnd <= cast.Timestamp) continue;

            var remaining = other.DotEnd - cast.Timestamp;
            if (remaining > (lead ?? int.MinValue)) lead = remaining;
        }

        return lead;
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

    private bool ChargeAvailableBetween(int start, int end)
    {
        if (end < start) return false;

        foreach (var window in AvailableWindows)
        {
            if (window.Start <= end && window.End > start) return true;
        }

        return false;
    }

    private List<EntropicBurstChain> BuildChains()
    {
        if (!EntropicBurstTaken) return [];

        var chains = _closedChains
            .Select(chain => new EntropicBurstChain(chain.Unit, chain.Start, chain.End, chain.Peak, chain.Rollovers, chain.EndedBy))
            .ToList();

        foreach (var chain in _openChains.Values)
            chains.Add(new EntropicBurstChain(chain.Unit, chain.Start, Pull.EndTime, chain.Peak, chain.Rollovers, EntropicBurstChainEnd.PullEnded));

        chains.Sort((left, right) => left.Start.CompareTo(right.Start));
        return chains;
    }

    private List<EntropicBurstLapse> BuildLapses()
    {
        var lapses = new List<EntropicBurstLapse>();
        var lapsed = Chains.Where(chain => chain.EndedBy == EntropicBurstChainEnd.Lapsed).OrderBy(chain => chain.End).ToList();

        var index = 0;
        while (index < lapsed.Count)
        {
            var first = lapsed[index];
            var group = new List<EntropicBurstChain> { first };
            index++;

            while (index < lapsed.Count && lapsed[index].End - first.End <= BurstGroupMs)
            {
                group.Add(lapsed[index]);
                index++;
            }

            var previousCast = _casts
                .Where(cast => cast.Timestamp <= first.End)
                .Select(cast => cast.Timestamp)
                .DefaultIfEmpty(Pull.StartTime)
                .Max();

            lapses.Add(new EntropicBurstLapse(
                first.End,
                group.Count,
                group.Max(chain => chain.PeakStacks),
                ChargeAvailableBetween(previousCast, first.End - RolloverLeadMs)));
        }

        return lapses;
    }

    private static List<int> GroupInstants(List<int> instants)
    {
        var grouped = new List<int>();
        foreach (var instant in instants.Order())
        {
            if (grouped.Count > 0 && instant - grouped[^1] <= BurstGroupMs) continue;
            grouped.Add(instant);
        }

        return grouped;
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
        public int BurstApplications { get; set; }
        public int BurstRollovers { get; set; }
    }

    private sealed class ChainState(UnitKey unit, int start)
    {
        public UnitKey Unit { get; } = unit;
        public int Start { get; } = start;
        public int End { get; set; }
        public int Peak { get; set; } = 1;
        public int Rollovers { get; set; }
        public EntropicBurstChainEnd EndedBy { get; set; }
    }
}
