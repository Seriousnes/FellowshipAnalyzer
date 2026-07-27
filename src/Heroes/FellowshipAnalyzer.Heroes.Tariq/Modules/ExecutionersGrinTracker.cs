using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Tariq.Statistics;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

public sealed partial class ExecutionersGrinTracker : EventSubscriber
{
    private const double ExecuteHealthThreshold = 0.30;

    private bool _procActive;
    private bool _procUsed;

    public int Procs { get; private set; }

    public int UsedProcs { get; private set; }

    public int WastedProcs { get; private set; }

    public int CullingStrikeHits { get; private set; }

    public int AboveExecuteCullingStrikes { get; private set; }

    public override Type? StatisticsComponentType =>
        Procs > 0 || AboveExecuteCullingStrikes > 0 ? typeof(ExecutionersGrinStatistics) : null;

    public override StatisticCategory StatisticCategory => StatisticCategory.Items;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ExecutionersGrin))]
    private void OnGrinApplied(ApplyBuffEvent @event)
    {
        Procs++;
        _procActive = true;
        _procUsed = false;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ExecutionersGrin))]
    private void OnGrinRemoved(RemoveBuffEvent @event)
    {
        if (!_procActive)
            return;

        if (!_procUsed)
            WastedProcs++;
        _procActive = false;
    }

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.CullingStrike))]
    private void OnCullingStrikeDamage(DamageEvent @event)
    {
        var target = @event.TargetResources;
        if (target is null || target.MaxHitPoints <= 0)
            return;

        CullingStrikeHits++;
        if ((double)target.HitPoints / target.MaxHitPoints < ExecuteHealthThreshold)
            return;

        AboveExecuteCullingStrikes++;
        if (_procActive && !_procUsed)
        {
            _procUsed = true;
            UsedProcs++;
        }
    }
}
