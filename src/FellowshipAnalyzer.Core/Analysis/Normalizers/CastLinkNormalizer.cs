using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis.Normalizers;

/// <summary>
/// Pre-processes cast and channel events before any module sees them:
/// <list type="bullet">
///   <item>Removes FellowshipLogs <c>fake+activation</c> cast events (redundant with the accompanying begincast).</item>
///   <item>Converts <c>activation</c>-only cast events (cast-start markers with no begincast) into fabricated <see cref="BeginCastEvent"/>s.</item>
///   <item>Links <see cref="BeginCastEvent"/> to its completing <see cref="CastEvent"/> via <see cref="BeginCastEvent.CastEvent"/>.</item>
///   <item>Links <see cref="EndChannelEvent"/> back to its <see cref="BeginChannelEvent"/> via <see cref="EndChannelEvent.BeginChannel"/>.</item>
///   <item>Marks <see cref="BeginCastEvent.IsCancelled"/> = true when a cast has no matching completion event.</item>
///   <item>
///     When a <see cref="CastEvent"/> is immediately followed by a <see cref="BeginChannelEvent"/> for the same
///     source and spell GUID (within <see cref="MaxChannelCastWindowMs"/>), establishes the expected
///     cast-to-channel relationship contract:
///     <list type="bullet">
///       <item><see cref="CastEvent.Channel"/> → <see cref="EndChannelEvent"/> (via <see cref="BaseCastEvent.Channel"/>)</item>
///       <item><see cref="BeginCastEvent.Channel"/> → <see cref="BeginChannelEvent"/> (when a begincast preceded the cast)</item>
///       <item><see cref="EndChannelEvent.BeginChannel"/> → <see cref="BeginChannelEvent"/> (already linked)</item>
///     </list>
///   </item>
/// </list>
/// </summary>
public sealed class CastLinkNormalizer(Abilities? abilities) : IEventNormalizer
{
    /// <summary>
    /// Maximum milliseconds between a <see cref="CastEvent"/> and the following
    /// <see cref="BeginChannelEvent"/> for the same spell/source to be treated as
    /// a single cast-to-channel sequence. Fellowship logs emit them at the same timestamp;
    /// this generous window tolerates minor log jitter.
    /// </summary>
    private const int MaxChannelCastWindowMs = 200;

    public int Priority => 0;

