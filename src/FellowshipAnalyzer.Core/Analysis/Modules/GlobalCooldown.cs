using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Fabricates <see cref="GlobalCooldownEvent"/> for every on-GCD cast or channel start,
/// and attaches it to the triggering event's <c>GlobalCooldown</c> property so that
/// Timeline rendering can display GCD bars without re-scanning all events.
/// </summary>
public sealed class GlobalCooldown : Analyzer
{
    private Abilities? _abilities;

    public override void Initialize()
    {
        _abilities = Owner.GetModule<Abilities>();

        AddEventListener(Events.Cast.By(SELECTED_PLAYER), OnCast);
        AddEventListener(Events.BeginChannel.By(SELECTED_PLAYER), OnBeginChannel);
    }

    private void OnCast(CastEvent e)
    {
        var gcdMs = GetGcdDuration(e.AbilityGameId);
        if (gcdMs <= 0) return;

        var gcdEvent = FabricateGcdEvent(e.AbilityGameId, e.Timestamp, gcdMs);
        e.GlobalCooldown = gcdEvent;
    }

    private void OnBeginChannel(BeginChannelEvent e)
    {
        var gcdMs = GetGcdDuration(e.AbilityGameId);
        if (gcdMs <= 0) return;

        var gcdEvent = FabricateGcdEvent(e.AbilityGameId, e.Timestamp, gcdMs);
        e.GlobalCooldown = gcdEvent;
    }

    private int GetGcdDuration(int spellId)
    {
        var ability = _abilities?.GetAbility(spellId);
        if (ability?.Gcd is null) return 0;

        var gcd = ability.Gcd;

        // A static GCD (not affected by haste) takes precedence.
        if (gcd.Static.HasValue)
            return (int)gcd.Static.Value;

        // Base GCD reduced by haste — haste multiplier is not tracked yet, so use 1.0.
        // TODO: integrate with a Haste module when available.
        var baseGcd = gcd.Base ?? 1500;
        var minimumGcd = gcd.Minimum ?? 750;
        var hasteReduced = baseGcd; // base / haste — haste = 1.0 for now
        return (int)Math.Max(minimumGcd, hasteReduced);
    }

    private GlobalCooldownEvent FabricateGcdEvent(int spellId, int timestamp, int durationMs)
    {
        var ability = _abilities?.GetAbility(spellId);
        var gcdEvent = Owner.EventEmitter.FabricateEvent(new GlobalCooldownEvent
        {
            Timestamp = timestamp,
            Duration = durationMs,
            SourceId = Owner.PlayerId,
            TargetId = Owner.PlayerId,
            TargetIsFriendly = true,
            Ability = new Ability
            {
                Guid = spellId,
                Name = ability?.Name ?? string.Empty,
            },
        });
        return gcdEvent;
    }
}
