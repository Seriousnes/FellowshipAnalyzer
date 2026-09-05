using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;
using FSLID = FellowshipAnalyzer.Core.Common.Spells.FSLID;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// The mana a single Amend Fate or Restore Continuity cast returned, beside the Stagger it cleared.
/// </summary>
/// <param name="Timestamp">When the cast completed.</param>
/// <param name="Ability">The cleanse cast, either <c>Spells.AmendFate</c> or <c>Spells.RestoreContinuity</c>.</param>
/// <param name="StaggerCleared">The Stagger the cast cleared across its heal targets, in hit points, or <see langword="null"/> when no target carried readings either side of the cast.</param>
/// <param name="ManaRestored">The mana that arrived inside the attribution window, or <see langword="null"/> when no mana change was observed there at all. A window that also carries a mana spend reports the gains alone, so this is a lower bound.</param>
/// <param name="HasInterveningEvent">Whether something other than this cast moved a target's Stagger inside the measured bracket, which makes <paramref name="StaggerCleared"/> an upper bound.</param>
public sealed record CleanseReturn(
    int Timestamp,
    FSLID Ability,
    int? StaggerCleared,
    int? ManaRestored,
    bool HasInterveningEvent);

/// <summary>
/// One Chrona Tap window on the player, from its first stack to the removal that paid it out.
/// </summary>
/// <param name="Start">When the first stack was applied.</param>
/// <param name="End">When the window ended, or the pull's end time while it was still active.</param>
/// <param name="Stacks">The stack count the window held when it ended. Chrona Tap does not refresh its duration when a stack arrives, so this is the count the mana return is paid on.</param>
/// <param name="Expired">Whether the window's removal was observed. A window still active at the pull's end has returned no mana yet.</param>
public sealed record ChronaTapWindow(int Start, int End, int Stacks, bool Expired);

/// <summary>
/// Aeona's Chrona and mana economy for one pull: what arrived, what was spent, how long each pool
/// sat at its maximum, and the return the build's economy talents produced.
/// </summary>
/// <remarks>
/// <para>
/// Generation and spending come from <see cref="ChronaTracker"/>, which reconstructs both from the
/// resource snapshots events carry because Fellowship logs of Aeona emit no resource-change event and
/// no declared cost. This analyzer adds only what that tracker does not own: the Synchronicity
/// estimate, the Chrona Tap windows, and the mana a cleanse cast returned.
/// </para>
/// <para>
/// Waste is reported as time at the maximum rather than as points lost. A reconstructed gain is the
/// difference between two snapshots and the later snapshot is already capped, so the points the game
/// discarded never reach the log. Time at the maximum is what the log can prove: a pool only leaves
/// its maximum through a change the tracker records, so the span from the gain that reached the
/// maximum to the next recorded change is the time spent there.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<ChronaTracker>]
[Dependency<StaggerTracker>]
public sealed partial class ChronaEconomyAnalyzer : Analyzer
{
    /// <summary>
    /// The share of maximum Chrona below which Synchronicity increases generation. Codex
    /// <c>talent 537</c>, "When you are below 50% Chrona you generate 25% more Chrona".
    /// </summary>
    public const double SynchronicityThresholdShare = 0.5;

    /// <summary>
    /// The generation increase Synchronicity applies below <see cref="SynchronicityThresholdShare"/>.
    /// Codex <c>talent 537</c> and its <c>effect 2733</c>, "Synchronicity: Catch Up - 25% increased
    /// Resource Generation". The talent's other half, 15% increased damage above the same threshold,
    /// is a damage figure and is not measured here.
    /// </summary>
    public const double SynchronicityGenerationIncrease = 0.25;

    /// <summary>
    /// The share of maximum mana Chrona Tap returns for each stack it held when it expired. Codex
    /// <c>talent 539</c>, "Chrona Tap replenishes 1.3% of your Maximum Mana per stack when it expires".
    /// </summary>
    public const double ChronaTapManaSharePerStack = 0.013;

    /// <summary>
    /// The stack count Chrona Tap holds at most. Codex <c>talent 539</c>, "up to 10 stacks".
    /// </summary>
    public const int ChronaTapMaximumStacks = 10;

    /// <summary>
    /// How long after a cleanse cast a mana change or a Stagger reading is attributed to it. Both
    /// cleanses are on the standard global cooldown and Fellowship lands their heals in the same
    /// millisecond as the cast, so a second is wide enough for the next snapshot to arrive and narrow
    /// enough to exclude the following cast.
    /// </summary>
    public const int CleanseReturnWindowMs = 1_000;

