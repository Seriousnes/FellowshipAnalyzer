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
    private const int MaxChannelCastWindowMs = 200;

    public int Priority => 0;

    public List<Event> Normalize(List<Event> events, int playerId)
    {
        var castableWhileCasting = abilities?.Spellbook()
            .Where(a => a.CastableWhileCasting)
            .Select(a => a.PrimarySpell.FSLID)
            .ToHashSet() ?? [];

        if (abilities is not null)
        {
            foreach (var a in abilities.Spellbook())
            {
                if (a.CastableWhileCasting && a.AdditionalSpells is not null)
                    foreach (var spell in a.AdditionalSpells)
                        castableWhileCasting.Add(spell.FSLID);
            }
        }

        var pendingCasts = new Dictionary<(int abilityId, int sourceId), BeginCastEvent>();
        var pendingChannels = new Dictionary<(int abilityId, int sourceId), BeginChannelEvent>();

        var lastBeginCast = new Dictionary<int, BeginCastEvent>();

        var pendingChannelCasts = new Dictionary<(int abilityId, int sourceId), (CastEvent Cast, BeginCastEvent? BeginCast, int Timestamp)>();
        var pendingCastForEndChannel = new Dictionary<(int abilityId, int sourceId), CastEvent>();

        var duplicateActivations = new HashSet<(int timestamp, int abilityGuid, int sourceId)>();
        foreach (var e in events)
        {
            if (e is CastEvent { Activation: false, Fake: false } cast && cast.Ability is not null)
                duplicateActivations.Add((cast.Timestamp, cast.Ability.FSLID, cast.SourceId));
        }

        var fixedEvents = new List<Event>();

        foreach (var e in events)
        {
            if (e is CastEvent { Activation: true } activationCast)
            {
                if (activationCast.Fake)
                    continue;

                if (activationCast.Ability is not null &&
                    duplicateActivations.Contains((activationCast.Timestamp, activationCast.Ability.FSLID, activationCast.SourceId)))
                    continue;
            }

            fixedEvents.Add(e);

            switch (e)
            {
                case BeginCastEvent bc when bc.Ability is not null:
                    {
                        var key = (bc.Ability.FSLID, bc.SourceId);
                        CancelPending(lastBeginCast, bc.SourceId, key, pendingCasts);
                        pendingCasts[key] = bc;
                        lastBeginCast[bc.SourceId] = bc;
                        break;
                    }

                case CastEvent cast when cast.Ability is not null:
                    {
                        var castKey = (cast.Ability.FSLID, cast.SourceId);

                        if (lastBeginCast.TryGetValue(cast.SourceId, out var pending)
                            && pending.Ability?.FSLID != cast.Ability.FSLID)
                        {
                            if (!castableWhileCasting.Contains(cast.Ability.FSLID))
                            {
                                pending.IsCancelled = true;
                                pending.CastEvent = null;
                                lastBeginCast.Remove(cast.SourceId);
                                pendingCasts.Remove((pending.Ability!.FSLID, pending.SourceId));
                            }
                        }

                        BeginCastEvent? linkedBeginCast = null;
                        if (pendingCasts.TryGetValue(castKey, out var beginCast))
                        {
                            beginCast.CastEvent = cast;
                            linkedBeginCast = beginCast;
                            pendingCasts.Remove(castKey);
                            lastBeginCast.Remove(cast.SourceId);
                        }

                        pendingChannelCasts[castKey] = (cast, linkedBeginCast, cast.Timestamp);
                        break;
                    }

                case BeginChannelEvent beginChannel when beginChannel.Ability is not null:
                    {
                        var channelKey = (beginChannel.Ability.FSLID, beginChannel.SourceId);
                        pendingChannels[channelKey] = beginChannel;

                        if (pendingChannelCasts.TryGetValue(channelKey, out var castInfo)
                            && beginChannel.Timestamp - castInfo.Timestamp <= MaxChannelCastWindowMs)
                        {
                            castInfo.BeginCast?.Channel = beginChannel;
                            pendingCastForEndChannel[channelKey] = castInfo.Cast;
                            pendingChannelCasts.Remove(channelKey);
                        }
                        break;
                    }

                case EndChannelEvent ec when ec.Ability is not null:
                    var ecKey = (ec.Ability.FSLID, ec.SourceId);
                    if (pendingChannels.TryGetValue(ecKey, out var beginChannel2))
                    {
                        ec.BeginChannel = beginChannel2;
                        beginChannel2.EndChannel = ec;
                        pendingChannels.Remove(ecKey);
                    }
                    
                    if (pendingCastForEndChannel.TryGetValue(ecKey, out var channelCast))
                    {
                        channelCast.Channel = ec;
                        pendingCastForEndChannel.Remove(ecKey);
                    }
                    break;
            }
        }

        foreach (var bc in lastBeginCast.Values)
        {
            bc.IsCancelled = true;
            bc.CastEvent = null;
        }

        return fixedEvents;
    }

    private static void CancelPending(
        Dictionary<int, BeginCastEvent> lastBeginCast,
        int sourceId,
        (int abilityId, int sourceId) newKey,
        Dictionary<(int, int), BeginCastEvent> pendingCasts)
    {
        if (!lastBeginCast.TryGetValue(sourceId, out var existing))
            return;

        var existingKey = (existing.Ability!.FSLID, existing.SourceId);
        if (existingKey == newKey)
            return;

        existing.IsCancelled = true;
        existing.CastEvent = null;
        lastBeginCast.Remove(sourceId);
        pendingCasts.Remove(existingKey);
    }
}

