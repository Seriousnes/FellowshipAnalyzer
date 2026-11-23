using FellowshipAnalyzer.Core.Contracts.Design;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Fabricates <see cref="GlobalCooldownEvent"/> for every on-GCD hard cast start, instant cast or channel start,
/// and attaches it to the triggering event's <c>GlobalCooldown</c> property so that
/// Timeline rendering can display GCD bars without re-scanning all events.
/// </summary>
public sealed partial class GlobalCooldown(Lazy<Abilities> abilities, Lazy<DebugAnnotations> debugAnnotations, Lazy<Haste> haste) : Analyzer
{
    private int _lastGcdEnd;

    [On<BeginCastEvent>(By = Actor.Player)]
    private void OnBeginCast(BeginCastEvent e)
    {
        if (e.Channel is not null) return;

        var gcdMs = GetGcdDuration(e.Ability.Id);
        if (gcdMs <= 0) return;

        if (e.Timestamp < _lastGcdEnd)
        {
            var overlapMs = _lastGcdEnd - e.Timestamp;
            _debugAnnotations.AddAnnotation(this, e, new DebugAnnotation(
                Color: FaVar.Amber,
                Summary: $"Cast start during active GCD ({overlapMs}ms overlap)",
                Details: $"The GCD was still active for {overlapMs}ms when this cast start was registered."));
        }

        var gcdEvent = FabricateGcdEvent(e.Ability.Id, e.Timestamp, gcdMs);
        e.GlobalCooldown = gcdEvent;
        e.CastEvent?.GlobalCooldown = gcdEvent;
        _lastGcdEnd = e.Timestamp + gcdMs;
    }

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent e)
    {
        if (e.Channel is not null || e.GlobalCooldown is not null) return;

        var gcdMs = GetGcdDuration(e.Ability.Id);
        if (gcdMs <= 0) return;

        if (e.Timestamp < _lastGcdEnd)
        {
            var overlapMs = _lastGcdEnd - e.Timestamp;
            _debugAnnotations.AddAnnotation(this, e, new DebugAnnotation(
                Color: FaVar.Amber,
                Summary: $"Cast during active GCD ({overlapMs}ms overlap)",
                Details: $"The GCD was still active for {overlapMs}ms when this cast was registered. " +
                         "This may indicate a log timing issue or a spell that bypasses the GCD."));
        }

        var gcdEvent = FabricateGcdEvent(e.Ability.Id, e.Timestamp, gcdMs);
        e.GlobalCooldown = gcdEvent;
        _lastGcdEnd = e.Timestamp + gcdMs;
    }

    [On<BeginChannelEvent>(By = Actor.Player)]
    private void OnBeginChannel(BeginChannelEvent e)
    {
        var gcdMs = GetGcdDuration(e.Ability.Id);
        if (gcdMs <= 0) return;

        if (e.Timestamp < _lastGcdEnd)
        {
            var overlapMs = _lastGcdEnd - e.Timestamp;
            _debugAnnotations.AddAnnotation(this, e, new DebugAnnotation(
                Color: FaVar.Amber,
                Summary: $"Channel start during active GCD ({overlapMs}ms overlap)",
                Details: $"The GCD was still active for {overlapMs}ms when this channel start was registered."));
        }

        var gcdEvent = FabricateGcdEvent(e.Ability.Id, e.Timestamp, gcdMs);
        e.GlobalCooldown = gcdEvent;
        _lastGcdEnd = e.Timestamp + gcdMs;
    }

    /// <summary>
    /// The global cooldown in milliseconds that a cast of <paramref name="spellId"/> starts at the current Haste,
    /// or 0 when the spell has none.
    /// </summary>
    /// <param name="spellId">Combat-log spell id.</param>
    /// <exception cref="InvalidOperationException">The entry's <see cref="GcdInfo"/> has neither <see cref="GcdInfo.Static"/> nor <see cref="GcdInfo.Base"/>.</exception>
    public int GetGcdDuration(int spellId)
    {
        var ability = _abilities.GetAbility(spellId);
        if (ability is null) return 0;

        if (ability.Gcd is { } gcd)
        {
            var combatant = Owner.SelectedCombatant;

            double Resolve(GcdValue v) => v.Match(value => value, func => func(combatant));

            if (gcd.Static is not null)
                return (int)Resolve(gcd.Static);

            if (gcd.Base is null)
                throw new InvalidOperationException($"The GcdInfo of {ability.SpellFor(spellId).Name} has neither Static nor Base.");

            var minimumGcd = gcd.Minimum is not null ? Resolve(gcd.Minimum) : 0;
            var hasteReduced = Resolve(gcd.Base) / (1 + _haste.Current);
            return (int)Math.Round(Math.Max(minimumGcd, hasteReduced), MidpointRounding.AwayFromZero);
        }

        var spell = ability.SpellFor(spellId);
        if (spell.GlobalCooldown is not { } seconds) return 0;

        var milliseconds = seconds * 1000;
        if (spell.GlobalCooldownScalesWithHaste == true)
            milliseconds /= 1 + _haste.Current;

        return (int)Math.Round(milliseconds, MidpointRounding.AwayFromZero);
    }

    private GlobalCooldownEvent FabricateGcdEvent(int spellId, int timestamp, int durationMs)
    {
        var ability = _abilities.GetAbility(spellId);
        var gcdEvent = Owner.EventEmitter.FabricateEvent(new GlobalCooldownEvent
        {
            Timestamp = timestamp,
            Duration = durationMs,
            SourceId = Owner.PlayerId,
            TargetId = Owner.PlayerId,
            TargetIsFriendly = true,
            Ability = new Ability
            {
                FSLID = spellId,
                Name = ability?.Name ?? string.Empty,
            },
        });
        return gcdEvent;
    }
}
