using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Measures how well Gunde banked and deployed Blood Feathers. Rend ticks drop feather orbs, picking
/// one up adds stacks to the Owed in Blood self-buff, and the Owed in Blood ability converts every
/// held stack one-for-one into Rend on the target. A conversion is worth the pile standing behind it
/// and the use it is put to, so each one is judged three ways: the size of the bank against the
/// <see cref="MaxStacks"/> cap the game allows, whether a Slaughter followed to cash the Rend it
/// placed, and whether that Slaughter landed inside a Rupture Open Wounds window. Stacks leak in two
/// further ways - the buff falls off with stacks still held, and pickups stop accruing once the pool
/// is pinned at the cap - so decay and time at the cap are recorded as well.
/// </summary>
/// <remarks>
/// <para>
/// The stack count is read from the buff event stream, which is the only feather signal a Gunde log
/// carries: player events have no resource snapshots at all, and Blood Feathers never appear as a
/// snapshotted resource. <see cref="ApplyBuffStackEvent.Stack"/> carries an absolute count. Only the
/// <c>Spells.OwedInBloodSelfBuff</c> effect is tracked; <c>Spells.OwedInBloodSelfBuffFromOrbs</c> has
/// never been observed in live data, so it is deliberately left alone rather than guessed at.
/// </para>
/// <para>
/// Both the base cast and <c>Spells.OwedInBloodAoe</c>, the talented variant that replaces it, count
/// as conversions.
/// </para>
/// <para>
/// Whether a conversion was cashed is settled after the pull closes, because the Slaughter that pays
/// it out comes later in the stream. Each Slaughter records whether an Open Wounds window was live
/// when it went out, so a conversion inherits the Rupture pairing from the cast that actually
/// consumed its Rend rather than from a window that happened to be up when it was pressed.
/// </para>
/// <para>
/// Decay is detected purely from events - a drop in the stack count that no Owed in Blood cast within
/// <see cref="ConversionGraceMs"/> explains - so no buff duration is assumed anywhere. Stacks still
/// held when the pull ends count as neither converted nor decayed, since holding a bank into the next
/// pull is not waste.
/// </para>
/// <para>
/// Feathers and the self-buff survive across pull boundaries while an analyzer does not, so a pull
/// whose first Owed in Blood converts a bank built on the previous pull reads zero stacks without the
/// bank ever having been measured. <see cref="OwedInBloodConversion.BankObserved"/> marks that case so
/// it is never mistaken for an empty press.
/// </para>
/// <para>
/// <see cref="BloodFeatherTracker"/> reconstructs the same stream over the whole fight rather than per
/// pull. The two deliberately keep separate state at their own lifetimes, but share the absolute-stack
/// handling and the <see cref="ConversionGraceMs"/> policy.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class OwedInBloodEconomyAnalyzer : Analyzer
{
    /// <summary>
    /// Blood Feather stacks Gunde can hold, shared with <see cref="BloodFeatherTracker"/>; there is no
    /// generated registry constant for it. The cap is also the target a conversion is scored against,
    /// because a bank is worth exactly the stacks in it and nothing caps it but this.
    /// </summary>
    public const int MaxStacks = BloodFeatherTracker.MaxBloodFeathers;

    /// <summary>
    /// Window either side of an Owed in Blood cast in which a falling stack count is the cast
    /// consuming the bank rather than the bank expiring. Every conversion observed in live data
    /// shares a millisecond with the removal it caused, so this is a safety envelope around log
    /// ordering rather than a measured gap.
    /// </summary>
    public const int ConversionGraceMs = 1_000;

    /// <summary>
    /// How long after a conversion a Slaughter still counts as the cast that cashed it. Five global
    /// cooldowns, which is long enough for the Rupture and Heart Splitter a burst puts between the
    /// two and short enough that the Rend placed has not started decaying away.
    /// </summary>
    public const int CashWindowMs = 5_000;

    /// <summary>
    /// Duration of the Open Wounds debuff Rupture leaves behind, used to bound a window whose removal
    /// never reached the log. Live Season 3 windows run to 18.0 seconds exactly.
    /// </summary>
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

    private Computed? _computed;
    private Computed Result => _computed ??= Compute();

    /// <summary>Every Owed in Blood cast on the pull, in encounter order, with the bank it cashed in.</summary>
    public IReadOnlyList<OwedInBloodConversion> Conversions => Result.Conversions;

    /// <summary>Blood Feather stacks turned into Rend across every conversion on the pull.</summary>
    public int TotalStacksConverted => Result.TotalStacksConverted;

    /// <summary>Mean conversion size, or zero when no conversion was made.</summary>
    public double AverageConversion => Result.AverageConversion;

    /// <summary>Conversions a Slaughter followed to cash the Rend they placed.</summary>
    public int CashedBySlaughter => Result.CashedBySlaughter;

    /// <summary>Conversions whose Slaughter also landed inside a Rupture Open Wounds window.</summary>
    public int PairedWithRupture => Result.PairedWithRupture;

    /// <summary>Conversions made while the Bloodbound Spirit buff was running.</summary>
    public int OverlappedSpirit => Result.OverlappedSpirit;

    /// <summary>Stacks the buff shed without an Owed in Blood cast to account for them.</summary>
    public int DecayedStacks => Result.DecayedStacks;

    /// <summary>
    /// Milliseconds the bank sat at <see cref="MaxStacks"/>, during which further pickups could add
    /// nothing. A span still open when the pull ends is closed at the pull boundary.
    /// </summary>
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
    private void OnSpiritApplied(ApplyBuffEvent buffEvent) => _spiritActive = true;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.BloodboundSpiritSelfBuff))]
    private void OnSpiritRemoved(RemoveBuffEvent buffEvent) => _spiritActive = false;

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.OpenWounds))]
    private void OnOpenWoundsApplied(ApplyDebuffEvent debuffEvent) => OpenWindow(debuffEvent, debuffEvent.Timestamp);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.OpenWounds))]
    private void OnOpenWoundsRefreshed(RefreshDebuffEvent debuffEvent) => OpenWindow(debuffEvent, debuffEvent.Timestamp);

    [On<RemoveDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.OpenWounds))]
    private void OnOpenWoundsRemoved(RemoveDebuffEvent debuffEvent) => _openWoundsUntil.Remove(Key(debuffEvent));

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Slaughter))]
    private void OnSlaughter(CastEvent castEvent) =>
        _slaughters.Add(new SlaughterMark(castEvent.Timestamp, IsOpenWoundsLive(castEvent.Timestamp)));

    [On<CastEvent>(By = Actor.Player, Spells = [
        nameof(Spells.OwedInBlood),
        nameof(Spells.OwedInBloodAoe)])]
    private void OnConverted(CastEvent castEvent)
    {
        var reclaimed = ReclaimPendingDecay(castEvent.Timestamp);
        _lastConversion = castEvent.Timestamp;
        _pending.Add(new PendingConversion(
            castEvent.Timestamp, reclaimed + _stacks, _bankObserved, castEvent.Ability.Id, _spiritActive));
    }

    /// <summary>
    /// Moves the tracked bank to an absolute count. Any decrease that no recent conversion explains is
    /// the buff expiring, so the shortfall is booked as decay. The count is never forced to zero on a
    /// cast: the buff reports its own consumption, and an absolute stack event corrects the running
    /// total if a log ever omits the removal.
    /// </summary>
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

    /// <summary>
    /// Takes back a bank emptying that was booked as decay because it reached the stream ahead of the
    /// Owed in Blood cast that caused it, and hands the stacks to that conversion instead.
    /// </summary>
    private int ReclaimPendingDecay(int timestamp)
    {
        if (_pendingDecay is not { } pending || timestamp - pending.Timestamp is < 0 or > ConversionGraceMs)
            return 0;

        _pendingDecay = null;
        _decayedStacks -= pending.Amount;
        return pending.Amount;
    }

    /// <summary>
    /// Opens a capped span on the first observation at the cap and closes it when the bank drops back
    /// below. The start is latched, so repeated pickups while already pinned do not restart the span.
    /// </summary>
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

    /// <summary>
    /// Pairs each conversion with the Slaughter that cashed it, folds the results into their
    /// aggregates, and closes a capped span left open at the pull boundary. Computed once, on first
    /// read, so every figure the analyzer exposes comes from the same reading of the pull.
    /// </summary>
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
        IReadOnlyList<OwedInBloodConversion> Conversions,
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

    /// <summary>One Owed in Blood cast and the Blood Feather bank it converted into Rend.</summary>
    /// <param name="Timestamp">Encounter time of the cast.</param>
    /// <param name="StacksConverted">Stacks the cast placed as Rend, as measured on this pull.</param>
    /// <param name="BankObserved">
    /// Whether any Owed in Blood self-buff event reached this pull before the cast. When false the
    /// bank was built before the pull started and its size could not be measured, so
    /// <paramref name="StacksConverted"/> reads zero without the cast having been an empty press.
    /// </param>
    /// <param name="AbilityId">
    /// The ability actually cast, either the base Owed in Blood or the talented AoE variant.
    /// </param>
    /// <param name="CashedBySlaughter">
    /// Whether a Slaughter followed within <see cref="CashWindowMs"/> to consume the Rend this
    /// conversion placed.
    /// </param>
    /// <param name="PairedWithRupture">
    /// Whether that Slaughter also landed inside a Rupture Open Wounds window, so the Rend went out
    /// under Rupture's bonus.
    /// </param>
    /// <param name="SpiritActive">Whether the Bloodbound Spirit buff was running when the cast went out.</param>
    public sealed record OwedInBloodConversion(
        int Timestamp,
        int StacksConverted,
        bool BankObserved,
        int AbilityId,
        bool CashedBySlaughter,
        bool PairedWithRupture,
        bool SpiritActive)
    {
        /// <summary>The bank as a share (0-1) of the <see cref="MaxStacks"/> cap it could have reached.</summary>
        public double ShareOfCap => (double)StacksConverted / MaxStacks;
    }
}
