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
    private DebugAnnotations? _debugAnnotations;
    private Haste? _haste;
    private int _lastGcdEnd;

    public override void Initialize()
    {
        _abilities = Owner.GetModule<Abilities>();
        _debugAnnotations = Owner.GetModule<DebugAnnotations>();
        _haste = Owner.GetModule<Haste>();
        _lastGcdEnd = 0;

        AddEventListener(Events.Cast.By(SELECTED_PLAYER), OnCast);
        AddEventListener(Events.BeginChannel.By(SELECTED_PLAYER), OnBeginChannel);
    }

    private void OnCast(CastEvent e)
    {
        // Channel casts: the GCD is owned by the BeginChannelEvent that immediately follows.
        // Emitting a GCD here would produce a duplicate; skip it.
        if (e.Channel is not null) return;

        var gcdMs = GetGcdDuration(e.Ability.Id);
        if (gcdMs <= 0) return;

        if (e.Timestamp < _lastGcdEnd)
        {
            var overlapMs = _lastGcdEnd - e.Timestamp;
            _debugAnnotations?.AddAnnotation(this, e, new DebugAnnotation(
                Color: "#f1c40f",
                Summary: $"Cast during active GCD ({overlapMs}ms overlap)",
                Details: $"The GCD was still active for {overlapMs}ms when this cast was registered. " +
                         "This may indicate a log timing issue or a spell that bypasses the GCD."));
        }

        var gcdEvent = FabricateGcdEvent(e.Ability.Id, e.Timestamp, gcdMs);
        e.GlobalCooldown = gcdEvent;
        _lastGcdEnd = e.Timestamp + gcdMs;
    }

    private void OnBeginChannel(BeginChannelEvent e)
    {
        var gcdMs = GetGcdDuration(e.Ability.Id);
        if (gcdMs <= 0) return;

        if (e.Timestamp < _lastGcdEnd)
        {
            var overlapMs = _lastGcdEnd - e.Timestamp;
            _debugAnnotations?.AddAnnotation(this, e, new DebugAnnotation(
                Color: "#f1c40f",
                Summary: $"Channel start during active GCD ({overlapMs}ms overlap)",
                Details: $"The GCD was still active for {overlapMs}ms when this channel start was registered."));
        }

        var gcdEvent = FabricateGcdEvent(e.Ability.Id, e.Timestamp, gcdMs);
        e.GlobalCooldown = gcdEvent;
        _lastGcdEnd = e.Timestamp + gcdMs;
    }

    private int GetGcdDuration(int spellId)
    {
        var ability = _abilities?.GetAbility(spellId);
        if (ability?.Gcd is null) return 0;

        var gcd = ability.Gcd;
        var combatant = Owner.SelectedCombatant;

        double Resolve(GcdValue v) => v.Match(value => value, func => func(combatant));

        // A static GCD (not affected by haste) takes precedence.
        if (gcd.Static is not null)
            return (int)Resolve(gcd.Static);

        // Base GCD scaled by current haste: effective = base × 100 / (100 + hastePercent)
        var baseGcd = gcd.Base is not null ? Resolve(gcd.Base) : 1500;
        var minimumGcd = gcd.Minimum is not null ? Resolve(gcd.Minimum) : 750;
        var hastePercent = (_haste?.Current ?? 0.0) * 100.0;
        var hasteReduced = baseGcd * 100.0 / (100.0 + hastePercent);
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
