using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

/// <summary>
/// Tracks the Executioner's Grin proc from Executioner's Unsanitary Bands, which lets Culling Strike be
/// cast on a target above <see cref="CullingStrikeAnalyzer.ExecuteHealthThreshold"/>. A proc is spent by
/// the next Culling Strike whatever the target's health, so the item only returns value when that cast lands
/// above the threshold; report <c>a:NcqHDKzamL7n6YFv</c> shows 66 of 102 procs doing so, 28 spent on a
/// cast that was legal anyway, and 8 expiring with no Culling Strike at all.
/// </summary>
public sealed partial class ExecutionersGrinTracker : EventSubscriber
{
    private bool _held;
    private int? _unclaimedRemoval;

    /// <summary>Executioner's Unsanitary Bands is in the analyzed player's gear.</summary>
    public bool Equipped => Owner.SelectedCombatant.HasItem(Items.ExecutionersUnsanitaryBands.Id);

    /// <summary>Procs gained.</summary>
    public int Procs { get; private set; }

    /// <summary>Procs that landed on a proc already held, which adds nothing.</summary>
    public int Reapplications { get; private set; }

    /// <summary>Procs spent on a Culling Strike above the execute threshold: the extra cast the item bought.</summary>
    public int SpentAboveThreshold { get; private set; }

    /// <summary>Procs spent on a Culling Strike that was already legal. Context rather than a fault - the game consumes the proc on any Culling Strike, and holding the cast to save it would cost more than the proc is worth.</summary>
    public int SpentBelowThreshold { get; private set; }

    /// <summary>Procs that fell off with no Culling Strike cast at all. The only avoidable loss.</summary>
    public int ExpiredUnspent { get; private set; }

    /// <summary>Culling Strikes that landed damage on a readable target.</summary>
    public int CullingStrikeHits { get; private set; }

    /// <summary>Culling Strikes that landed above the execute threshold, which only a proc makes possible.</summary>
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
