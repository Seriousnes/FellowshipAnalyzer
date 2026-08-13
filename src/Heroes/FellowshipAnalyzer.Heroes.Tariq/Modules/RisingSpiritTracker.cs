using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

public sealed partial class RisingSpiritTracker : Analyzer
{
    private int _fightStart;
    private int _fightEnd;
    private int _windowStart;
    private int _lastStackChange;
    private int _currentStacks;
    private long _weightedStackMs;

    public int Applications { get; private set; }

    public int CurrentStacks { get; private set; }

    public int MaxStacks { get; private set; }

    public int TotalActiveMs { get; private set; }

    public int FightDurationMs => Math.Max(0, _fightEnd - _fightStart);

    public double UptimeShare => FightDurationMs == 0 ? 0d : Math.Min(1d, (double)TotalActiveMs / FightDurationMs);

    public double AverageStacks => TotalActiveMs == 0 ? 0d : (double)_weightedStackMs / TotalActiveMs;

    [On<DungeonStartEvent>]
    private void OnFightStart(DungeonStartEvent @event)
    {
        _fightStart = @event.Timestamp;
        _fightEnd = @event.Timestamp;
    }

    [On<DungeonEndEvent>]
    private void OnFightEnd(DungeonEndEvent @event)
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
        _currentStacks = stacks;
        _lastStackChange = timestamp;
        CurrentStacks = _currentStacks;

        if (_currentStacks > MaxStacks)
            MaxStacks = _currentStacks;
    }
}
