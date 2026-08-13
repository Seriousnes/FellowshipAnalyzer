using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

public sealed partial class BloodFeatherTracker : Analyzer
{
    public const int MaxBloodFeathers = 150;

    public const int ConversionGraceMs = OwedInBloodEconomyAnalyzer.ConversionGraceMs;

    private int _bank;
    private int? _cappedSince;
    private int? _lastConversion;
    private PendingDecay? _pendingDecay;

    public int Generated { get; private set; }

    public int Spent { get; private set; }

    public int Decayed { get; private set; }

    public int Current => _bank;

    public int CappedMs { get; private set; }

    public override StatisticCategory StatisticCategory => StatisticCategory.Resources;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.OwedInBloodSelfBuff))]
    private void OnBuffApplied(ApplyBuffEvent buffEvent) => SetBank(buffEvent.Timestamp, 1);

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.OwedInBloodSelfBuff))]
    private void OnStackApplied(ApplyBuffStackEvent buffEvent) => SetBank(buffEvent.Timestamp, buffEvent.Stack);

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.OwedInBloodSelfBuff))]
    private void OnStackRemoved(RemoveBuffStackEvent buffEvent) => SetBank(buffEvent.Timestamp, buffEvent.Stack);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.OwedInBloodSelfBuff))]
    private void OnBuffRemoved(RemoveBuffEvent buffEvent) => SetBank(buffEvent.Timestamp, 0);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.OwedInBlood))]
    private void OnConverted(CastEvent castEvent)
    {
        var reclaimed = ReclaimPendingDecay(castEvent.Timestamp);
        _lastConversion = castEvent.Timestamp;
        Spent += reclaimed + _bank;
    }

    [On<DungeonEndEvent>]
    private void OnDungeonEnd(DungeonEndEvent dungeonEnd) => CloseCapWindow(dungeonEnd.Timestamp);

    private void SetBank(int timestamp, int bank)
    {
        var delta = bank - _bank;
        _pendingDecay = null;

        if (delta > 0)
        {
            Generated += delta;
        }
        else if (delta < 0 && !IsConverting(timestamp))
        {
            Decayed -= delta;
            if (bank == 0)
                _pendingDecay = new PendingDecay(-delta, timestamp);
        }

        UpdateCapWindow(timestamp, bank);
        _bank = bank;
    }

    private bool IsConverting(int timestamp) =>
        _lastConversion is { } cast && timestamp - cast is >= 0 and <= ConversionGraceMs;

    private int ReclaimPendingDecay(int timestamp)
    {
        if (_pendingDecay is not { } pending || timestamp - pending.Timestamp is < 0 or > ConversionGraceMs)
            return 0;

        _pendingDecay = null;
        Decayed -= pending.Amount;
        return pending.Amount;
    }

    private void UpdateCapWindow(int timestamp, int bank)
    {
        if (bank >= MaxBloodFeathers)
        {
            _cappedSince ??= timestamp;
            return;
        }

        CloseCapWindow(timestamp);
    }

    private void CloseCapWindow(int timestamp)
    {
        if (_cappedSince is not { } since) return;

        CappedMs += Math.Max(0, timestamp - since);
        _cappedSince = null;
    }

    private readonly record struct PendingDecay(int Amount, int Timestamp);
}
