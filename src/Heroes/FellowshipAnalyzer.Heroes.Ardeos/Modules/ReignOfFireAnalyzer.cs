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

    /// <summary>Reign of Fire procs granted this encounter.</summary>
    public int Procs { get; private set; }

    /// <summary>Procs gained while Fire Ball was already at max charges (the restored charge overcapped).</summary>
    public int ChargesWasted { get; private set; }

    /// <summary>Empowered Fire Ball buffs removed without a Fire Ball cast (expired unused).</summary>
    public int EmpowermentsWasted => _procRemovals.Count(removal =>
        !_fireBallCasts.Any(cast => Math.Abs(cast - removal) <= ConsumeWindowMs));

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
        Procs++;
        var spellUsable = Owner.SpellUsable!;
        if (spellUsable.ChargesAvailable(Spells.FireBall.Id) >= Spells.FireBall.Charges)
            ChargesWasted++;
        else
            spellUsable.EndCooldown(Spells.FireBall.Id, timestamp);
    }
}
