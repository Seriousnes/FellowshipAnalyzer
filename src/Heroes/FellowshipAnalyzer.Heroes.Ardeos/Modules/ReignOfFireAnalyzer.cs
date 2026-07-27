using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Ardeos.Statistics;

using ArdeosTalents = FellowshipAnalyzer.Core.Common.Spells.ArdeosTalents;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

[RequiresTalent(ArdeosTalents.ReignOfFire)]
public sealed partial class ReignOfFireAnalyzer : Analyzer
{
    private const int ConsumeWindowMs = 150;

    private readonly List<int> _fireBallCasts = [];
    private readonly List<int> _procRemovals = [];

    public int Procs { get; private set; }

    public int ChargesWasted { get; private set; }

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
