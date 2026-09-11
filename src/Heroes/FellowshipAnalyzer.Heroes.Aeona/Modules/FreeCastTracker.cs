using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>What made a cast free.</summary>
public enum FreeCastSource
{
    /// <summary>The cast spent a Uchronia window.</summary>
    Uchronia,

    /// <summary>An Epoch Break window covered the cast.</summary>
    EpochBreak,
}

/// <summary>One cast that cost no Chrona.</summary>
/// <param name="Timestamp">When the cast happened.</param>
/// <param name="AbilityId">The cast ability's FSLID.</param>
/// <param name="Source">What made the cast free.</param>
public readonly record struct FreeCast(int Timestamp, int AbilityId, FreeCastSource Source);

/// <summary>
/// Every free Oblivion, Amend Fate, and Restore Continuity cast across the dungeon, and the windows that
/// create them.
/// </summary>
/// <remarks>
/// <para>
/// A Uchronia window is spent by the Oblivion, Amend Fate, or Restore Continuity cast at its removal:
/// the cast within <see cref="CastMatchToleranceMs"/> of the removal, on either side, because the log
/// writes the two in either order inside one millisecond. A cast inside an Epoch Break window is free
/// from Epoch Break; Epoch Break takes precedence where the two overlap.
/// </para>
/// <para>
/// Registered dungeon-lifetime with no talent gate, because Epoch Break is in every build. Uchronia's
/// windows are read through <see cref="UchroniaTracker"/> when the build has the talent.
/// </para>
/// </remarks>
public sealed partial class FreeCastTracker : Analyzer
{
    /// <summary>Milliseconds a spending cast may sit from the Uchronia removal it pairs with.</summary>
    public const int CastMatchToleranceMs = 50;

    private readonly List<FreeCast> _freeCasts = [];
    private readonly List<AuraWindow> _epochBreakWindows = [];
    private readonly List<(int Timestamp, int AbilityId)> _spenderCasts = [];
    private readonly List<int> _unpairedRemovals = [];

    private int? _epochBreakOpenedAt;

    /// <summary>Every free cast, in log order.</summary>
    public IReadOnlyList<FreeCast> FreeCasts => _freeCasts;

    /// <summary>Every Epoch Break window on the player, in the order they opened.</summary>
    public IReadOnlyList<AuraWindow> EpochBreakWindows =>
        _epochBreakOpenedAt is { } start ? [.. _epochBreakWindows, CloseAtDungeonEnd(start)] : _epochBreakWindows;

    /// <summary>The free casts between <paramref name="start"/> and <paramref name="end"/>, both bounds inclusive.</summary>
    /// <param name="start">The first instant to include.</param>
    /// <param name="end">The last instant to include.</param>
    public IReadOnlyList<FreeCast> FreeCastsBetween(int start, int end) =>
        [.. _freeCasts.Where(freeCast => freeCast.Timestamp >= start && freeCast.Timestamp <= end)];

    /// <summary>
    /// Chances to make a free cast opened between <paramref name="start"/> and <paramref name="end"/>:
    /// every Uchronia window and every Epoch Break window that opened in the range.
    /// </summary>
    /// <param name="start">The first instant to include.</param>
    /// <param name="end">The last instant to include.</param>
    public int OpportunitiesBetween(int start, int end)
    {
        var uchronia = Uchronia?.Windows.Count(window => window.Start >= start && window.Start <= end) ?? 0;
        var epochBreak = EpochBreakWindows.Count(window => window.Start >= start && window.Start <= end);

        return uchronia + epochBreak;
    }

