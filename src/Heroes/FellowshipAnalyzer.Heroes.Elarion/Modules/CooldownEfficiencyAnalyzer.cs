using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>Per-ability cooldown-usage snapshot for one pull.</summary>
public readonly record struct CooldownSnapshot(int Casts, int HeldMs, int BuffUptimeMs);

/// <summary>
/// Tracks how well Skystrider's Grace and Event Horizon are used "on CD" within a pull. Records how
/// long each ability sat off-cooldown before the player used it, and the effective buff uptime for
/// each ability versus the pull duration. Cooldown discipline is shape-agnostic, so it runs on every
/// pull shape.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class CooldownEfficiencyAnalyzer : Analyzer
{
    private readonly CooldownTracking _grace = new(Spells.SkystridersGrace.FSLID, Spells.SkystridersGraceBuff.FSLID);
    private readonly CooldownTracking _eventHorizon = new(Spells.EventHorizon.FSLID, Spells.EventHorizonBuff.FSLID);
    private int _pullStart;
    private int _pullEnd;

    public CooldownSnapshot Grace { get; private set; }
    public CooldownSnapshot EventHorizon { get; private set; }
    public int PullDurationMs { get; private set; }

    [On<PullStartEvent>]
    private void OnPullStart(PullStartEvent e) => _pullStart = e.Timestamp;

    [On<UpdateSpellUsableEvent>(By = Actor.Player, Spells = [nameof(Spells.SkystridersGrace), nameof(Spells.EventHorizon)])]
    private void OnUpdate(UpdateSpellUsableEvent e)
    {
        var tracking = SelectTracking(e.Ability.Id);
        if (tracking is null) return;

        if (e.UpdateType == UpdateSpellUsableType.EndCooldown)
        {
            tracking.OffCooldownTimestamp = e.Timestamp;
        }
        else if (e.UpdateType == UpdateSpellUsableType.BeginCooldown && tracking.OffCooldownTimestamp is int offCd)
        {
            tracking.TotalHeldMs += Math.Max(0, e.Timestamp - offCd);
            tracking.OffCooldownTimestamp = null;
        }
    }

    [On<CastEvent>(By = Actor.Player, Spells = [nameof(Spells.SkystridersGrace), nameof(Spells.EventHorizon)])]
    private void OnCast(CastEvent e)
    {
        var tracking = SelectTracking(e.Ability.Id);
        if (tracking is null) return;

        tracking.Casts++;
    }

    [On<ApplyBuffEvent>(By = Actor.Player, Spells = [nameof(Spells.SkystridersGraceBuff), nameof(Spells.EventHorizonBuff)])]
    private void OnApplyBuff(ApplyBuffEvent e)
    {
        var tracking = SelectTrackingByBuff(e.Ability.Id);
        if (tracking is null) return;

        tracking.BuffStartTimestamp = e.Timestamp;
    }

    [On<RemoveBuffEvent>(By = Actor.Player, Spells = [nameof(Spells.SkystridersGraceBuff), nameof(Spells.EventHorizonBuff)])]
    private void OnRemoveBuff(RemoveBuffEvent e)
    {
        var tracking = SelectTrackingByBuff(e.Ability.Id);
        if (tracking is null || tracking.BuffStartTimestamp is not int start) return;

        tracking.TotalBuffUptimeMs += Math.Max(0, e.Timestamp - start);
        tracking.BuffStartTimestamp = null;
    }

    [On<PullEndEvent>]
    private void OnPullEndEvent(PullEndEvent e)
    {
        _pullEnd = e.Timestamp;
        CloseOpenWindows(_grace, e.Timestamp);
        CloseOpenWindows(_eventHorizon, e.Timestamp);
    }

    public override void OnPullEnd()
    {
        Grace = new CooldownSnapshot(_grace.Casts, _grace.TotalHeldMs, _grace.TotalBuffUptimeMs);
        EventHorizon = new CooldownSnapshot(_eventHorizon.Casts, _eventHorizon.TotalHeldMs, _eventHorizon.TotalBuffUptimeMs);
        PullDurationMs = Math.Max(0, _pullEnd - _pullStart);
    }

    private CooldownTracking? SelectTracking(int spellId)
    {
        if (spellId == Spells.SkystridersGrace.FSLID) return _grace;
        if (spellId == Spells.EventHorizon.FSLID) return _eventHorizon;
        return null;
    }

    private CooldownTracking? SelectTrackingByBuff(int buffId)
    {
        if (buffId == Spells.SkystridersGraceBuff.FSLID) return _grace;
        if (buffId == Spells.EventHorizonBuff.FSLID) return _eventHorizon;
        return null;
    }

    private static void CloseOpenWindows(CooldownTracking tracking, int pullEnd)
    {
        if (tracking.OffCooldownTimestamp is int offCd)
        {
            tracking.TotalHeldMs += Math.Max(0, pullEnd - offCd);
            tracking.OffCooldownTimestamp = null;
        }
        if (tracking.BuffStartTimestamp is int buffStart)
        {
            tracking.TotalBuffUptimeMs += Math.Max(0, pullEnd - buffStart);
            tracking.BuffStartTimestamp = null;
        }
    }

    private sealed class CooldownTracking(int spellId, int buffId)
    {
        public int SpellId { get; } = spellId;
        public int BuffId { get; } = buffId;
        public int Casts { get; set; }
        public int TotalHeldMs { get; set; }
        public int TotalBuffUptimeMs { get; set; }
        public int? OffCooldownTimestamp { get; set; }
        public int? BuffStartTimestamp { get; set; }
    }
}