    private readonly List<ChronaTapWindow> _chronaTapWindows = [];

    private int? _chronaTapStart;
    private int _chronaTapStacks;

    private (int TimeMs, int Gains)? _chronaOccupancy;
    private (int TimeMs, int Gains)? _manaOccupancy;
    private int? _generatedBelowThreshold;
    private int? _observedChronaSpends;

    /// <summary>Chrona that arrived during the pull, excluding anything lost at the maximum.</summary>
    public int ChronaGenerated => ChronaTracker.GeneratedBetween(ResourceTypes.Primary, Pull.StartTime, Pull.EndTime);

    /// <summary>
    /// Chrona spent during the pull. Reconstructed from falling snapshots, so a spend is net of any
    /// generation arriving in the same interval and is a lower bound.
    /// </summary>
    public int ChronaSpent => ChronaTracker.SpentBetween(ResourceTypes.Primary, Pull.StartTime, Pull.EndTime);

    /// <summary>Mana that arrived during the pull, excluding anything lost at the maximum.</summary>
    public int ManaGenerated => ChronaTracker.GeneratedBetween(ResourceTypes.Mana, Pull.StartTime, Pull.EndTime);

    /// <summary>Mana spent during the pull, reconstructed from falling snapshots.</summary>
    public int ManaSpent => ChronaTracker.SpentBetween(ResourceTypes.Mana, Pull.StartTime, Pull.EndTime);

    /// <summary>Maximum Chrona, as the highest maximum any snapshot reported.</summary>
    public int ChronaMaximum => ChronaTracker.MaxOf(ResourceTypes.Primary);

    /// <summary>Maximum mana, as the highest maximum any snapshot reported, or <c>0</c> when none did.</summary>
    public int ManaMaximum => ChronaTracker.MaxOf(ResourceTypes.Mana);

    /// <summary>
    /// Time at maximum Chrona, measured from each gain that reached the maximum to the next change
    /// that moved the pool, or to the pull's end.
    /// </summary>
    public int ChronaTimeAtMaximumMs => ChronaOccupancy.TimeMs;

    /// <summary>Chrona gains that landed the pool at its maximum.</summary>
    public int ChronaGainsAtMaximum => ChronaOccupancy.Gains;

    /// <summary>Time at maximum Chrona as a share of the pull.</summary>
    public double ChronaTimeAtMaximumShare =>
        Pull.Duration <= 0 ? 0 : (double)ChronaTimeAtMaximumMs / Pull.Duration;

    /// <summary>Time at maximum mana, measured the same way as <see cref="ChronaTimeAtMaximumMs"/>.</summary>
    public int ManaTimeAtMaximumMs => ManaOccupancy.TimeMs;

    /// <summary>Mana gains that landed the pool at its maximum.</summary>
    public int ManaGainsAtMaximum => ManaOccupancy.Gains;

    /// <summary>Time at maximum mana as a share of the pull.</summary>
    public double ManaTimeAtMaximumShare =>
        Pull.Duration <= 0 ? 0 : (double)ManaTimeAtMaximumMs / Pull.Duration;

    /// <summary>Whether the build takes Synchronicity.</summary>
    public bool SynchronicityTalented => Owner.SelectedCombatant.HasTalent(AeonaTalents.Synchronicity);

    /// <summary>The Chrona amount below which Synchronicity increases generation.</summary>
    public int SynchronicityThreshold => (int)(ChronaMaximum * SynchronicityThresholdShare);

    /// <summary>
    /// Chrona that arrived while the pool held less than <see cref="SynchronicityThreshold"/>, read
    /// from the amount each gain landed on less the gain itself. Counted whether or not the build
    /// takes Synchronicity.
    /// </summary>
    public int ChronaGeneratedBelowSynchronicityThreshold =>
        _generatedBelowThreshold ??= MeasureGenerationBelowThreshold();

    /// <summary>
    /// The share of <see cref="ChronaGeneratedBelowSynchronicityThreshold"/> Synchronicity produced, or
    /// <see langword="null"/> when the build does not take it. Fellowship logs the amount that reached
    /// the pool, which already carries the increase, so the estimate is the increase's share of that
    /// amount rather than a further percentage of it.
    /// </summary>
    public double? EstimatedSynchronicityChrona => SynchronicityTalented
        ? ChronaGeneratedBelowSynchronicityThreshold
            * (SynchronicityGenerationIncrease / (1 + SynchronicityGenerationIncrease))
        : null;