    /// <summary>
    /// The free cast of <paramref name="abilityId"/> nearest <paramref name="timestamp"/> within
    /// <see cref="CastMatchToleranceMs"/>, or null when that cast cost Chrona.
    /// </summary>
    /// <param name="timestamp">The cast's timestamp.</param>
    /// <param name="abilityId">The cast ability's FSLID.</param>
    public FreeCast? FreeCastAt(int timestamp, int abilityId)
    {
        FreeCast? nearest = null;
        var nearestDistance = int.MaxValue;

        foreach (var freeCast in _freeCasts)
        {
            if (freeCast.AbilityId != abilityId) continue;

            var distance = Math.Abs(freeCast.Timestamp - timestamp);
            if (distance > CastMatchToleranceMs || distance >= nearestDistance) continue;

            nearest = freeCast;
            nearestDistance = distance;
        }

        return nearest;
    }

    /// <summary>Whether the cast of <paramref name="abilityId"/> at <paramref name="timestamp"/> was free.</summary>
    /// <param name="timestamp">The cast's timestamp.</param>
    /// <param name="abilityId">The cast ability's FSLID.</param>
    public bool IsFree(int timestamp, int abilityId) => FreeCastAt(timestamp, abilityId) is not null;

    /// <summary>Whether an Epoch Break window covered <paramref name="timestamp"/>, endpoints included.</summary>
    /// <param name="timestamp">The instant asked about.</param>
    public bool EpochBreakActive(int timestamp)
    {
        if (_epochBreakOpenedAt is { } start && timestamp >= start && timestamp <= CloseAtDungeonEnd(start).End)
            return true;

        foreach (var window in _epochBreakWindows)
        {
            if (timestamp >= window.Start && timestamp <= window.End) return true;
        }

        return false;
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.EpochBreakSelfBuff))]
    private void OnEpochBreakApplied(ApplyBuffEvent e) => _epochBreakOpenedAt ??= e.Timestamp;

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.EpochBreakSelfBuff))]
    private void OnEpochBreakRefreshed(RefreshBuffEvent e) => _epochBreakOpenedAt ??= e.Timestamp;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.EpochBreakSelfBuff))]
    private void OnEpochBreakRemoved(RemoveBuffEvent e)
    {
        if (_epochBreakOpenedAt is not { } start) return;

        _epochBreakWindows.Add(new AuraWindow(start, Math.Max(start, e.Timestamp)));
        _epochBreakOpenedAt = null;
    }

    [On<CastEvent>(By = Actor.Player, Spells = [nameof(Spells.Oblivion), nameof(Spells.AmendFate), nameof(Spells.RestoreContinuity)])]
    private void OnSpenderCast(CastEvent e)
    {
        var abilityId = e.Ability.Id;

        if (EpochBreakActive(e.Timestamp))
        {
            _freeCasts.Add(new FreeCast(e.Timestamp, abilityId, FreeCastSource.EpochBreak));
            return;
        }

        var removal = _unpairedRemovals.FindIndex(timestamp => Math.Abs(timestamp - e.Timestamp) <= CastMatchToleranceMs);
        if (removal >= 0)
        {
            _unpairedRemovals.RemoveAt(removal);
            _freeCasts.Add(new FreeCast(e.Timestamp, abilityId, FreeCastSource.Uchronia));
            return;
        }

        _spenderCasts.Add((e.Timestamp, abilityId));
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.Uchronia))]
    private void OnUchroniaRemoved(RemoveBuffEvent e)
    {
        var cast = _spenderCasts.FindLastIndex(cast => Math.Abs(cast.Timestamp - e.Timestamp) <= CastMatchToleranceMs);
        if (cast >= 0)
        {
            var (timestamp, abilityId) = _spenderCasts[cast];
            _spenderCasts.RemoveAt(cast);
            _freeCasts.Add(new FreeCast(timestamp, abilityId, FreeCastSource.Uchronia));
            return;
        }

        _unpairedRemovals.Add(e.Timestamp);
    }

    private UchroniaTracker? Uchronia => field ??= Owner.GetModule<UchroniaTracker>();

    private AuraWindow CloseAtDungeonEnd(int start) => new(start, Math.Max(start, Owner.DungeonEndTime));
}
