using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Ardeos.Statistics;

using ArdeosTalents = FellowshipAnalyzer.Core.Common.Spells.ArdeosTalents;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Tracks Reign of Fire (talent 157). Each proc — the "Reign of Fire" buff granted by Detonate —
/// restores a Fire Ball charge and empowers the next Fire Ball. A restore is fabricated through
/// <see cref="SpellUsable"/> so it flows onto the cooldown timeline. Waste is a proc gained while
/// Fire Ball is already at max charges (the restored charge overcaps) or an empowered buff that is
/// removed without a Fire Ball cast (it expired unused).
/// </summary>
[RequiresTalent(ArdeosTalents.ReignOfFire)]
public sealed partial class ReignOfFireAnalyzer : Analyzer
{
    /// <summary>
    /// A buff removal within this many milliseconds of a Fire Ball cast counts as consumed rather
    /// than expired. Kept small because Fire Ball is spammed — a wide window would read a genuine
    /// expiry near an unrelated cast as consumed. Real expiries sit seconds away from any cast.
    /// </summary>
    private const int ConsumeWindowMs = 150;

    private readonly List<int> _fireBallCasts = [];
    private readonly List<int> _procRemovals = [];
    private int _procs;
    private int _chargesWasted;

    public override Type? StatisticsComponentType => typeof(ReignOfFireStatistics);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.FireBall))]
    private void OnFireBallCast(CastEvent e) => _fireBallCasts.Add(e.Timestamp);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ReignOfFireBuff))]
    private void OnProc(ApplyBuffEvent e) => HandleProc(e.Timestamp);

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ReignOfFireBuff))]
    private void OnProcStack(ApplyBuffStackEvent e) => HandleProc(e.Timestamp);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ReignOfFireBuff))]
    private void OnProcRemoved(RemoveBuffEvent e) => _procRemovals.Add(e.Timestamp);

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ReignOfFireBuff))]
    private void OnProcStackRemoved(RemoveBuffStackEvent e) => _procRemovals.Add(e.Timestamp);

    private void HandleProc(int timestamp)
    {
        _procs++;
        var spellUsable = Owner.SpellUsable!;
        if (spellUsable.ChargesAvailable(Spells.FireBall.Id) >= Spells.FireBall.Charges)
            _chargesWasted++;
        else
            spellUsable.EndCooldown(Spells.FireBall.Id, timestamp);
    }

    /// <summary>Projects the encounter's Reign of Fire waste.</summary>
    public ReignOfFireReport ToReport()
    {
        var empowermentsWasted = _procRemovals.Count(removal =>
            !_fireBallCasts.Any(cast => Math.Abs(cast - removal) <= ConsumeWindowMs));
        return new ReignOfFireReport(_procs, _chargesWasted, empowermentsWasted);
    }
}

/// <summary>
/// Projection of <see cref="ReignOfFireAnalyzer"/> waste over the encounter.
/// </summary>
/// <param name="Procs">Reign of Fire procs granted.</param>
/// <param name="ChargesWasted">Procs gained while Fire Ball was already at max charges (the restored charge overcapped).</param>
/// <param name="EmpowermentsWasted">Empowered Fire Ball buffs removed without a Fire Ball cast (expired unused).</param>
public sealed record ReignOfFireReport(int Procs, int ChargesWasted, int EmpowermentsWasted);
