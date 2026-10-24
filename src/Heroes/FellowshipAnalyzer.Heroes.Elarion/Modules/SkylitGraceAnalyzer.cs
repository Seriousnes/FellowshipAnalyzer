using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

using ElarionTalents = FellowshipAnalyzer.Core.Common.Spells.ElarionTalents;
using Spell = FellowshipAnalyzer.Core.Common.Spells.Spell;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Skylit Grace: while Starfall Volley is active, Skystrider's Grace has +120% Cooldown Acceleration. A
/// Starfall Volley cast opens the window for the volley's 8 seconds. A Skylit Grace buff apply also opens
/// it, and a Skylit Grace buff removal closes it.
/// </summary>
[RequiresTalent(ElarionTalents.SkylitGrace)]
[Before<SpellUsable>]
[Dependency<StatTracker>]
public sealed partial class SkylitGraceAnalyzer : Analyzer
{
    /// <summary>The Cooldown Acceleration term Skystrider's Grace has while Starfall Volley is active.</summary>
    public const double Acceleration = 1.2;

    /// <summary>Starfall Volley's duration, in milliseconds.</summary>
    public const int VolleyDurationMs = 8_000;

    private static readonly CooldownModifier GraceAcceleration =
        new(Acceleration, new Spell[] { Spells.SkystridersGrace });

    private RemoveBuffEvent? _scheduledEnd;
    private int? _openedAt;
    private int _activeMs;

    /// <summary>Milliseconds Skystrider's Grace had the Skylit Grace term across the report.</summary>
    public int ActiveMs => _activeMs;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.StarfallVolley))]
    private void OnVolleyCast(CastEvent e)
    {
        Open(e);
        _scheduledEnd = Owner.EventEmitter.FabricateEvent(new RemoveBuffEvent
        {
            Timestamp = e.Timestamp + VolleyDurationMs,
            SourceId = Owner.PlayerId,
            TargetId = Owner.PlayerId,
            Ability = new Ability
            {
                FSLID = Spells.StarfallVolleyTalentBuffWhileActive.FSLID,
                Name = Spells.StarfallVolleyTalentBuffWhileActive.Name,
            },
        }, e);
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.StarfallVolleyTalentBuffWhileActive))]
    private void OnBuffApplied(ApplyBuffEvent e) => Open(e);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.StarfallVolleyTalentBuffWhileActive))]
    private void OnBuffRemoved(RemoveBuffEvent e)
    {
        if (e.Fabricated == true && !ReferenceEquals(e, _scheduledEnd)) return;

        Close(e);
    }

    private void Open(Event trigger)
    {
        if (_openedAt is not null) return;

        _openedAt = trigger.Timestamp;
        StatTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, GraceAcceleration, trigger);
    }

    private void Close(Event trigger)
    {
        _scheduledEnd = null;
        if (_openedAt is not int openedAt) return;

        _activeMs += trigger.Timestamp - openedAt;
        _openedAt = null;
        StatTracker.RemoveCooldownModifier(CooldownPool.CooldownAcceleration, GraceAcceleration, trigger);
    }
}
