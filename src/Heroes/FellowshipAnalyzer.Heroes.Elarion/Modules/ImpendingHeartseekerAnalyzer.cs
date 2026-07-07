using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Tracks Impending Heartseeker proc lifecycle within a pull: gains, refreshes, consumptions, and
/// expirations. Procs last 15s; a second proc resets the first timer. Expired procs are wasted
/// procs. Proc management is shape-agnostic, so this runs on every pull shape.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class ImpendingHeartseekerAnalyzer : Analyzer<ImpendingHeartseekerReport>
{
    private const int ProcDurationMs = 15_000;
    private const int ExpiryToleranceMs = 250;

    private readonly List<ProcEvent> _events = [];
    private int? _activeStartTimestamp;
    private int _activeStacks;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ImpendingHeartseeker))]
    private void OnApply(ApplyBuffEvent e)
    {
        _activeStartTimestamp = e.Timestamp;
        _activeStacks = 1;
        _events.Add(new ProcEvent(e.Timestamp, ProcKind.Gain, _activeStacks));
    }

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ImpendingHeartseeker))]
    private void OnApplyStack(ApplyBuffStackEvent e)
    {
        _activeStartTimestamp = e.Timestamp;
        _activeStacks = e.Stack;
        _events.Add(new ProcEvent(e.Timestamp, ProcKind.Gain, _activeStacks));
    }

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ImpendingHeartseeker))]
    private void OnRefresh(RefreshBuffEvent e)
    {
        _activeStartTimestamp = e.Timestamp;
        _events.Add(new ProcEvent(e.Timestamp, ProcKind.Refresh, _activeStacks));
    }

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.ImpendingHeartseeker))]
    private void OnRemoveStack(RemoveBuffStackEvent e)
    {
        _activeStacks = e.Stack;
        var kind = IsExpiry(e.Timestamp) ? ProcKind.Expired : ProcKind.Consumed;
        _events.Add(new ProcEvent(e.Timestamp, kind, _activeStacks));
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ImpendingHeartseeker))]
    private void OnRemove(RemoveBuffEvent e)
    {
        var kind = IsExpiry(e.Timestamp) ? ProcKind.Expired : ProcKind.Consumed;
        _events.Add(new ProcEvent(e.Timestamp, kind, 0));
        _activeStartTimestamp = null;
        _activeStacks = 0;
    }

    public override ImpendingHeartseekerReport OnPullEnd() =>
        new(
            _events,
            _events.Count(p => p.Kind == ProcKind.Gain),
            _events.Count(p => p.Kind == ProcKind.Refresh),
            _events.Count(p => p.Kind == ProcKind.Consumed),
            _events.Count(p => p.Kind == ProcKind.Expired));

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
