using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Tariq.Statistics;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

/// <summary>
/// Tracks Rising Spirit, the stacking self-buff that Spirit refund procs grant. It builds up while the
/// procs keep landing and falls off when they stop, so how much of a fight it covers and how deep the
/// stacks ran are a readout on the pace the player sustained rather than on any one decision.
/// </summary>
/// <remarks>
/// Stacks are read from the buff stream directly: an <c>applybuff</c> opens a window at one stack, each
/// <c>applybuffstack</c> carries the new total, and a <c>removebuff</c> closes the window whole - the buff
/// is dropped outright rather than decayed one stack at a time. A window still open when the fight ends is
/// closed at the fight boundary, so a buff held to the last pull is credited for the time it was actually
/// up.
/// </remarks>
public sealed partial class RisingSpiritTracker : EventSubscriber
{
    /// <summary>Stacks Rising Spirit holds at most, from the effect's own data.</summary>
    public const int StackCap = 5;

    private int _fightStart;
    private int _fightEnd;
    private int _windowStart;
    private int _lastStackChange;
    private int _currentStacks;
    private long _weightedStackMs;

    /// <summary>Fresh applications of the buff observed on the player.</summary>
    public int Applications { get; private set; }

    /// <summary>Stacks held right now, or zero while the buff is down.</summary>
    public int CurrentStacks { get; private set; }

    /// <summary>The deepest stack count observed, up to <see cref="StackCap"/>.</summary>
    public int MaxStacks { get; private set; }

    /// <summary>Total milliseconds the buff was up.</summary>
    public int TotalActiveMs { get; private set; }

    /// <summary>Milliseconds the fight spanned.</summary>
    public int FightDurationMs => Math.Max(0, _fightEnd - _fightStart);

    /// <summary>Share (0-1) of the fight the buff was up.</summary>
    public double UptimeShare => FightDurationMs == 0 ? 0d : Math.Min(1d, (double)TotalActiveMs / FightDurationMs);

    /// <summary>Mean stacks held while the buff was up, weighted by the time spent at each depth.</summary>
    public double AverageStacks => TotalActiveMs == 0 ? 0d : (double)_weightedStackMs / TotalActiveMs;

    public override Type? StatisticsComponentType => Applications > 0 ? typeof(RisingSpiritStatistics) : null;

    [On<FightStartEvent>]
    private void OnFightStart(FightStartEvent @event)
    {
        _fightStart = @event.Timestamp;
        _fightEnd = @event.Timestamp;
    }

    [On<FightEndEvent>]
    private void OnFightEnd(FightEndEvent @event)
    {
        _fightEnd = @event.Timestamp;
        CloseWindow(@event.Timestamp);
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.RisingSpirit))]
    private void OnApplied(ApplyBuffEvent @event)
    {
        Applications++;

        if (_currentStacks > 0)
            AccrueTo(@event.Timestamp);
        else
            _windowStart = @event.Timestamp;

        SetStacks(1, @event.Timestamp);
    }

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.RisingSpirit))]
    private void OnStackGained(ApplyBuffStackEvent @event)
    {
        if (_currentStacks == 0)
        {
            _windowStart = @event.Timestamp;
            _lastStackChange = @event.Timestamp;
        }
        else
        {
            AccrueTo(@event.Timestamp);
        }

        SetStacks(Math.Max(@event.Stack, _currentStacks + 1), @event.Timestamp);
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.RisingSpirit))]
    private void OnRemoved(RemoveBuffEvent @event) => CloseWindow(@event.Timestamp);

    private void CloseWindow(int timestamp)
    {
        if (_currentStacks == 0)
            return;

        AccrueTo(timestamp);
        TotalActiveMs += Math.Max(0, timestamp - _windowStart);
        _currentStacks = 0;
        CurrentStacks = 0;
    }

    private void AccrueTo(int timestamp)
    {
        _weightedStackMs += (long)_currentStacks * Math.Max(0, timestamp - _lastStackChange);
        _lastStackChange = timestamp;
    }

    private void SetStacks(int stacks, int timestamp)
    {
        _currentStacks = Math.Min(stacks, StackCap);
        _lastStackChange = timestamp;
        CurrentStacks = _currentStacks;

        if (_currentStacks > MaxStacks)
            MaxStacks = _currentStacks;
    }
}
