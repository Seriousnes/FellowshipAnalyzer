using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Judges where each Serrated Edge went. Blood Arc grants the buff on every cast and the next
/// damaging ability eats it for 20% more bleed damage, so the buff is not a resource that can be
/// hoarded - it is a choice of which ability follows Blood Arc. Which ability that should be depends
/// on the pull: Grim Carve is what a pack wants, Heart Splitter is what a boss wants, and each is
/// still a reasonable home on the other shape. Filler is the sub-optimal home, but only a mistake
/// when one of those two was actually ready to be pressed instead.
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
/// <para>
/// Whether a better home was available is asked of <see cref="SpellUsable"/> at the consuming cast,
/// so haste, cooldown acceleration and charges are all accounted for rather than assumed from the
/// curated cooldown. Both Heart Splitter and Grim Carve are checked whatever the pull shape, because
/// either beats filler. Their remaining cooldowns are recorded alongside the verdict so a near miss
/// can be told from a genuinely forced one.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
[Uses<SpellUsable>]
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
    private CooldownSnapshot _lastCastCooldowns;

    /// <summary>The pull shape, which decides which of the two homes this pull wanted.</summary>
    public GundePullShape Shape => Pull.Targets == PullKind.Single ? GundePullShape.Boss : GundePullShape.Aoe;

    /// <summary>The ability this pull shape wants Serrated Edge spent on.</summary>
    public int PriorityAbilityId =>
        Shape == GundePullShape.Aoe ? Spells.GrimCarve.FSLID.Value : Spells.HeartSplitter.FSLID.Value;

    /// <summary>
    /// The other of Heart Splitter and Grim Carve - a reasonable home, but not the one this shape
    /// calls for.
    /// </summary>
    public int AlternateAbilityId =>
        Shape == GundePullShape.Aoe ? Spells.HeartSplitter.FSLID.Value : Spells.GrimCarve.FSLID.Value;

    /// <summary>Every Serrated Edge grant on the pull, in encounter order, with what consumed it.</summary>
    public IReadOnlyList<SerratedEdgeGrant> Grants => _grants;

    /// <summary>
    /// Grants that closed within the pull. A buff still up when the pull ends is left out entirely,
    /// since it carries into the next pull rather than having been wasted.
    /// </summary>
    public int JudgedGrants => _grants.Count;

    /// <summary>Grants the pull shape's priority ability consumed.</summary>
    public int PriorityConsumed => Count(SerratedEdgeOutcome.Priority);

    /// <summary>Grants the other of Heart Splitter and Grim Carve consumed.</summary>
    public int AlternateConsumed => Count(SerratedEdgeOutcome.Alternate);

    /// <summary>Grants filler consumed while Heart Splitter or Grim Carve was ready to be pressed.</summary>
    public int AvoidableFiller => Count(SerratedEdgeOutcome.AvoidableFiller);

    /// <summary>Grants filler consumed with both Heart Splitter and Grim Carve on cooldown.</summary>
    public int ForcedFiller => Count(SerratedEdgeOutcome.ForcedFiller);

    /// <summary>Grants that ended with no cast to account for them, so the buff went unused.</summary>
    public int Unspent => Count(SerratedEdgeOutcome.Unspent);

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
        _lastCastCooldowns = Snapshot(castEvent.Timestamp);
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
        var consumer = ConsumerAt(granted, buffEvent.Timestamp);
        var cooldowns = consumer is null ? Snapshot(buffEvent.Timestamp) : _lastCastCooldowns;

        _grants.Add(new SerratedEdgeGrant(
            granted,
            consumer,
            cooldowns.HeartSplitterReady,
            cooldowns.HeartSplitterRemainingMs,
            cooldowns.GrimCarveReady,
            cooldowns.GrimCarveRemainingMs,
            Classify(consumer, cooldowns)));
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

    /// <summary>
    /// Places a grant on the four-way scale: the home this shape wanted, the other of the two homes,
    /// filler while a home was ready to be pressed, or filler with both on cooldown. Readiness is
    /// asked of both homes regardless of shape, because either is a better place for the buff than
    /// filler is.
    /// </summary>
    private SerratedEdgeOutcome Classify(int? consumer, CooldownSnapshot cooldowns)
    {
        if (consumer is not { } ability) return SerratedEdgeOutcome.Unspent;
        if (ability == PriorityAbilityId) return SerratedEdgeOutcome.Priority;
        if (ability == AlternateAbilityId) return SerratedEdgeOutcome.Alternate;

        return cooldowns.HeartSplitterReady || cooldowns.GrimCarveReady
            ? SerratedEdgeOutcome.AvoidableFiller
            : SerratedEdgeOutcome.ForcedFiller;
    }

    private CooldownSnapshot Snapshot(int timestamp) => new(
        SpellUsable.IsAvailable(Spells.HeartSplitter.FSLID.Value),
        SpellUsable.CooldownRemaining(Spells.HeartSplitter.FSLID.Value, timestamp),
        SpellUsable.IsAvailable(Spells.GrimCarve.FSLID.Value),
        SpellUsable.CooldownRemaining(Spells.GrimCarve.FSLID.Value, timestamp));

    private int Count(SerratedEdgeOutcome outcome) => _grants.Count(grant => grant.Outcome == outcome);

    private readonly record struct CooldownSnapshot(
        bool HeartSplitterReady,
        int HeartSplitterRemainingMs,
        bool GrimCarveReady,
        int GrimCarveRemainingMs);
}

/// <summary>Where a Serrated Edge buff ended up, on the scale the pull shape sets.</summary>
public enum SerratedEdgeOutcome
{
    /// <summary>Consumed by the ability this pull shape wants it on.</summary>
    Priority,

    /// <summary>Consumed by the other of Heart Splitter and Grim Carve.</summary>
    Alternate,

    /// <summary>Consumed by filler while Heart Splitter or Grim Carve was ready to be pressed instead.</summary>
    AvoidableFiller,

    /// <summary>Consumed by filler with both Heart Splitter and Grim Carve on cooldown.</summary>
    ForcedFiller,

    /// <summary>Removed with no cast to account for it.</summary>
    Unspent,
}

/// <summary>One Serrated Edge buff, from the Blood Arc that granted it to the ability that ate it.</summary>
/// <param name="Timestamp">Encounter time the buff appeared.</param>
/// <param name="ConsumerAbilityId">
/// The ability that consumed the buff, or null when the removal had no cast to account for it.
/// </param>
/// <param name="HeartSplitterReady">Whether Heart Splitter could have been pressed instead.</param>
/// <param name="HeartSplitterRemainingMs">
/// Heart Splitter's remaining cooldown, so a near miss reads differently from a forced one. A spell
/// with a spare charge is ready even while a recharge runs, so this can be non-zero on a ready ability.
/// </param>
/// <param name="GrimCarveReady">Whether Grim Carve could have been pressed instead.</param>
/// <param name="GrimCarveRemainingMs">Grim Carve's remaining cooldown, on the same terms.</param>
/// <param name="Outcome">Where the buff ended up on the pull shape's scale.</param>
public sealed record SerratedEdgeGrant(
    int Timestamp,
    int? ConsumerAbilityId,
    bool HeartSplitterReady,
    int HeartSplitterRemainingMs,
    bool GrimCarveReady,
    int GrimCarveRemainingMs,
    SerratedEdgeOutcome Outcome);
