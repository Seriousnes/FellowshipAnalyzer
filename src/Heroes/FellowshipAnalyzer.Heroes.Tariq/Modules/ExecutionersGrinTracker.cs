using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

/// <summary>
/// Executioner's Grin procs and the Culling Strikes that spent them. The buff holds up to two stacks.
/// The next Culling Strike spends one stack. Every stack held when the buff is removed expired unspent.
/// </summary>
public sealed partial class ExecutionersGrinTracker : Analyzer
{
    private int _held;
    private int? _unclaimedRemoval;

    public bool Equipped => Owner.SelectedCombatant.HasItem(Legendaries.ExecutionersGrin.Id);

    public int Procs { get; private set; }

    public int Reapplications { get; private set; }

    public int SpentAboveThreshold { get; private set; }

    public int SpentBelowThreshold { get; private set; }

    public int ExpiredUnspent { get; private set; }

    public int CullingStrikeHits { get; private set; }

    public int AboveExecuteCullingStrikes { get; private set; }

    public override StatisticCategory StatisticCategory => StatisticCategory.Items;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ExecutionersGrin))]
    private void OnGrinApplied()
    {
        Procs++;
        _held = 1;
        _unclaimedRemoval = null;
    }

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ExecutionersGrin))]
    private void OnGrinStacked()
    {
        Procs++;
        _held++;
        _unclaimedRemoval = null;
    }

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ExecutionersGrin))]
    private void OnGrinReapplied()
    {
        Reapplications++;
        _held = Math.Max(_held, 1);
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ExecutionersGrin))]
    private void OnGrinRemoved(RemoveBuffEvent @event)
    {
        if (_held == 0)
            return;

        ExpiredUnspent += _held;
        _unclaimedRemoval = @event.Timestamp;
        _held = 0;
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
        if (_held > 0)
        {
            _held--;
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
