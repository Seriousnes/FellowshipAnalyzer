using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Elarion.Statistics;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Tracks how well Skystrider's Grace and Event Horizon are used "on CD". Records how long
/// each ability sat off-cooldown before the player used it, and the effective buff uptime
/// for each ability versus the maximum duration.
/// </summary>
public sealed partial class CooldownEfficiencyAnalyzer : Analyzer
{
    private readonly CooldownTracking _grace = new(SpellIds.SkystridersGrace, SpellIds.SkystridersGraceBuff);
    private readonly CooldownTracking _eventHorizon = new(SpellIds.EventHorizon, SpellIds.EventHorizonBuff);
    private int _fightStart;
    private int _fightEnd;

    public CooldownTracking SkystridersGrace => _grace;
    public CooldownTracking EventHorizon => _eventHorizon;

    public int FightLengthMs => Math.Max(0, _fightEnd - _fightStart);

    public override Type? StatisticsComponentType => typeof(CooldownEfficiencyStatistics);

    [On<FightStartEvent>]
    private void OnFightStart(FightStartEvent e) => _fightStart = e.Timestamp;

    [On<UpdateSpellUsableEvent>(By = Actor.Player, Spells = new[] { SpellIds.SkystridersGrace, SpellIds.EventHorizon })]
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

    [On<CastEvent>(By = Actor.Player, Spells = new[] { SpellIds.SkystridersGrace, SpellIds.EventHorizon })]
    private void OnCast(CastEvent e)
    {
        var tracking = SelectTracking(e.Ability.Id);
        if (tracking is null) return;

        tracking.Casts++;
    }

    [On<ApplyBuffEvent>(By = Actor.Player, Spells = new[] { SpellIds.SkystridersGraceBuff, SpellIds.EventHorizonBuff })]
    private void OnApplyBuff(ApplyBuffEvent e)
    {
        var tracking = SelectTrackingByBuff(e.Ability.Id);
        if (tracking is null) return;

        tracking.BuffStartTimestamp = e.Timestamp;
    }

    [On<RemoveBuffEvent>(By = Actor.Player, Spells = new[] { SpellIds.SkystridersGraceBuff, SpellIds.EventHorizonBuff })]
    private void OnRemoveBuff(RemoveBuffEvent e)
    {
        var tracking = SelectTrackingByBuff(e.Ability.Id);
        if (tracking is null || tracking.BuffStartTimestamp is not int start) return;

        tracking.TotalBuffUptimeMs += Math.Max(0, e.Timestamp - start);
        tracking.BuffStartTimestamp = null;
    }

    [On<FightEndEvent>]
    private void OnFightEnd(FightEndEvent e)
    {
        _fightEnd = e.Timestamp;
        CloseOpenWindows(_grace, e.Timestamp);
        CloseOpenWindows(_eventHorizon, e.Timestamp);
    }

    private CooldownTracking? SelectTracking(int spellId) => spellId switch
    {
        SpellIds.SkystridersGrace => _grace,
        SpellIds.EventHorizon => _eventHorizon,
        _ => null,
    };

    private CooldownTracking? SelectTrackingByBuff(int buffId) => buffId switch
    {
        SpellIds.SkystridersGraceBuff => _grace,
        SpellIds.EventHorizonBuff => _eventHorizon,
        _ => null,
    };

    private static void CloseOpenWindows(CooldownTracking tracking, int fightEnd)
    {
        if (tracking.OffCooldownTimestamp is int offCd)
        {
            tracking.TotalHeldMs += Math.Max(0, fightEnd - offCd);
            tracking.OffCooldownTimestamp = null;
        }
        if (tracking.BuffStartTimestamp is int buffStart)
        {
            tracking.TotalBuffUptimeMs += Math.Max(0, fightEnd - buffStart);
            tracking.BuffStartTimestamp = null;
        }
    }

    public sealed class CooldownTracking
    {
        public CooldownTracking(int spellId, int buffId)
        {
            SpellId = spellId;
            BuffId = buffId;
        }

        public int SpellId { get; }
        public int BuffId { get; }
        public int Casts { get; internal set; }
        public int TotalHeldMs { get; internal set; }
        public int TotalBuffUptimeMs { get; internal set; }
        internal int? OffCooldownTimestamp { get; set; }
        internal int? BuffStartTimestamp { get; set; }
    }
}
