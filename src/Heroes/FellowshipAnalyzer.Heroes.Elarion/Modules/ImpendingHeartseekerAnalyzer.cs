using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Elarion.Statistics;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Tracks Impending Heartseeker proc lifecycle: gains, refreshes, consumptions, and expirations.
/// Procs last 15s; a second proc resets the first timer. Expired procs are wasted procs.
/// </summary>
public sealed partial class ImpendingHeartseekerAnalyzer : Analyzer
{
    private const int ProcDurationMs = 15_000;
    private const int ExpiryToleranceMs = 250;

    private readonly List<ProcEvent> _events = [];
    private int? _activeStartTimestamp;
    private int _activeStacks;

    public IReadOnlyList<ProcEvent> Procs => _events;
    public int Gains => _events.Count(p => p.Kind == ProcKind.Gain);
    public int Refreshes => _events.Count(p => p.Kind == ProcKind.Refresh);
    public int Consumed => _events.Count(p => p.Kind == ProcKind.Consumed);
    public int Expired => _events.Count(p => p.Kind == ProcKind.Expired);

    public override Type? StatisticsComponentType => typeof(ImpendingHeartseekerStatistics);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = SpellIds.ImpendingHeartseekerBuff)]
    private void OnApply(ApplyBuffEvent e)
    {
        _activeStartTimestamp = e.Timestamp;
        _activeStacks = 1;
        _events.Add(new ProcEvent(e.Timestamp, ProcKind.Gain, _activeStacks));
    }

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = SpellIds.ImpendingHeartseekerBuff)]
    private void OnApplyStack(ApplyBuffStackEvent e)
    {
        _activeStartTimestamp = e.Timestamp;
        _activeStacks = e.Stack;
        _events.Add(new ProcEvent(e.Timestamp, ProcKind.Gain, _activeStacks));
    }

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = SpellIds.ImpendingHeartseekerBuff)]
    private void OnRefresh(RefreshBuffEvent e)
    {
        _activeStartTimestamp = e.Timestamp;
        _events.Add(new ProcEvent(e.Timestamp, ProcKind.Refresh, _activeStacks));
    }

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = SpellIds.ImpendingHeartseekerBuff)]
    private void OnRemoveStack(RemoveBuffStackEvent e)
    {
        _activeStacks = e.Stack;
        var kind = IsExpiry(e.Timestamp) ? ProcKind.Expired : ProcKind.Consumed;
        _events.Add(new ProcEvent(e.Timestamp, kind, _activeStacks));
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = SpellIds.ImpendingHeartseekerBuff)]
    private void OnRemove(RemoveBuffEvent e)
    {
        var kind = IsExpiry(e.Timestamp) ? ProcKind.Expired : ProcKind.Consumed;
        _events.Add(new ProcEvent(e.Timestamp, kind, 0));
        _activeStartTimestamp = null;
        _activeStacks = 0;
    }

    private bool IsExpiry(int now)
    {
        if (_activeStartTimestamp is not int start) return false;
        var elapsed = now - start;
        return elapsed >= ProcDurationMs - ExpiryToleranceMs;
    }

    public readonly record struct ProcEvent(int Timestamp, ProcKind Kind, int Stacks);

    public enum ProcKind
    {
        Gain,
        Refresh,
        Consumed,
        Expired,
    }
}