    /// <summary>
    /// Every Amend Fate and Restore Continuity cast in the pull, with the Stagger it cleared and the
    /// mana that arrived inside <see cref="CleanseReturnWindowMs"/> of it.
    /// </summary>
    public IReadOnlyList<CleanseReturn> CleanseReturns => field ??= MeasureCleanseReturns();

    /// <summary>Amend Fate and Restore Continuity casts made during the pull.</summary>
    public int CleanseCasts => CleanseReturns.Count;

    /// <summary>Stagger cleared across every cleanse cast that carried readings either side of it, in hit points.</summary>
    public int StaggerClearedByCleansing =>
        CleanseReturns.Sum(cleanse => cleanse.StaggerCleared ?? 0);

    /// <summary>Cleanse casts with no Stagger reading either side of them, whose cleared amount is unknown.</summary>
    public int CleansesWithoutStaggerReading =>
        CleanseReturns.Count(cleanse => cleanse.StaggerCleared is null);

    /// <summary>
    /// Mana attributed to cleansing, as the gains observed inside <see cref="CleanseReturnWindowMs"/>
    /// of a cleanse cast. Attribution is by proximity, so a Chrona Tap payout landing in the same
    /// window is counted here as well.
    /// </summary>
    public int EstimatedManaFromCleansing =>
        CleanseReturns.Sum(cleanse => cleanse.ManaRestored ?? 0);

    /// <summary>Cleanse casts with no mana change observed inside their window, whose return is unknown.</summary>
    public int CleansesWithoutManaReading =>
        CleanseReturns.Count(cleanse => cleanse.ManaRestored is null);

    /// <summary>Whether the build takes Chrona Tap.</summary>
    public bool ChronaTapTalented => Owner.SelectedCombatant.HasTalent(AeonaTalents.ChronaTap);

    /// <summary>
    /// Every Chrona Tap window in the pull, in the order they opened. A window still active at the
    /// pull's end is included, ending at <see cref="PullStartEvent.EndTime"/> and marked unexpired. A
    /// window whose application fell before the pull opened starts at the first stack or refresh
    /// observed inside it, so its recorded stack count is a lower bound.
    /// </summary>
    public IReadOnlyList<ChronaTapWindow> ChronaTapWindows => _chronaTapStart is { } start
        ? [.. _chronaTapWindows, new ChronaTapWindow(start, Pull.EndTime, _chronaTapStacks, Expired: false)]
        : _chronaTapWindows;

    /// <summary>
    /// Chrona Tap stacks gained across the pull, read as the stack count each window ended on. Chrona
    /// Tap sheds no stack inside a window, so the ending count is the count that window gained. A window
    /// whose first stack landed before the pull opened is counted from the first event inside it.
    /// </summary>
    public int ChronaTapStacksGained => ChronaTapWindows.Sum(window => window.Stacks);

    /// <summary>
    /// Chrona spends the snapshots revealed. A spend masked by generation arriving in the same interval
    /// leaves no falling snapshot, so this undercounts the casts that drew on Chrona.
    /// </summary>
    public int ObservedChronaSpends => _observedChronaSpends ??= CountSpends(ResourceTypes.Primary);

    /// <summary>
    /// Chrona Tap stacks gained for each observed Chrona spend, or <see langword="null"/> when the build
    /// does not take Chrona Tap or no spend was observed. The talent grants one stack per Chrona spend,
    /// so a build converting every spend reads 1 against a complete denominator.
    /// </summary>
    public double? ChronaTapStacksPerObservedSpend => ChronaTapTalented && ObservedChronaSpends > 0
        ? (double)ChronaTapStacksGained / ObservedChronaSpends
        : null;

    /// <summary>
    /// The mana Chrona Tap returned, as <see cref="ChronaTapManaSharePerStack"/> of maximum mana for
    /// each stack an expired window held. Windows still active at the pull's end have returned nothing
    /// and are excluded. <see langword="null"/> when the build does not take Chrona Tap or no snapshot
    /// reported maximum mana.
    /// </summary>
    public double? EstimatedChronaTapMana => ChronaTapTalented && ManaMaximum > 0
        ? ChronaTapWindows.Where(window => window.Expired).Sum(window => window.Stacks)
            * ChronaTapManaSharePerStack * ManaMaximum
        : null;

    private (int TimeMs, int Gains) ChronaOccupancy =>
        _chronaOccupancy ??= MeasureOccupancy(ResourceTypes.Primary);

