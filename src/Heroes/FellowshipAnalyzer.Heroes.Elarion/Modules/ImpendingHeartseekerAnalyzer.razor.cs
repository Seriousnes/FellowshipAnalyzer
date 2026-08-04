using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

using ElarionTalents = FellowshipAnalyzer.Core.Common.Spells.ElarionTalents;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Handles the Impending Heartseeker buff, which resets Heartseeker Barrage. The log carries no
/// cooldown update for that reset, so this module writes it to <see cref="SpellUsable"/> on every
/// grant of the buff; without it the tracker believes Barrage is unavailable for most of its real
/// casts. The recharge remaining immediately before each reset is what that reset recovered, and a
/// grant arriving while Barrage is already available recovered nothing.
/// </summary>
[RequiresTalent(ElarionTalents.ImpendingHeartseeker)]
[Dependency<SpellUsable>]
public sealed partial class ImpendingHeartseekerAnalyzer : EventSubscriber
{
    /// <summary>Impending Heartseeker buffs granted, each one a Heartseeker Barrage reset.</summary>
    public int Resets { get; private set; }

    /// <summary>Resets that landed while Heartseeker Barrage was already available, recovering nothing.</summary>
    public int WastedResets { get; private set; }

    /// <summary>Resets that landed on a running recharge.</summary>
    public int LandedResets => Resets - WastedResets;

    /// <summary>Total Heartseeker Barrage recharge time the resets removed, in milliseconds.</summary>
    public int RecoveredMs { get; private set; }

    /// <summary>Mean recharge time removed per reset that landed on a running recharge, in milliseconds.</summary>
    public double AverageRecoveredMs => LandedResets == 0 ? 0d : RecoveredMs / (double)LandedResets;

    /// <summary>Share (0-1) of resets that recovered nothing.</summary>
    public double WastedShare => Resets == 0 ? 0d : WastedResets / (double)Resets;

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Talents;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ImpendingHeartseeker))]
    private void OnBuffApplied(ApplyBuffEvent e) => ResetBarrage(e.Timestamp);

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ImpendingHeartseeker))]
    private void OnBuffStackApplied(ApplyBuffStackEvent e) => ResetBarrage(e.Timestamp);

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ImpendingHeartseeker))]
    private void OnBuffRefreshed(RefreshBuffEvent e) => ResetBarrage(e.Timestamp);

    private void ResetBarrage(int timestamp)
    {
        var barrageId = Spells.HeartseekerBarrage.Id;
        var remaining = SpellUsable.CooldownRemaining(barrageId, timestamp);

        Resets++;
        if (remaining <= 0)
            WastedResets++;
        else
            RecoveredMs += remaining;

        SpellUsable.EndCooldown(barrageId, timestamp, restoreAllCharges: true);
    }
}
