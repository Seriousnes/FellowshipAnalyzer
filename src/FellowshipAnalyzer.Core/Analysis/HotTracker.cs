using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Attributes periodic healing to the application that is delivering it, keyed by
/// (TargetId, TargetInstance) and spell. A heal lands on the assignment open for its spell and target,
/// so the ticks, effective healing and overheal of one application are separable from the next one on
/// the same ally.
/// <para>
/// This deliberately models nothing about duration. There is no clipping, pandemic or extension
/// accounting here: an assignment opens when the log says the aura went on and closes when the log
/// says it came off, and healing in between belongs to it. Nothing infers when it <em>should</em>
/// have ended.
/// </para>
/// <para>
/// Derive a per-hero tracker, override <see cref="TracksSpell"/>, and register it with
/// <c>[AddState&lt;T&gt;]</c> so the assignments outlive a single pull - an aura applied in the first
/// pull and still held in the last has no application event inside the later pulls at all.
/// </para>
/// </summary>
public partial class HotTracker : Analyzer
{
    private readonly List<HotAssignment> _assignments = [];
    private readonly Dictionary<(int SpellId, UnitKey Unit), HotAssignment> _open = [];

    private int _unattributedTicks;
    private long _unattributedHealing;

    /// <summary>Every assignment observed this parse, in the order it opened.</summary>
    public List<HotAssignment> Assignments => _assignments;

    /// <summary>Assignments still open when the parse ended.</summary>
    public List<HotAssignment> OpenAssignments => [.. _open.Values];

    /// <summary>Ticks that landed with no assignment open for their spell and target.</summary>
    public int UnattributedTicks => _unattributedTicks;

    /// <summary>Effective healing plus overheal from ticks with no assignment open.</summary>
    public long UnattributedHealing => _unattributedHealing;

    /// <summary>Every assignment of <paramref name="spellId"/>, in the order it opened.</summary>
    public IEnumerable<HotAssignment> AssignmentsOf(int spellId) =>
        _assignments.Where(assignment => assignment.SpellId == spellId);

    /// <summary>The assignment of <paramref name="spellId"/> open on <paramref name="unit"/>, or <c>null</c>.</summary>
    public HotAssignment? OpenOn(int spellId, UnitKey unit) => _open.GetValueOrDefault((spellId, unit));

    /// <summary>Whether the tracker attributes healing for <paramref name="spellId"/>. Override to name the spells to track.</summary>
    protected virtual bool TracksSpell(int spellId) => false;

    /// <summary>
    /// Called when a tracked aura is reapplied to a unit that already carries it. The default records
    /// the refresh on the open assignment; override to react to a reapplication that the hero treats
    /// as a new application rather than a continuation.
    /// </summary>
    protected virtual void OnRefreshed(HotAssignment assignment, int timestamp) =>
        assignment.RecordRefresh(timestamp);

    [On<ApplyBuffEvent>(By = Actor.Player)]
    private void OnApplied(ApplyBuffEvent buffEvent) => Open(buffEvent);

    [On<RefreshBuffEvent>(By = Actor.Player)]
    private void OnRefreshedBuff(RefreshBuffEvent buffEvent)
    {
        if (!TracksSpell(buffEvent.Ability.Id)) return;

        var key = (buffEvent.Ability.Id, AuraWindowLedger.KeyOf(buffEvent));
        if (_open.TryGetValue(key, out var assignment))
            OnRefreshed(assignment, buffEvent.Timestamp);
        else
            Open(buffEvent);
    }

    [On<RemoveBuffEvent>(By = Actor.Player)]
    private void OnRemoved(RemoveBuffEvent buffEvent)
    {
        if (!TracksSpell(buffEvent.Ability.Id)) return;

        var key = (buffEvent.Ability.Id, AuraWindowLedger.KeyOf(buffEvent));
        if (!_open.Remove(key, out var assignment)) return;

        assignment.Close(buffEvent.Timestamp);
    }

    [On<HealEvent>(By = Actor.Player)]
    private void OnHeal(HealEvent healEvent)
    {
        if (!TracksSpell(healEvent.Ability.Id)) return;

        var total = healEvent.Amount + (healEvent.Overheal ?? 0);
        var key = (healEvent.Ability.Id, new UnitKey(healEvent.TargetId, healEvent.TargetInstance ?? 0));
        if (_open.TryGetValue(key, out var assignment))
        {
            assignment.RecordTick(healEvent.Timestamp, healEvent.Amount, healEvent.Overheal ?? 0);
            return;
        }

        _unattributedTicks++;
        _unattributedHealing += total;
    }

    private void Open(BuffEvent buffEvent)
    {
        if (!TracksSpell(buffEvent.Ability.Id)) return;

        var unit = AuraWindowLedger.KeyOf(buffEvent);
        var key = (buffEvent.Ability.Id, unit);
        if (_open.TryGetValue(key, out var running))
            running.Close(buffEvent.Timestamp);

        var assignment = new HotAssignment(buffEvent.Ability.Id, unit, buffEvent.Timestamp);
        _assignments.Add(assignment);
        _open[key] = assignment;
    }
}

/// <summary>
/// One continuous application of a periodic heal on one unit, and the healing it delivered.
/// </summary>
public sealed class HotAssignment
{
    private readonly List<int> _refreshes = [];

    internal HotAssignment(int spellId, UnitKey unit, int start)
    {
        SpellId = spellId;
        Unit = unit;
        Start = start;
        LastTick = start;
    }

    /// <summary>The spell whose application this is.</summary>
    public int SpellId { get; }

    /// <summary>The unit carrying it.</summary>
    public UnitKey Unit { get; }

    /// <summary>When it was applied.</summary>
    public int Start { get; }

    /// <summary>When it came off, or <c>null</c> while it is still on.</summary>
    public int? End { get; private set; }

    /// <summary>The last tick this assignment delivered, or <see cref="Start"/> when it has delivered none.</summary>
    public int LastTick { get; private set; }

    /// <summary>Ticks attributed to this assignment.</summary>
    public int Ticks { get; private set; }

    /// <summary>Effective healing attributed to this assignment.</summary>
    public long Effective { get; private set; }

    /// <summary>Overheal attributed to this assignment.</summary>
    public long Overheal { get; private set; }

    /// <summary>Timestamps at which the aura was reapplied without coming off.</summary>
    public List<int> Refreshes => _refreshes;

    /// <summary>Effective healing plus overheal.</summary>
    public long TotalHealing => Effective + Overheal;

    /// <summary>Share (0-1) of this assignment's healing that was overheal.</summary>
    public double OverhealShare => TotalHealing > 0 ? Overheal / (double)TotalHealing : 0;

    /// <summary>How long the assignment held, measured to <paramref name="openEnd"/> while it is still on.</summary>
    public int DurationMs(int openEnd) => Math.Max(0, (End ?? openEnd) - Start);

    internal void RecordTick(int timestamp, long effective, long overheal)
    {
        Ticks++;
        Effective += effective;
        Overheal += overheal;
        LastTick = timestamp;
    }

    internal void RecordRefresh(int timestamp) => _refreshes.Add(timestamp);

    internal void Close(int timestamp) => End = timestamp;
}
