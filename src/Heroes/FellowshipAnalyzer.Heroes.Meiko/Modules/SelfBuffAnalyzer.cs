using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.Heroes.Meiko.Modules;

public abstract class SelfBuffAnalyzer : Analyzer
{
    public const int RecastGraceMs = 50;

    private readonly Dictionary<int, int> _msAtStacks = [];

    private int _stacks;
    private int _since;
    private int _drops;
    private int? _pendingDrop;

    protected abstract Spell Buff { get; }

    public IReadOnlyDictionary<int, int> MsAtStacks => Totals();

    public int ActiveMs => Totals().Where(entry => entry.Key > 0).Sum(entry => entry.Value);

    public int DurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    public int InactiveMs => Math.Max(0, DurationMs - ActiveMs);

    public double Uptime => DurationMs > 0 ? Math.Clamp((double)ActiveMs / DurationMs, 0, 1) : 0;

    public int Drops => _drops + (_pendingDrop is null ? 0 : 1);

    protected void Seed(int timestamp)
    {
        _since = timestamp;
        _stacks = Owner.SelectedCombatant.HasBuff(Buff, timestamp)
            ? Math.Max(1, Owner.SelectedCombatant.GetBuffStacks(Buff, timestamp))
            : 0;
    }

    protected void SetStacks(int timestamp, int stacks)
    {
        Accumulate(timestamp);
        if (_pendingDrop is { } droppedAt && stacks > 0)
        {
            if (timestamp - droppedAt > RecastGraceMs) _drops++;
            _pendingDrop = null;
        }
        if (_stacks > 0 && stacks == 0) _pendingDrop = timestamp;
        _stacks = Math.Max(0, stacks);
    }

    private void Accumulate(int timestamp)
    {
        var elapsed = Math.Max(0, timestamp - _since);
        _msAtStacks[_stacks] = _msAtStacks.GetValueOrDefault(_stacks) + elapsed;
        _since = Math.Max(_since, timestamp);
    }

    private Dictionary<int, int> Totals()
    {
        var totals = new Dictionary<int, int>(_msAtStacks);
        var open = Math.Max(0, Pull.EndTime - _since);
        totals[_stacks] = totals.GetValueOrDefault(_stacks) + open;
        return totals;
    }
}