    public List<Event> Normalize(List<Event> events, int playerId)
    {
        // Build a set of spell IDs that can be cast while another cast is in-progress
        // (e.g. off-GCD instants). These do NOT cancel a pending cast.
        var castableWhileCasting = abilities?.Spellbook()
            .Where(a => a.CastableWhileCasting)
            .Select(a => a.PrimarySpell.Guid)
            .ToHashSet() ?? [];

        // Additional spell IDs from AdditionalSpells
        if (abilities is not null)
        {
            foreach (var a in abilities.Spellbook())
            {
                if (a.CastableWhileCasting && a.AdditionalSpells is not null)
                    foreach (var spell in a.AdditionalSpells)
                        castableWhileCasting.Add(spell.Guid);
            }
        }

        var pendingCasts = new Dictionary<(int abilityId, int sourceId), BeginCastEvent>();
        var pendingChannels = new Dictionary<(int abilityId, int sourceId), BeginChannelEvent>();

        // Per-source tracking for cancel detection.
        var lastBeginCast = new Dictionary<int, BeginCastEvent>();

        // Cast-to-channel linking state:
        // Tracks casts that may transition into a channel (keyed by abilityGuid + sourceId).
        // Entry is cleared when a BeginChannelEvent matches within MaxChannelCastWindowMs, or
        // naturally overwritten when the same spell is cast again.
        var pendingChannelCasts = new Dictionary<(int abilityId, int sourceId), (CastEvent Cast, BeginCastEvent? BeginCast, int Timestamp)>();
        // Tracks the cast that has been linked to a pending channel, waiting for EndChannelEvent.
        var pendingCastForEndChannel = new Dictionary<(int abilityId, int sourceId), CastEvent>();

        // Pre-pass: identify activation CastEvents that have a matching non-activation
        // CastEvent at the same timestamp (instant casts with duplicate events).
        // These duplicates must be dropped to avoid double-cast icons.
        var duplicateActivations = new HashSet<(int timestamp, int abilityGuid, int sourceId)>();
        foreach (var e in events)
        {
            if (e is CastEvent { Activation: false, Fake: false } cast && cast.Ability is not null)
                duplicateActivations.Add((cast.Timestamp, cast.Ability.Guid, cast.SourceId));
        }

        var fixedEvents = new List<Event>();

        foreach (var e in events)
        {
            // ── Activation cast events ────────────────────────────────────────────
            // FellowshipLogs emits cast events with activation=true to mark cast-starts:
            //   Pattern A: cast(fake=true, activation=true) + begincast  → drop the fake cast, keep begincast
            //   Pattern B: cast(activation=true) only — this IS the complete cast for instant spells.
            //              If a matching non-activation success event exists at the same timestamp,
            //              drop this event to avoid a double-cast icon. Otherwise, keep it as
            //              a regular CastEvent so SpellUsable and the timeline see it.
            if (e is CastEvent { Activation: true } activationCast)
            {
                if (activationCast.Fake)
                    continue; // Pattern A — redundant, the real begincast follows

                // Pattern B — check for a duplicate success event at the same timestamp
                if (activationCast.Ability is not null &&
                    duplicateActivations.Contains((activationCast.Timestamp, activationCast.Ability.Guid, activationCast.SourceId)))
                    continue; // Duplicate — the non-activation CastEvent is the real one

                // No matching success event: this IS the complete cast (instant).
                // Fall through to normal CastEvent processing.
            }

            fixedEvents.Add(e);

            switch (e)
            {
                case BeginCastEvent bc when bc.Ability is not null:
                    {
                        var key = (bc.Ability.Guid, bc.SourceId);
                        CancelPending(lastBeginCast, bc.SourceId, key, pendingCasts);
                        pendingCasts[key] = bc;
                        lastBeginCast[bc.SourceId] = bc;
                        break;
                    }

                case CastEvent cast when cast.Ability is not null:
                    {
                        var castKey = (cast.Ability.Guid, cast.SourceId);

                        // Check whether this cast cancels a pending begincast for a different spell
                        if (lastBeginCast.TryGetValue(cast.SourceId, out var pending)
                            && pending.Ability?.Guid != cast.Ability.Guid)
                        {
                            // This cast is for a different ability. If the new ability is
                            // castable-while-casting, don't cancel the pending begincast.
                            if (!castableWhileCasting.Contains(cast.Ability.Guid))
                            {
                                pending.IsCancelled = true;
                                pending.CastEvent = null;
                                lastBeginCast.Remove(cast.SourceId);
                                pendingCasts.Remove((pending.Ability!.Guid, pending.SourceId));
                            }
                            // else: castable-while-casting — don't disturb the pending begincast
                        }

                        BeginCastEvent? linkedBeginCast = null;
                        if (pendingCasts.TryGetValue(castKey, out var beginCast))
                        {
                            beginCast.CastEvent = cast;
                            linkedBeginCast = beginCast;
                            pendingCasts.Remove(castKey);
                            lastBeginCast.Remove(cast.SourceId);
                        }

                        // Store as a potential channel cast. If a BeginChannelEvent for the same
                        // spell/source arrives within MaxChannelCastWindowMs, these will be linked.
                        pendingChannelCasts[castKey] = (cast, linkedBeginCast, cast.Timestamp);
                        break;
                    }

                case BeginChannelEvent beginChannel when beginChannel.Ability is not null:
                    {
                        var channelKey = (beginChannel.Ability.Guid, beginChannel.SourceId);
                        pendingChannels[channelKey] = beginChannel;

                        // If a cast for this spell/source just fired within the window, treat
                        // the pair as a single cast-to-channel sequence and establish links.
                        if (pendingChannelCasts.TryGetValue(channelKey, out var castInfo)
                            && beginChannel.Timestamp - castInfo.Timestamp <= MaxChannelCastWindowMs)
                        {
                            // BeginCastEvent.Channel → BeginChannelEvent
                            if (castInfo.BeginCast is not null)
                                castInfo.BeginCast.Channel = beginChannel;

                            // Remember the cast so we can set CastEvent.Channel when endchannel arrives.
                            pendingCastForEndChannel[channelKey] = castInfo.Cast;
                            pendingChannelCasts.Remove(channelKey);
                        }
                        break;
                    }

                case EndChannelEvent ec when ec.Ability is not null:
                    var ecKey = (ec.Ability.Guid, ec.SourceId);
                    if (pendingChannels.TryGetValue(ecKey, out var beginChannel2))
                    {
                        ec.BeginChannel = beginChannel2;
                        pendingChannels.Remove(ecKey);
                    }
                    // CastEvent.Channel → EndChannelEvent (completing the contract)
                    if (pendingCastForEndChannel.TryGetValue(ecKey, out var channelCast))
                    {
                        channelCast.Channel = ec;
                        pendingCastForEndChannel.Remove(ecKey);
                    }
                    break;
            }
        }

        // Any remaining pending beginCasts were never completed → cancelled
        foreach (var bc in lastBeginCast.Values)
        {
            bc.IsCancelled = true;
            bc.CastEvent = null;
        }

        return fixedEvents;
    }

    /// <summary>
    /// Cancels any existing pending begincast for <paramref name="sourceId"/> when a new
    /// begincast for a different ability arrives.
    /// </summary>
    private static void CancelPending(
        Dictionary<int, BeginCastEvent> lastBeginCast,
        int sourceId,
        (int abilityId, int sourceId) newKey,
        Dictionary<(int, int), BeginCastEvent> pendingCasts)
    {
        if (!lastBeginCast.TryGetValue(sourceId, out var existing))
            return;

        var existingKey = (existing.Ability!.Guid, existing.SourceId);
        if (existingKey == newKey)
            return; // Same spell re-cast; will overwrite naturally

        existing.IsCancelled = true;
        existing.CastEvent = null;
        lastBeginCast.Remove(sourceId);
        pendingCasts.Remove(existingKey);
    }
}

