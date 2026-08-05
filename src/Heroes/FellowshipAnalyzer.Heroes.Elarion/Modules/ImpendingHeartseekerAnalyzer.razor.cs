using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

using ElarionTalents = FellowshipAnalyzer.Core.Common.Spells.ElarionTalents;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

[RequiresTalent(ElarionTalents.ImpendingHeartseeker)]
[Dependency<SpellUsable>]
public sealed partial class ImpendingHeartseekerAnalyzer : EventSubscriber
{
    public int Resets { get; private set; }

    public int WastedResets { get; private set; }

    public int LandedResets => Resets - WastedResets;

    public int RecoveredMs { get; private set; }

    public double AverageRecoveredMs => LandedResets == 0 ? 0d : RecoveredMs / (double)LandedResets;

    public double WastedShare => Resets == 0 ? 0d : WastedResets / (double)Resets;

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
