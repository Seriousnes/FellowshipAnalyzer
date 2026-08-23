using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class OwedInBloodEconomyAnalyzer : Analyzer
{
    public const int MaxStacks = BloodFeatherTracker.MaxBloodFeathers;

    public const int ConversionGraceMs = 1_000;

    public const int CashWindowMs = 5_000;

    public const int OpenWoundsDurationMs = 18_000;

    private readonly List<PendingConversion> _pending = [];
    private readonly List<SlaughterMark> _slaughters = [];
    private readonly Dictionary<OpenWoundsTarget, int> _openWoundsUntil = [];

    private int _stacks;
    private int _decayedStacks;
    private int _cappedMs;
    private int? _cappedSince;
    private int? _lastConversion;
    private bool _bankObserved;
    private bool _spiritActive;
    private PendingDecay? _pendingDecay;

    private Computed Result => field ??= Compute();

    public List<OwedInBloodConversion> Conversions => Result.Conversions;

    public int TotalStacksConverted => Result.TotalStacksConverted;

    public double AverageConversion => Result.AverageConversion;

    public int CashedBySlaughter => Result.CashedBySlaughter;

    public int PairedWithRupture => Result.PairedWithRupture;

    public int OverlappedSpirit => Result.OverlappedSpirit;

    public int DecayedStacks => Result.DecayedStacks;

    public int CappedMs => Result.CappedMs;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.OwedInBloodSelfBuff))]
    private void OnBuffApplied(ApplyBuffEvent buffEvent) => SetStacks(buffEvent.Timestamp, 1);

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.OwedInBloodSelfBuff))]
    private void OnStackApplied(ApplyBuffStackEvent buffEvent) => SetStacks(buffEvent.Timestamp, buffEvent.Stack);

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.OwedInBloodSelfBuff))]
    private void OnStackRemoved(RemoveBuffStackEvent buffEvent) => SetStacks(buffEvent.Timestamp, buffEvent.Stack);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.OwedInBloodSelfBuff))]
    private void OnBuffRemoved(RemoveBuffEvent buffEvent) => SetStacks(buffEvent.Timestamp, 0);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.BloodboundSpiritSelfBuff))]
    private void OnSpiritApplied() => _spiritActive = true;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.BloodboundSpiritSelfBuff))]
    private void OnSpiritRemoved() => _spiritActive = false;

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.OpenWounds))]
    private void OnOpenWoundsApplied(ApplyDebuffEvent debuffEvent) => OpenWindow(debuffEvent, debuffEvent.Timestamp);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.OpenWounds))]
    private void OnOpenWoundsRefreshed(RefreshDebuffEvent debuffEvent) => OpenWindow(debuffEvent, debuffEvent.Timestamp);

    [On<RemoveDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.OpenWounds))]
    private void OnOpenWoundsRemoved(RemoveDebuffEvent debuffEvent) => _openWoundsUntil.Remove(Key(debuffEvent));

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Slaughter))]
    private void OnSlaughter(CastEvent castEvent) =>
        _slaughters.Add(new SlaughterMark(castEvent.Timestamp, IsOpenWoundsLive(castEvent.Timestamp)));

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.OwedInBlood))]
    private void OnConverted(CastEvent castEvent)
    {
        var reclaimed = ReclaimPendingDecay(castEvent.Timestamp);
        _lastConversion = castEvent.Timestamp;
        _pending.Add(new PendingConversion(
            castEvent.Timestamp, reclaimed + _stacks, _bankObserved, castEvent.Ability.Id, _spiritActive));
    }

    private void SetStacks(int timestamp, int stacks)
    {
        _bankObserved = true;
        _pendingDecay = null;

        var lost = _stacks - stacks;
        if (lost > 0 && !IsConverting(timestamp))
        {
            _decayedStacks += lost;
            if (stacks == 0)
                _pendingDecay = new PendingDecay(lost, timestamp);
        }

        UpdateCapWindow(timestamp, stacks);
        _stacks = stacks;
    }

    private bool IsConverting(int timestamp) =>
        _lastConversion is { } cast && timestamp - cast is >= 0 and <= ConversionGraceMs;

    private int ReclaimPendingDecay(int timestamp)
    {
        if (_pendingDecay is not { } pending || timestamp - pending.Timestamp is < 0 or > ConversionGraceMs)
            return 0;

        _pendingDecay = null;
        _decayedStacks -= pending.Amount;
        return pending.Amount;
    }

    private void UpdateCapWindow(int timestamp, int stacks)
    {
        if (stacks >= MaxStacks)
        {
            _cappedSince ??= timestamp;
            return;
        }

        if (_cappedSince is not { } since) return;

        _cappedMs += Math.Max(0, timestamp - since);
        _cappedSince = null;
    }

    private void OpenWindow(IHasTargetWithInstanceEvent debuffEvent, int timestamp) =>
        _openWoundsUntil[Key(debuffEvent)] = timestamp + OpenWoundsDurationMs;

    private bool IsOpenWoundsLive(int timestamp) => _openWoundsUntil.Values.Any(until => until >= timestamp);

    private Computed Compute()
    {
        var cappedMs = _cappedMs;
        if (_cappedSince is { } since)
            cappedMs += Math.Max(0, Pull.EndTime - since);

        var conversions = new List<OwedInBloodConversion>(_pending.Count);
        var total = 0;
        foreach (var pending in _pending)
        {
            var cashed = _slaughters.FirstOrDefault(slaughter =>
                slaughter.Timestamp > pending.Timestamp &&
                slaughter.Timestamp - pending.Timestamp <= CashWindowMs);

            total += pending.StacksConverted;
            conversions.Add(new OwedInBloodConversion(
                pending.Timestamp,
                pending.StacksConverted,
                pending.BankObserved,
                pending.AbilityId,
                cashed is not null,
                cashed?.OpenWoundsActive ?? false,
                pending.SpiritActive));
        }

        var average = conversions.Count > 0 ? (double)total / conversions.Count : 0d;
        return new Computed(
            conversions,
            total,
            average,
            conversions.Count(conversion => conversion.CashedBySlaughter),
            conversions.Count(conversion => conversion.PairedWithRupture),
            conversions.Count(conversion => conversion.SpiritActive),
            cappedMs,
            _decayedStacks);
    }

    private static OpenWoundsTarget Key(IHasTargetWithInstanceEvent debuffEvent) =>
        new(debuffEvent.TargetId, debuffEvent.TargetInstance ?? 0);

    private sealed record Computed(
        List<OwedInBloodConversion> Conversions,
        int TotalStacksConverted,
        double AverageConversion,
        int CashedBySlaughter,
        int PairedWithRupture,
        int OverlappedSpirit,
        int CappedMs,
        int DecayedStacks);

    private readonly record struct PendingDecay(int Amount, int Timestamp);

    private readonly record struct PendingConversion(
        int Timestamp, int StacksConverted, bool BankObserved, int AbilityId, bool SpiritActive);

    private sealed record SlaughterMark(int Timestamp, bool OpenWoundsActive);

    private readonly record struct OpenWoundsTarget(int TargetId, int TargetInstance);

    public sealed record OwedInBloodConversion(
        int Timestamp,
        int StacksConverted,
        bool BankObserved,
        int AbilityId,
        bool CashedBySlaughter,
        bool PairedWithRupture,
        bool SpiritActive)
    {
        public double ShareOfCap => (double)StacksConverted / MaxStacks;
    }
}
