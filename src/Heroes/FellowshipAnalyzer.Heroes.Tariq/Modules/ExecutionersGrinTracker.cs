using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Tariq.Statistics;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

/// <summary>
/// Tracks Executioner's Grin, the proc granted by the legendary Executioner's Unsanitary Bands
/// (item 5225). Culling Strike is only castable on targets below 30% health; while Executioner's
/// Grin is active it is castable at any health, so each proc is one opportunity to land a Culling
/// Strike above the execute threshold, and a proc that expires without such a hit is a wasted
/// opportunity. Culling Strikes landing above the threshold are also counted independently of the
/// proc buff - such a hit is only possible with the item, so the count evidences item usage even
/// in logs that do not carry the proc as a discrete buff event.
/// </summary>
public sealed partial class ExecutionersGrinTracker : EventSubscriber
{
    private const double ExecuteHealthThreshold = 0.30;

    private bool _procActive;
    private bool _procUsed;

    /// <summary>Executioner's Grin buff applications observed on the player.</summary>
    public int Procs { get; private set; }

    /// <summary>Procs during which a Culling Strike landed above the execute threshold.</summary>
    public int UsedProcs { get; private set; }

    /// <summary>Procs that expired without a Culling Strike above the execute threshold.</summary>
    public int WastedProcs { get; private set; }

    /// <summary>All Culling Strike hits with target health information.</summary>
    public int CullingStrikeHits { get; private set; }

    /// <summary>Culling Strikes that landed on a target at or above the execute threshold.</summary>
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