    private (int TimeMs, int Gains) ManaOccupancy =>
        _manaOccupancy ??= MeasureOccupancy(ResourceTypes.Mana);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ChronaTap))]
    private void OnChronaTapApply(ApplyBuffEvent e)
    {
        CloseChronaTapWindow(e.Timestamp, expired: true);
        _chronaTapStart = e.Timestamp;
        _chronaTapStacks = 1;
    }

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ChronaTap))]
    private void OnChronaTapStackApply(ApplyBuffStackEvent e)
    {
        _chronaTapStart ??= e.Timestamp;
        _chronaTapStacks = Math.Max(_chronaTapStacks, e.Stack);
    }

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ChronaTap))]
    private void OnChronaTapRefresh(RefreshBuffEvent e)
    {
        if (_chronaTapStart is not null) return;

        _chronaTapStart = e.Timestamp;
        _chronaTapStacks = 1;
    }

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ChronaTap))]
    private void OnChronaTapStackRemove(RemoveBuffStackEvent e)
    {
        if (_chronaTapStart is null) return;

        _chronaTapStacks = e.Stack;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ChronaTap))]
    private void OnChronaTapRemove(RemoveBuffEvent e) => CloseChronaTapWindow(e.Timestamp, expired: true);

    private void CloseChronaTapWindow(int timestamp, bool expired)
    {
        if (_chronaTapStart is not { } start) return;

        _chronaTapWindows.Add(new ChronaTapWindow(start, timestamp, _chronaTapStacks, expired));
        _chronaTapStart = null;
        _chronaTapStacks = 0;
    }

    private int MeasureGenerationBelowThreshold()
    {
        var threshold = SynchronicityThreshold;
        var total = 0;

        foreach (var change in ChronaTracker.EventsBetween(ResourceTypes.Primary, Pull.StartTime, Pull.EndTime))
        {
            if (change.Kind != ResourceEventKind.Gain) continue;
            if (change.CurrentAfter - change.Amount >= threshold) continue;

            total += change.Amount;
        }

        return total;
    }

    private int CountSpends(ResourceTypes type)
    {
        var spends = 0;

        foreach (var change in ChronaTracker.EventsBetween(type, Pull.StartTime, Pull.EndTime))
        {
            if (change.Kind == ResourceEventKind.Spend) spends++;
        }

        return spends;
    }

    private (int TimeMs, int Gains) MeasureOccupancy(ResourceTypes type)
    {
        var changes = ChronaTracker.EventsBetween(type, Pull.StartTime, Pull.EndTime);
        var timeMs = 0;
        var gains = 0;

        for (var i = 0; i < changes.Count; i++)
        {
            var change = changes[i];
            if (change.Kind != ResourceEventKind.Gain) continue;
            if (change.Max <= 0 || change.CurrentAfter < change.Max) continue;

            gains++;
            var until = i + 1 < changes.Count ? changes[i + 1].Timestamp : Pull.EndTime;
            timeMs += Math.Max(0, Math.Min(until, Pull.EndTime) - change.Timestamp);
        }

        return (timeMs, gains);
    }

    private IReadOnlyList<CleanseReturn> MeasureCleanseReturns()
    {
        var casts = StaggerTracker.CleanseCastsBetween(Pull.StartTime, Pull.EndTime);
        var returns = new List<CleanseReturn>(casts.Count);

        for (var i = 0; i < casts.Count; i++)
        {
            var cast = casts[i];
            var limit = Math.Min(cast.Timestamp + CleanseReturnWindowMs, Pull.EndTime);
            if (i + 1 < casts.Count)
                limit = Math.Min(limit, casts[i + 1].Timestamp - 1);

            limit = Math.Max(limit, cast.Timestamp);

            int? cleared = null;
            var intervening = false;

            foreach (var unitId in cast.HealTargets)
            {
                if (StaggerTracker.MeasureCleanse(unitId, cast.Timestamp, CleanseReturnWindowMs) is not { } measured)
                    continue;

                cleared = (cleared ?? 0) + Math.Max(0, measured.ClearedAmount);
                intervening |= measured.HasInterveningEvent;
            }

            int? restored = null;

            foreach (var change in ChronaTracker.EventsBetween(ResourceTypes.Mana, cast.Timestamp, limit))
            {
                restored ??= 0;
                if (change.Kind == ResourceEventKind.Gain)
                    restored += change.Amount;
            }

            returns.Add(new CleanseReturn(cast.Timestamp, cast.Ability, cleared, restored, intervening));
        }

        return returns;
    }
}
