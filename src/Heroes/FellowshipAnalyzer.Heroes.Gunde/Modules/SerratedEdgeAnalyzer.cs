using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Judges where each Serrated Edge went. Blood Arc grants the buff on every cast and the next
/// damaging ability eats it for 20% more bleed damage, so the buff is not a resource that can be
/// hoarded - it is a choice of which ability follows Blood Arc. Heart Splitter and Grim Carve are
/// the abilities worth putting it on; spending it on filler is the whole of the waste this measures.
/// Each grant is recorded with the ability that actually consumed it, so a pull is judged one grant
/// at a time rather than against its own best stretch.
/// </summary>
/// <remarks>
/// <para>
/// A grant runs from the Serrated Edge self-buff appearing to it being removed. The consuming cast
/// reaches the log a millisecond or two ahead of that removal, so the ability holding
/// <see cref="ConsumerGraceMs"/> before it is the consumer. Live Season 3 data bears this out: 442
/// grants, 442 removals, every one within a hasted global cooldown of the cast that took it, and
/// none held anywhere near the buff's eight second duration.
/// </para>
/// <para>
/// Only the abilities observed consuming the buff in live data are treated as candidates, so a
/// removal that no such cast explains is reported as unspent rather than blamed on whichever button
/// happened to be pressed. Blood Arc is one of them: a second Blood Arc consumes the buff its
/// predecessor granted and immediately grants another, which the log shows as a removal and an
/// application sharing a millisecond.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class SerratedEdgeAnalyzer : Analyzer
{
    /// <summary>
    /// Window before a removal in which a cast is the ability that consumed the buff. Every pair in
    /// live data shares a millisecond or sits one apart, so this is an envelope around log ordering
    /// rather than a measured gap.
    /// </summary>
    public const int ConsumerGraceMs = 250;

    private readonly List<SerratedEdgeGrant> _grants = [];

    private int? _grantedAt;
    private int _lastCastTimestamp = int.MinValue;
    private int _lastCastAbilityId;

    /// <summary>Every Serrated Edge grant on the pull, in encounter order, with what consumed it.</summary>
    public IReadOnlyList<SerratedEdgeGrant> Grants => _grants;

    /// <summary>
    /// Grants that closed within the pull. A buff still up when the pull ends is left out entirely,
    /// since it carries into the next pull rather than having been wasted.
    /// </summary>
    public int JudgedGrants => _grants.Count;

    /// <summary>Grants Heart Splitter or Grim Carve consumed.</summary>
    public int WellSpent => _grants.Count(grant => grant.WellSpent);

    /// <summary>Grants some other ability consumed.</summary>
    public int Misspent => _grants.Count(grant => grant.ConsumerAbilityId is not null && !grant.WellSpent);

    /// <summary>Grants that ended with no cast to account for them, so the buff went unused.</summary>
    public int Unspent => _grants.Count(grant => grant.ConsumerAbilityId is null);

    /// <summary>Whether an ability is one of the two Serrated Edge is worth spending on.</summary>
    public static bool IsIntendedConsumer(int abilityId) =>
        abilityId == Spells.HeartSplitter.FSLID.Value || abilityId == Spells.GrimCarve.FSLID.Value;

    [On<CastEvent>(By = Actor.Player, Spells = [
        nameof(Spells.HeartSplitter),
        nameof(Spells.GrimCarve),
        nameof(Spells.Rupture),
        nameof(Spells.BloodArc),
        nameof(Spells.ReaverEdge),
        nameof(Spells.DoubleStrike)])]
    private void OnCandidateCast(CastEvent castEvent)
    {
        _lastCastTimestamp = castEvent.Timestamp;
        _lastCastAbilityId = castEvent.Ability.Id;
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SerratedEdge))]
    private void OnGranted(ApplyBuffEvent buffEvent) => _grantedAt = buffEvent.Timestamp;

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SerratedEdge))]
    private void OnRegranted(RefreshBuffEvent buffEvent) => _grantedAt = buffEvent.Timestamp;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SerratedEdge))]
    private void OnRemoved(RemoveBuffEvent buffEvent)
    {
        if (_grantedAt is not { } granted) return;

        _grantedAt = null;
        _grants.Add(new SerratedEdgeGrant(granted, buffEvent.Timestamp, ConsumerAt(granted, buffEvent.Timestamp)));
    }

    /// <summary>
    /// The ability that took the buff: the last candidate cast, provided it landed inside the grant
    /// and within <see cref="ConsumerGraceMs"/> of the removal. Null means the buff was removed with
    /// nothing to account for it, which is a buff that went unused.
    /// </summary>
    private int? ConsumerAt(int granted, int removed) =>
        _lastCastTimestamp >= granted && removed - _lastCastTimestamp <= ConsumerGraceMs
            ? _lastCastAbilityId
            : null;
}

/// <summary>One Serrated Edge buff, from the Blood Arc that granted it to the ability that ate it.</summary>
/// <param name="Timestamp">Encounter time the buff appeared.</param>
/// <param name="ConsumedTimestamp">Encounter time the buff was removed.</param>
/// <param name="ConsumerAbilityId">
/// The ability that consumed the buff, or null when the removal had no cast to account for it.
/// </param>
public sealed record SerratedEdgeGrant(int Timestamp, int ConsumedTimestamp, int? ConsumerAbilityId)
{
    /// <summary>Whether Heart Splitter or Grim Carve took the buff.</summary>
    public bool WellSpent => ConsumerAbilityId is { } consumer && SerratedEdgeAnalyzer.IsIntendedConsumer(consumer);

    /// <summary>How long the buff was held before something consumed it.</summary>
    public int HeldMs => ConsumedTimestamp - Timestamp;
}
