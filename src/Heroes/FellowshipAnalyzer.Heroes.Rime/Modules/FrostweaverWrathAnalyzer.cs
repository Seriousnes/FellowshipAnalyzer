using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Rime.Statistics;

using RimeTalents = FellowshipAnalyzer.Core.Common.Spells.RimeTalents;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/// <summary>
/// Tracks Frostweaver's Wrath. Gaining a Winter Orb has a 20% chance to bank a proc that makes the
/// next Winter Orb spender a guaranteed critical strike, so a proc is worth exactly one spender and
/// is lost any other way it leaves. The counters separate the two ways it can be lost: it expires
/// when the buff falls off unspent, and it is overwritten when a fresh proc rolls on top of one
/// still banked.
/// </summary>
/// <remarks>
/// The proc is a one-shot state, not a stack, so the machine holds only whether a proc stands and
/// whether it has been spent. The spend is read from the spender rather than from the buff removal,
/// because the removal alone cannot say which of the two exits it was.
/// <para>
/// A spender spends the proc on its cast or on its damage, whichever lands first while the proc
/// stands. The game takes the proc when the spender resolves, so a projectile already in flight when
/// the proc rolls consumes it on impact with no cast inside the buff at all; counting casts alone
/// reads those as expiries. For the same reason the removal does not settle the proc where it lands.
/// The consuming hit and the removal it causes share a millisecond and the log does not consistently
/// order the hit first, so a removal only arms a close, which settles once the timeline moves past
/// that millisecond or the fight ends.
/// </para>
/// A proc still banked when the fight ends counts as gained and stays out of both loss counters, so
/// it depresses <see cref="UtilisationShare"/> without being called a mistake.
/// </remarks>
[RequiresTalent(RimeTalents.FrostweaversWrath)]
public sealed partial class FrostweaverWrathAnalyzer : EventSubscriber
{
    private bool _procBanked;
    private bool _procSpent;
    private int? _closesAt;

    /// <summary>Procs banked this fight, counting each fresh roll whether or not one was already standing.</summary>
    public int ProcsGained { get; private set; }

    /// <summary>Procs spent on a Winter Orb spender.</summary>
    public int ProcsConsumed { get; private set; }

    /// <summary>Procs whose buff fell off without a spender taking them.</summary>
    public int ProcsExpired { get; private set; }

    /// <summary>Procs replaced by a fresh roll while still banked and unspent.</summary>
    public int ProcsOverwritten { get; private set; }

    /// <summary>The share of banked procs that reached a spender.</summary>
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

    /// <summary>
    /// Settles a proc whose removal is already armed. Nothing having spent it by now means the
    /// millisecond the removal landed on carried no spender, so the proc expired.
    /// </summary>
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
