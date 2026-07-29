using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Helena.Statistics;

using HelenaTalents = FellowshipAnalyzer.Core.Common.Spells.HelenaTalents;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>
/// Measures Sword &amp; Board, whose proc makes the next Shield Slam free. The charge it hands back goes
/// through <see cref="SpellUsable.RefundCharge"/> rather than a reset, so the charge that was already
/// recharging keeps the progress it had made - a refund advances the charge count, it does not restart
/// the clock.
/// <para>
/// The damage a free Shield Slam deals is damage the rotation would not otherwise have had, so it is
/// attributed here: every hit landed between a free cast and the next Shield Slam counts towards it,
/// cleave included. A proc that falls off before a Shield Slam is pressed under it is never collected.
/// </para>
/// <para>
/// The game emits the proc under two ids - the talent's own and the ability-rank tier granting the same
/// free cast - and both are watched. No log held locally has a player running the talent, so the refund
/// is modelled from the Season 3 constants and has not been checked against live data.
/// </para>
/// </summary>
[RequiresTalent(HelenaTalents.SwordAndBoard)]
[Dependency<SpellUsable>]
public sealed partial class SwordAndBoardAnalyzer : Analyzer
{
    private readonly HashSet<int> _activeProcs = [];

    private bool _inFreeCastWindow;

    /// <inheritdoc/>
    public override Type? StatisticsComponentType => Procs > 0 ? typeof(SwordAndBoardStatistics) : null;

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Talents;

    /// <summary>Procs seen this fight, counting a refresh landing on a live proc as a fresh one.</summary>
    public int Procs { get; private set; }

    /// <summary>Free Shield Slams the proc bought, each handing a charge back.</summary>
    public int FreeCasts { get; private set; }

    /// <summary>Procs that fell off before a Shield Slam was pressed under them.</summary>
    public int ProcsExpired { get; private set; }

    /// <summary>Damage dealt by the free Shield Slams, cleave included.</summary>
    public long ExtraDamage { get; private set; }

    /// <summary>Share (0-1) of procs that bought an extra Shield Slam.</summary>
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
