using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Measures how continuously each of a set of damage-over-time effects stayed on its primary target,
/// the multi-effect counterpart to <see cref="DebuffUptimeAnalyzer"/>. Each effect keeps its own
/// <see cref="AuraWindowLedger"/> and picks its own primary target - the one with the most
/// active time - so a transient add never dilutes the boss reading and two effects maintained on
/// different targets are both read honestly.
/// <para>
/// Derive a per-hero analyzer from this, keep <c>[ForPull]</c> and the surface marker interface on
/// the leaf, and declare the effects' own <c>[On&lt;&gt;]</c> handlers there: apply and refresh call
/// <see cref="OpenWindow"/>, remove calls <see cref="CloseWindow"/>, and every other event an effect
/// drives on its target (stack changes, ticks) calls <see cref="ObserveTarget"/>. Each takes the
/// event and files it under whichever effect in <see cref="Dots"/> its ability id names, so an event
/// for an untracked ability is ignored.
/// </para>
/// </summary>
public abstract class DotUptimeAnalyzer : Analyzer
{
    private readonly Dictionary<int, AuraWindowLedger> _ledgers = [];

    /// <summary>The effects this analyzer measures, in the order results are reported.</summary>
    protected abstract List<Dot> Dots { get; }

    private List<DotUptime> Result => field ??= Compute();

    /// <summary>One reading per effect in <see cref="Dots"/> order.</summary>
    public List<DotUptime> Uptimes => Result;

    /// <summary>The reading for <paramref name="dot"/>.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="dot"/> is not one of <see cref="Dots"/>.</exception>
    public DotUptime For(Dot dot)
    {
        foreach (var uptime in Result)
        {
            if (uptime.Dot.EffectId == dot.EffectId) return uptime;
        }

        throw new InvalidOperationException($"'{dot.Effect.Name}' is not measured by {GetType().Name}.");
    }

    /// <summary>Opens a window on the event's target unless one is already open. Call from apply and refresh handlers.</summary>
    protected void OpenWindow<TEvent>(TEvent e) where TEvent : Event, IAbilityEvent, IHasTargetWithInstanceEvent
    {
        if (LedgerFor(e.Ability.Id) is { } ledger)
            ledger.Open(e, e.Timestamp);
    }

    /// <summary>Closes the window open on the event's target, if any. Call from remove handlers.</summary>
    protected void CloseWindow<TEvent>(TEvent e) where TEvent : Event, IAbilityEvent, IHasTargetWithInstanceEvent
    {
        if (LedgerFor(e.Ability.Id) is { } ledger)
            ledger.Close(e, e.Timestamp);
    }

    /// <summary>
    /// Records that the event's target was still in the log, without opening or closing anything, so
    /// a window left open at pull end closes at the last moment its target is known to have existed.
    /// </summary>
    protected void ObserveTarget<TEvent>(TEvent e) where TEvent : Event, IAbilityEvent, IHasTargetWithInstanceEvent
    {
        if (LedgerFor(e.Ability.Id) is { } ledger)
            ledger.Observe(e, e.Timestamp);
    }

    private AuraWindowLedger? LedgerFor(int abilityId)
    {
        foreach (var dot in Dots)
        {
            if (dot.EffectId != abilityId) continue;

            if (!_ledgers.TryGetValue(abilityId, out var ledger))
                _ledgers[abilityId] = ledger = new AuraWindowLedger();
            return ledger;
        }
        return null;
    }

    private List<DotUptime> Compute()
    {
        var duration = Pull.EndTime - Pull.StartTime;
        var results = new List<DotUptime>(Dots.Count);

        foreach (var dot in Dots)
        {
            if (!_ledgers.TryGetValue(dot.EffectId, out var ledger))
            {
                results.Add(new DotUptime(dot, null, [], 0d, 0, 0));
                continue;
            }

            var primary = ledger.Build()
                .Select(entry => (Unit: entry.Key, entry.Value, Active: AuraWindowLedger.ActiveMs(entry.Value)))
                .Where(candidate => candidate.Active > 0)
                .OrderByDescending(candidate => candidate.Active)
                .ThenBy(candidate => candidate.Value[0].Start)
                .ThenBy(candidate => candidate.Unit.ActorId)
                .ToList();

            if (primary.Count == 0)
            {
                results.Add(new DotUptime(dot, null, [], 0d, 0, 0));
                continue;
            }

            var (unit, windows, active) = primary[0];

            var gapCount = 0;
            var totalGapMs = 0;
            for (var i = 1; i < windows.Count; i++)
            {
                var gap = windows[i].Start - windows[i - 1].End;
                if (gap <= 0) continue;

                gapCount++;
                totalGapMs += gap;
            }

            var uptime = duration > 0 ? Math.Min(1d, active / (double)duration) : 0d;
            results.Add(new DotUptime(dot, unit, windows, uptime, gapCount, totalGapMs));
        }

        return results;
    }
}

/// <summary>
/// How continuously one effect was active on its primary target.
/// </summary>
/// <param name="Dot">The effect measured.</param>
/// <param name="PrimaryTarget">The target every figure here is taken against, or <c>null</c> when the
/// effect was never applied.</param>
/// <param name="Windows">That target's windows, in encounter order.</param>
/// <param name="Uptime">Share of the pull (0-1) the effect was active on the primary target.</param>
/// <param name="GapCount">Gaps between <paramref name="Windows"/>.</param>
/// <param name="TotalGapMs">Milliseconds the effect was off the primary target between its windows.</param>
public sealed record DotUptime(
    Dot Dot,
    UnitKey? PrimaryTarget,
    List<AuraWindow> Windows,
    double Uptime,
    int GapCount,
    int TotalGapMs);
