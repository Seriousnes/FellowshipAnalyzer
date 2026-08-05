using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

using HelenaTalents = FellowshipAnalyzer.Core.Common.Spells.HelenaTalents;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

[RequiresTalent(HelenaTalents.SwordAndBoard)]
[Dependency<SpellUsable>]
public sealed partial class SwordAndBoardAnalyzer : Analyzer
{
    private readonly HashSet<int> _activeProcs = [];

    private bool _inFreeCastWindow;

    public override StatisticCategory StatisticCategory => StatisticCategory.Talents;

    public int Procs { get; private set; }

    public int FreeCasts { get; private set; }

    public int ProcsExpired { get; private set; }

    public long ExtraDamage { get; private set; }

    public double Efficiency => Procs > 0 ? (double)FreeCasts / Procs : 0;

    [On<ApplyBuffEvent>(To = Actor.Player, Spells = [
        nameof(Spells.SwordAndBoardBuff),
        nameof(Spells.SwordAndBoardFreeCastBuff)])]
    private void OnProcApplied(ApplyBuffEvent buffEvent)
    {
        _activeProcs.Add(buffEvent.Ability.Id);
        Procs++;
    }

    [On<RefreshBuffEvent>(To = Actor.Player, Spells = [
        nameof(Spells.SwordAndBoardBuff),
        nameof(Spells.SwordAndBoardFreeCastBuff)])]
    private void OnProcRefreshed(RefreshBuffEvent buffEvent)
    {
        if (!_activeProcs.Add(buffEvent.Ability.Id)) ProcsExpired++;
        Procs++;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spells = [
        nameof(Spells.SwordAndBoardBuff),
        nameof(Spells.SwordAndBoardFreeCastBuff)])]
    private void OnProcRemoved(RemoveBuffEvent buffEvent)
    {
        if (_activeProcs.Remove(buffEvent.Ability.Id)) ProcsExpired++;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.ShieldSlam))]
    private void OnShieldSlam(CastEvent castEvent)
    {
        _inFreeCastWindow = _activeProcs.Count > 0;
        if (!_inFreeCastWindow) return;

        _activeProcs.Clear();
        SpellUsable.RefundCharge(Spells.ShieldSlam.FSLID, castEvent.Timestamp);
        FreeCasts++;
    }

    [On<DamageEvent>(By = Actor.Player, Spells = [
        nameof(Spells.ShieldSlamDamage),
        nameof(Spells.ShieldSlamCleaveDamage)])]
    private void OnShieldSlamDamage(DamageEvent damageEvent)
    {
        if (_inFreeCastWindow) ExtraDamage += damageEvent.Amount;
    }
}
