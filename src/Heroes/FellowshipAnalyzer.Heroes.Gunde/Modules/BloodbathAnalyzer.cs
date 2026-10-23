using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

using GundeTalents = FellowshipAnalyzer.Core.Common.Spells.GundeTalents;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Bloodbath: each Grim Carve grants Bloodbath for 3 seconds, and Reign in Blood has +200% Cooldown
/// Acceleration while it lasts. The Grim Carve cast fabricates a Bloodbath <see cref="ApplyBuffEvent"/> on the
/// player and adds the term to the pool <see cref="StatTracker"/> tracks. A fabricated
/// <see cref="RemoveBuffEvent"/> at the scheduled expiry removes the term. A Grim Carve inside an open window
/// moves the expiry to a full duration from that cast.
/// </summary>
[RequiresTalent(GundeTalents.Bloodbath)]
[Dependency<StatTracker>]
public sealed partial class BloodbathAnalyzer : Analyzer
{
    /// <summary>Reign in Blood's added Cooldown Acceleration while Bloodbath lasts, as a fraction.</summary>
    public const double AddedAcceleration = 2.0;

    /// <summary>Bloodbath's duration, in milliseconds.</summary>
    public const int DurationMs = 3000;

    private static readonly CooldownModifier ReignInBloodAcceleration =
        new(AddedAcceleration, new Core.Common.Spells.Spell[] { Spells.ReignInBlood });

    private readonly List<BloodbathWindow> _windows = [];
    private RemoveBuffEvent? _scheduledRemoval;

    /// <summary>Every Bloodbath window for the selected player.</summary>
    public IReadOnlyList<BloodbathWindow> Windows => _windows;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.GrimCarve))]
    private void OnGrimCarve(CastEvent e)
    {
        if (_scheduledRemoval is null)
        {
            Owner.EventEmitter.FabricateEvent(new ApplyBuffEvent
            {
                Timestamp = e.Timestamp,
                SourceId = e.SourceId,
                TargetId = e.SourceId,
                Ability = BloodbathAbility(),
            }, e);
            StatTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, ReignInBloodAcceleration, e);
            _windows.Add(new BloodbathWindow(e.Timestamp, e.Timestamp + DurationMs));
        }
        else
        {
            _windows[^1] = _windows[^1] with { EndTimestamp = e.Timestamp + DurationMs };
        }

        _scheduledRemoval = Owner.EventEmitter.FabricateEvent(new RemoveBuffEvent
        {
            Timestamp = e.Timestamp + DurationMs,
            SourceId = e.SourceId,
            TargetId = e.SourceId,
            Ability = BloodbathAbility(),
        }, e);
    }

    private static Ability BloodbathAbility() => new()
    {
        Id = Talents.Bloodbath.FSLID,
        Name = Talents.Bloodbath.Name,
        Icon = Talents.Bloodbath.Icon,
    };

    [On<RemoveBuffEvent>(To = Actor.Player)]
    private void OnExpiry(RemoveBuffEvent e)
    {
        if (!ReferenceEquals(e, _scheduledRemoval)) return;

        StatTracker.RemoveCooldownModifier(CooldownPool.CooldownAcceleration, ReignInBloodAcceleration, e);
        _scheduledRemoval = null;
    }
}

/// <summary>One Bloodbath window: the interval Reign in Blood's Cooldown Acceleration was raised.</summary>
public sealed record BloodbathWindow(int BeginTimestamp, int EndTimestamp);
