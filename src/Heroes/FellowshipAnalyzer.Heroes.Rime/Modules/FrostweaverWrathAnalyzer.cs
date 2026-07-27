using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Rime.Statistics;

using RimeTalents = FellowshipAnalyzer.Core.Common.Spells.RimeTalents;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

[RequiresTalent(RimeTalents.FrostweaversWrath)]
public sealed partial class FrostweaverWrathAnalyzer : EventSubscriber
{
    private bool _procBanked;
    private bool _procSpent;
    private int? _closesAt;

    public int ProcsGained { get; private set; }

    public int ProcsConsumed { get; private set; }

    public int ProcsExpired { get; private set; }

    public int ProcsOverwritten { get; private set; }

    public double UtilisationShare => ProcsGained > 0 ? (double)ProcsConsumed / ProcsGained : 0;

    public override Type? StatisticsComponentType =>
        ProcsGained > 0 ? typeof(FrostweaversWrathStatistics) : null;

    public override StatisticCategory StatisticCategory => StatisticCategory.Talents;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.FrostweaversWrathBuff))]
    private void OnProcApplied(ApplyBuffEvent applyBuffEvent) => BankProc();

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.FrostweaversWrathBuff))]
    private void OnProcRefreshed(RefreshBuffEvent refreshBuffEvent) => BankProc();

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.FrostweaversWrathBuff))]
    private void OnProcRemoved(RemoveBuffEvent removeBuffEvent)
    {
        if (_procBanked)
            _closesAt = removeBuffEvent.Timestamp;
    }

    [On<CastEvent>(By = Actor.Player, Spells = [
        nameof(Spells.GlacialBlast),
        nameof(Spells.IceComet),
        nameof(Spells.TalonStrike),
        nameof(Spells.RisingTalons)])]
    private void OnSpenderCast(CastEvent castEvent) => SpendProc(castEvent.Timestamp);

    [On<DamageEvent>(By = Actor.Player, Spells = [
        nameof(Spells.GlacialBlast),
        nameof(Spells.IceComet),
        nameof(Spells.TalonStrike),
        nameof(Spells.RisingTalons)])]
    private void OnSpenderDamage(DamageEvent damageEvent) => SpendProc(damageEvent.Timestamp);

    [On<FightEndEvent>]
    private void OnFightEnd(FightEndEvent fightEndEvent) => CloseArmedRemoval();

    private void SpendProc(int timestamp)
    {
        if (_closesAt is { } closesAt && timestamp > closesAt)
            CloseArmedRemoval();

        if (!_procBanked || _procSpent)
            return;

        _procSpent = true;
        ProcsConsumed++;
    }

    private void BankProc()
    {
        CloseArmedRemoval();

        if (_procBanked && !_procSpent)
            ProcsOverwritten++;

        ProcsGained++;
        _procBanked = true;
        _procSpent = false;
    }

    private void CloseArmedRemoval()
    {
        if (_closesAt is null)
            return;

        if (_procBanked && !_procSpent)
            ProcsExpired++;

        _procBanked = false;
        _procSpent = false;
        _closesAt = null;
    }
}
