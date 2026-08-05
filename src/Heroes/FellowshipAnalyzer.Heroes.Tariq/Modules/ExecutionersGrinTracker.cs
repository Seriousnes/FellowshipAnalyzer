using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

public sealed partial class ExecutionersGrinTracker : Analyzer
{
    private bool _held;
    private int? _unclaimedRemoval;

    public bool Equipped => Owner.SelectedCombatant.HasItem(Items.ExecutionersUnsanitaryBands.Id);

    public int Procs { get; private set; }

    public int Reapplications { get; private set; }

    public int SpentAboveThreshold { get; private set; }

    public int SpentBelowThreshold { get; private set; }

    public int ExpiredUnspent { get; private set; }

    public int CullingStrikeHits { get; private set; }

    public int AboveExecuteCullingStrikes { get; private set; }

    public override StatisticCategory StatisticCategory => StatisticCategory.Items;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ExecutionersGrin))]
    private void OnGrinApplied(ApplyBuffEvent @event)
    {
        Procs++;
        _held = true;
        _unclaimedRemoval = null;
    }

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ExecutionersGrin))]
    private void OnGrinReapplied(RefreshBuffEvent @event)
    {
        Reapplications++;
        _held = true;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ExecutionersGrin))]
    private void OnGrinRemoved(RemoveBuffEvent @event)
    {
        if (!_held)
            return;

        ExpiredUnspent++;
        _unclaimedRemoval = @event.Timestamp;
        _held = false;
    }

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.CullingStrike))]
    private void OnCullingStrikeDamage(DamageEvent @event)
    {
        var target = @event.TargetResources;
        if (target is null || target.MaxHitPoints <= 0 || target.HitPoints > target.MaxHitPoints)
            return;

        CullingStrikeHits++;

        var aboveThreshold = (double)target.HitPoints / target.MaxHitPoints > CullingStrikeAnalyzer.ExecuteHealthThreshold;
        if (aboveThreshold)
            AboveExecuteCullingStrikes++;

        if (!Consume(@event.Timestamp))
            return;

        if (aboveThreshold)
            SpentAboveThreshold++;
        else
            SpentBelowThreshold++;
    }

    private bool Consume(int timestamp)
    {
        if (_held)
        {
            _held = false;
            _unclaimedRemoval = null;
            return true;
        }

        if (_unclaimedRemoval != timestamp)
            return false;

        ExpiredUnspent--;
        _unclaimedRemoval = null;
        return true;
    }
}
