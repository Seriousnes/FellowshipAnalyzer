using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Measures how well Gunde banked and deployed Blood Feathers. Rend ticks drop feather orbs, each
/// pickup adds one stack of the Owed in Blood self-buff, and the Owed in Blood ability converts every
/// held stack one-for-one into Rend on the target. A conversion is therefore worth exactly the pile
/// standing behind it, and stacks leak in two ways: the buff falls off with stacks still held, and
/// pickups stop accruing once the pool is pinned at <see cref="MaxStacks"/>. This analyzer records the
/// size of every conversion, the stacks lost to decay, and the time spent at the cap.
/// </summary>
/// <remarks>
/// <para>
/// The stack count is read from the buff event stream rather than from resource snapshots, because
/// buff stacks are the standard Fellowship Logs mechanism and <see cref="ApplyBuffStackEvent.Stack"/>
/// carries an absolute count. <see cref="BloodFeatherTracker"/>'s Tertiary resource covers the
/// snapshot view independently. Only the <c>Spells.OwedInBloodSelfBuff</c> effect is tracked;
/// <c>Spells.OwedInBloodSelfBuffFromOrbs</c> is the unnamed pickup-side applicator and its logged
/// behaviour is unverified, so it is deliberately left alone rather than guessed at.
/// </para>
/// <para>
/// Decay is detected purely from events - a drop in the stack count with no Owed in Blood cast within
/// <see cref="ConversionGraceMs"/> before it - so no buff duration is assumed anywhere. Stacks still
/// held when the pull ends count as neither converted nor decayed, since holding a bank into the next
/// pull is not waste.
/// </para>
/// <para>
/// Feathers and the self-buff survive across pull boundaries while an analyzer does not, so a pull
/// whose first Owed in Blood converts a bank built on the previous pull records that conversion at
/// zero stacks. That reads the same as pressing the button empty and is recorded honestly as such.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class OwedInBloodEconomyAnalyzer : Analyzer
{
    /// <summary>
    /// Blood Feather stacks Gunde can hold. Mirrors the game data cap and the Tertiary resource
    /// override on <see cref="BloodFeatherTracker"/>; there is no generated registry constant for it.
    /// </summary>
    public const int MaxStacks = BloodFeatherTracker.MaxBloodFeathers;

    /// <summary>
    /// Window after an Owed in Blood cast in which a falling stack count is the cast consuming the
    /// bank rather than the bank expiring.
    /// </summary>
    public const int ConversionGraceMs = 1_000;

    private readonly List<OwedInBloodConversion> _conversions = [];

    private int _stacks;
    private int _decayedStacks;
    private int _cappedMs;
    private int? _cappedSince;
    private int? _lastConversion;

    private Computed? _computed;
    private Computed Result => _computed ??= Compute();

    /// <summary>Every Owed in Blood cast on the pull, in encounter order, with the bank it cashed in.</summary>
    public IReadOnlyList<OwedInBloodConversion> Conversions => _conversions;

    /// <summary>Blood Feather stacks turned into Rend across every conversion on the pull.</summary>
    public int TotalStacksConverted => Result.TotalStacksConverted;

    /// <summary>The largest single conversion on the pull, or zero when none were made.</summary>
    public int BestConversion => Result.BestConversion;

    /// <summary>Mean conversion size, or zero when no conversion was made.</summary>
    public double AverageConversion => Result.AverageConversion;

    /// <summary>Stacks the buff shed without an Owed in Blood cast to account for them.</summary>
    public int DecayedStacks => _decayedStacks;

    /// <summary>
    /// Milliseconds the bank sat at <see cref="MaxStacks"/>, during which further pickups could add
    /// nothing. A span still open when the pull ends is closed at the pull boundary.
    /// </summary>
    public int CappedMs => Result.CappedMs;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.OwedInBloodSelfBuff))]
    private void OnBuffApplied(ApplyBuffEvent @event) => SetStacks(@event.Timestamp, 1);

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.OwedInBloodSelfBuff))]
    private void OnStackApplied(ApplyBuffStackEvent @event) => SetStacks(@event.Timestamp, @event.Stack);

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.OwedInBloodSelfBuff))]
    private void OnStackRemoved(RemoveBuffStackEvent @event) => SetStacks(@event.Timestamp, @event.Stack);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.OwedInBloodSelfBuff))]
    private void OnBuffRemoved(RemoveBuffEvent @event) => SetStacks(@event.Timestamp, 0);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.OwedInBlood))]
    private void OnConverted(CastEvent @event)
    {
        _lastConversion = @event.Timestamp;
        _conversions.Add(new OwedInBloodConversion(@event.Timestamp, _stacks));
    }

    /// <summary>
    /// Moves the tracked bank to an absolute count. Any decrease that no recent conversion explains is
    /// the buff expiring, so the shortfall is booked as decay. The count is never forced to zero on a
    /// cast: the buff reports its own consumption, and an absolute stack event corrects the running
    /// total if a log ever omits the removal.
    /// </summary>
    private void SetStacks(int timestamp, int stacks)
    {
        var lost = _stacks - stacks;
        if (lost > 0 && !IsConverting(timestamp))
            _decayedStacks += lost;

        UpdateCapWindow(timestamp, stacks);
        _stacks = stacks;
    }

    private bool IsConverting(int timestamp) =>
        _lastConversion is { } cast && timestamp - cast is >= 0 and <= ConversionGraceMs;

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

    /// <summary>
    /// Folds the conversions into their aggregates and closes a capped span left open at the pull
    /// boundary. Computed once, on first read.
    /// </summary>
    private Computed Compute()
    {
        var cappedMs = _cappedMs;
        if (_cappedSince is { } since)
            cappedMs += Math.Max(0, Pull.EndTime - since);

        var total = 0;
        var best = 0;
        foreach (var conversion in _conversions)
        {
            total += conversion.StacksConverted;
            if (conversion.StacksConverted > best)
                best = conversion.StacksConverted;
        }

        var average = _conversions.Count > 0 ? (double)total / _conversions.Count : 0d;
        return new Computed(total, best, average, cappedMs);
    }

    private readonly record struct Computed(int TotalStacksConverted, int BestConversion, double AverageConversion, int CappedMs);

    /// <summary>One Owed in Blood cast and the Blood Feather bank it converted into Rend.</summary>
    public sealed record OwedInBloodConversion(int Timestamp, int StacksConverted);
}
