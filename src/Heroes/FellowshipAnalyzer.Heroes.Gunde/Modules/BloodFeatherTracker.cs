using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Gunde.Statistics;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Reconstructs Gunde's Blood Feather bank across the whole fight. Feathers drop from bleeding
/// enemies, are picked up off the ground, and are converted in full by Owed in Blood, so the pool
/// behaves as an accumulate-then-dump resource rather than a per-cast cost.
/// </summary>
/// <remarks>
/// <para>
/// Gunde logs carry no resource snapshots for the player: <c>sourceResources.resources</c> is empty
/// on every player-sourced event, and Blood Feathers never appear as a snapshotted resource. The
/// bank is therefore reconstructed purely from the Owed in Blood self-buff stream, whose
/// <see cref="ApplyBuffStackEvent.Stack"/> carries an absolute count.
/// </para>
/// <para>
/// <see cref="Generated"/> counts observed pickups only. A feather that lands on a full pool
/// produces no stack event at all, so overcap can never be expressed as a feather count; it is
/// measured instead as <see cref="CappedMs"/>, the time the bank spent pinned at
/// <see cref="MaxBloodFeathers"/> with nothing left to gain.
/// </para>
/// <para>
/// <see cref="OwedInBloodEconomyAnalyzer"/> reads the same stream per pull rather than per fight.
/// The two deliberately keep separate state at their own lifetimes, but share the absolute-stack
/// handling and the <see cref="ConversionGraceMs"/> policy that separates a conversion from decay.
/// </para>
/// </remarks>
public sealed partial class BloodFeatherTracker : EventSubscriber
{
    /// <summary>Maximum Blood Feathers Gunde can hold.</summary>
    public const int MaxBloodFeathers = 150;

    /// <summary>
    /// Window either side of an Owed in Blood cast in which a bank emptying is the cast consuming it
    /// rather than the buff expiring. Mirrors
    /// <see cref="OwedInBloodEconomyAnalyzer.ConversionGraceMs"/>.
    /// </summary>
    public const int ConversionGraceMs = OwedInBloodEconomyAnalyzer.ConversionGraceMs;

    private int _bank;
    private int? _cappedSince;
    private int? _lastConversion;
    private PendingDecay? _pendingDecay;

    /// <summary>Blood Feathers observed arriving in the bank over the fight.</summary>
    public int Generated { get; private set; }

    /// <summary>Blood Feathers Owed in Blood converted into Rend over the fight.</summary>
    public int Spent { get; private set; }

    /// <summary>Blood Feathers the buff shed with no Owed in Blood cast to account for them.</summary>
    public int Decayed { get; private set; }

    /// <summary>Blood Feathers still banked at the last observation.</summary>
    public int Current => _bank;

    /// <summary>
    /// Milliseconds the bank sat at <see cref="MaxBloodFeathers"/>, during which further pickups
    /// could add nothing. A span still open at the end of the fight is closed at the fight boundary.
    /// </summary>
    public int CappedMs { get; private set; }

    /// <summary>
    /// The Blood Feather card, contributed only when the run showed feather activity. A Gunde log
    /// without the self-buff would otherwise render a card of zeroes.
    /// </summary>
    public override Type? StatisticsComponentType =>
        Generated > 0 || Spent > 0 ? typeof(BloodFeatherStatistics) : null;

    public override StatisticCategory StatisticCategory => StatisticCategory.Resources;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.OwedInBloodSelfBuff))]
    private void OnBuffApplied(ApplyBuffEvent buffEvent) => SetBank(buffEvent.Timestamp, 1);

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.OwedInBloodSelfBuff))]
    private void OnStackApplied(ApplyBuffStackEvent buffEvent) => SetBank(buffEvent.Timestamp, buffEvent.Stack);

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.OwedInBloodSelfBuff))]
    private void OnStackRemoved(RemoveBuffStackEvent buffEvent) => SetBank(buffEvent.Timestamp, buffEvent.Stack);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.OwedInBloodSelfBuff))]
    private void OnBuffRemoved(RemoveBuffEvent buffEvent) => SetBank(buffEvent.Timestamp, 0);

    [On<CastEvent>(By = Actor.Player, Spells = [
        nameof(Spells.OwedInBlood),
        nameof(Spells.OwedInBloodAoe)])]
    private void OnConverted(CastEvent castEvent)
    {
        var reclaimed = ReclaimPendingDecay(castEvent.Timestamp);
        _lastConversion = castEvent.Timestamp;
        Spent += reclaimed + _bank;
    }

    [On<FightEndEvent>]
    private void OnFightEnd(FightEndEvent fightEnd) => CloseCapWindow(fightEnd.Timestamp);

    /// <summary>
    /// Moves the reconstructed bank to an absolute count. A rise is feathers picked up; a fall that
    /// no recent conversion explains is the buff expiring, so the shortfall is booked as decay. The
    /// bank is never forced to zero on a cast: the buff reports its own consumption, and an absolute
    /// stack event corrects the running total if a log ever omits the removal.
    /// </summary>
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

    /// <summary>
    /// Takes back a bank emptying that was booked as decay because it reached the stream ahead of the
    /// Owed in Blood cast that caused it. Real logs place the cast first, and every observed pair
    /// shares a millisecond, so the grace window is a safety envelope around log ordering rather than
    /// a measured gap.
    /// </summary>
    private int ReclaimPendingDecay(int timestamp)
    {
        if (_pendingDecay is not { } pending || timestamp - pending.Timestamp is < 0 or > ConversionGraceMs)
            return 0;

        _pendingDecay = null;
        Decayed -= pending.Amount;
        return pending.Amount;
    }

    /// <summary>
    /// Opens a capped span on the first observation at the cap and closes it when the bank drops back
    /// below. The start is latched, so repeated pickups while already pinned do not restart the span.
    /// </summary>
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
