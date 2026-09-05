using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>What made a cast free.</summary>
public enum FreeCastSource
{
    /// <summary>A Uchronia window covered the cast.</summary>
    Uchronia,

    /// <summary>An Epoch Break window covered the cast.</summary>
    EpochBreak,

    /// <summary>Neither window covered the cast, so a Spirit ability or another effect made it free.</summary>
    Other,
}

/// <summary>One cast that cost no resources.</summary>
/// <param name="Timestamp">When the cast happened, in milliseconds.</param>
/// <param name="AbilityId">The cast ability's FSLID, as given on <see cref="Ability.Id"/>.</param>
/// <param name="Source">What made the cast free.</param>
public readonly record struct FreeCast(int Timestamp, int AbilityId, FreeCastSource Source);

/// <summary>
/// Every free cast the player made across the dungeon and the windows that create them, so a segment can
/// ask both what a free cast was spent on and how many chances to spend one the pull offered.
/// </summary>
/// <remarks>
/// <para>
/// Registered dungeon-lifetime with no talent gate, because Epoch Break is in every build and free casts
/// arrive whether or not Uchronia is taken. Uchronia's windows are read through
/// <see cref="UchroniaTracker"/> when the build has the talent, which keeps the two windows' own
/// bookkeeping where it belongs.
/// </para>
/// <para>
/// Window membership includes the endpoints, so a free cast logged in the same millisecond as the removal
/// that consumed the buff is attributed to that window.
/// </para>
/// </remarks>
public sealed partial class FreeCastTracker : Analyzer
{
    /// <summary>Milliseconds by which the free-cast record and the cast record for one cast may differ in <see cref="FreeCastAt"/>.</summary>
    public const int CastMatchToleranceMs = 50;

    private readonly List<FreeCast> _freeCasts = [];
    private readonly List<AuraWindow> _epochBreakWindows = [];

    private int? _epochBreakOpenedAt;

    /// <summary>Every free cast the player made, in log order.</summary>
    public IReadOnlyList<FreeCast> FreeCasts => _freeCasts;

    /// <summary>Whether the log reported any free cast at all.</summary>
    public bool HasFreeCasts => _freeCasts.Count > 0;

    /// <summary>Every Epoch Break window on the player, in the order they opened.</summary>
    public IReadOnlyList<AuraWindow> EpochBreakWindows =>
        _epochBreakOpenedAt is { } start ? [.. _epochBreakWindows, CloseAtDungeonEnd(start)] : _epochBreakWindows;

    /// <summary>The free casts between <paramref name="start"/> and <paramref name="end"/>, endpoints included.</summary>
    /// <param name="start">The first instant to include.</param>
    /// <param name="end">The last instant to include.</param>
    public IReadOnlyList<FreeCast> FreeCastsBetween(int start, int end) =>
        [.. _freeCasts.Where(freeCast => freeCast.Timestamp >= start && freeCast.Timestamp <= end)];

    /// <summary>
    /// How many chances to spend a free cast opened between <paramref name="start"/> and
    /// <paramref name="end"/>: every Uchronia window and every Epoch Break window that opened in the range.
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
    /// <see cref="CastMatchToleranceMs"/>, or <see langword="null"/> when the cast cost resources.
    /// </summary>
    /// <param name="timestamp">The cast's timestamp in milliseconds.</param>
    /// <param name="abilityId">The cast ability's FSLID, as given on <see cref="Ability.Id"/>.</param>
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

    [On<FreeCastEvent>(By = Actor.Player)]
    private void OnFreeCast(FreeCastEvent e)
    {
        var abilityId = e.Ability?.Id ?? e.AbilityGameId.Value;
        _freeCasts.Add(new FreeCast(e.Timestamp, abilityId, SourceAt(e.Timestamp)));
    }

    /// <summary>Epoch Break takes precedence over an overlapping Uchronia window.</summary>
    private FreeCastSource SourceAt(int timestamp)
    {
        if (EpochBreakActive(timestamp)) return FreeCastSource.EpochBreak;
        if (Uchronia?.IsActive(timestamp) == true) return FreeCastSource.Uchronia;

        return FreeCastSource.Other;
    }

    private UchroniaTracker? Uchronia => field ??= Owner.GetModule<UchroniaTracker>();

    private AuraWindow CloseAtDungeonEnd(int start) => new(start, Math.Max(start, Owner.DungeonEndTime));
}
