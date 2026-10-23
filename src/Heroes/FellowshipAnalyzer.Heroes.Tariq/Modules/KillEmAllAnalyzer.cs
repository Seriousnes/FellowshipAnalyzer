using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Tariq.Spells;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

/// <summary>
/// Cooldown Acceleration Kill Em All gives Heavy Strike: <see cref="HeavyStrikeAcceleration"/> on
/// <see cref="StatTracker"/>'s pool while the Kill Em All effect is on the player. The term is flat. A
/// stack Heavy Strike consumes leaves it in place, and only the effect's removal revokes it.
/// </summary>
[RequiresTalent(TariqTalents.KillEmAll)]
[Dependency<StatTracker>]
[Before<SpellUsable>]
public sealed partial class KillEmAllAnalyzer : Analyzer
{
    /// <summary>Cooldown Acceleration on Heavy Strike while Kill Em All is active, as a fraction.</summary>
    public const double HeavyStrikeAcceleration = 0.60;

    /// <summary>The Kill Em All effect the talent applies to the player.</summary>
    public static readonly Effect KillEmAll = new() { Id = 3160, Name = "Kill Em All", Icon = "Berserker6" };

    private static readonly CooldownModifier Acceleration =
        new(HeavyStrikeAcceleration, new Spell[] { Spells.HeavyStrike });

    private readonly List<AuraWindow> _windows = [];

    private int? _openedAt;

    /// <summary>Every window Kill Em All was active on the player, in the order they opened.</summary>
    public IReadOnlyList<AuraWindow> Windows =>
        _openedAt is { } start ? [.. _windows, new AuraWindow(start, Math.Max(start, Owner.DungeonEndTime))] : _windows;

    [On<ApplyBuffEvent>(To = Actor.Player)]
    private void OnApplied(ApplyBuffEvent e) => Grant(e);

    [On<ApplyBuffStackEvent>(To = Actor.Player)]
    private void OnStackApplied(ApplyBuffStackEvent e) => Grant(e);

    [On<RefreshBuffEvent>(To = Actor.Player)]
    private void OnRefreshed(RefreshBuffEvent e) => Grant(e);

    [On<RemoveBuffEvent>(To = Actor.Player)]
    private void OnRemoved(RemoveBuffEvent e)
    {
        if (!IsKillEmAll(e) || _openedAt is not { } start) return;

        StatTracker.RemoveCooldownModifier(CooldownPool.CooldownAcceleration, Acceleration, e, e.Timestamp);
        _windows.Add(new AuraWindow(start, Math.Max(start, e.Timestamp)));
        _openedAt = null;
    }

    private void Grant(BuffEvent e)
    {
        if (!IsKillEmAll(e) || _openedAt is not null) return;

        StatTracker.AddCooldownModifier(CooldownPool.CooldownAcceleration, Acceleration, e, e.Timestamp);
        _openedAt = e.Timestamp;
    }

    private static bool IsKillEmAll(BuffEvent e) => e.Ability.FSLID == KillEmAll.FSLID;
}
