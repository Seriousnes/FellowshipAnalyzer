using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class SafeHavenAnalyzer : Analyzer
{
    private readonly List<int> _casts = [];
    private readonly List<(int CastIndex, int TargetId, AuraWindow Window)> _windows = [];
    private readonly Dictionary<int, (int CastIndex, int Start)> _open = [];

    private Computed Result => field ??= Compute();

    public IReadOnlyList<SafeHavenPlacement> Placements => Result.Placements;

    public int Casts => _casts.Count;

    public int CastsWithoutAnAlly => Result.Placements.Count(placement => placement.Allies == 0);

    public long AllyMs => Result.Placements.Sum(placement => placement.AllyMs);

    public int AlliesReached => Result.Placements.Sum(placement => placement.Allies);

    public long OfferedAllyMs => Result.Placements.Sum(placement => (long)placement.Allies * placement.AvailableMs);

    public long CarriedInAllyMs => Result.CarriedInAllyMs;

    public int CarriedInAllies => Result.CarriedInAllies;

    public double HeldShare => OfferedAllyMs > 0 ? AllyMs / (double)OfferedAllyMs : 0;

    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.SafeHaven))]
    private void OnCast(CastEvent castEvent) => _casts.Add(castEvent.Timestamp);

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.SafeHavenIncreasedHealingTakenBuff))]
    private void OnApplied(ApplyBuffEvent buffEvent) => Open(buffEvent.TargetId, buffEvent.Timestamp);

    [On<RefreshBuffEvent>(By = Actor.Player, Spell = nameof(Spells.SafeHavenIncreasedHealingTakenBuff))]
    private void OnRefreshed(RefreshBuffEvent buffEvent) => Open(buffEvent.TargetId, buffEvent.Timestamp);

    [On<RemoveBuffEvent>(By = Actor.Player, Spell = nameof(Spells.SafeHavenIncreasedHealingTakenBuff))]
    private void OnRemoved(RemoveBuffEvent buffEvent)
    {
        if (_open.Remove(buffEvent.TargetId, out var entry))
            _windows.Add((entry.CastIndex, buffEvent.TargetId, new AuraWindow(entry.Start, buffEvent.Timestamp)));
    }

    private void Open(int targetId, int timestamp)
    {
        if (_open.ContainsKey(targetId)) return;

        _open[targetId] = (CastIndexAt(timestamp), timestamp);
    }

    private int CastIndexAt(int timestamp)
    {
        for (var index = _casts.Count - 1; index >= 0; index--)
            if (_casts[index] <= timestamp)
                return index;

        return -1;
    }

    private Computed Compute()
    {
        var closed = new List<(int CastIndex, int TargetId, AuraWindow Window)>(_windows);
        foreach (var (targetId, entry) in _open)
            closed.Add((entry.CastIndex, targetId, new AuraWindow(entry.Start, Pull.EndTime)));

        var placements = new List<SafeHavenPlacement>(_casts.Count);
        for (var index = 0; index < _casts.Count; index++)
        {
            var cast = _casts[index];
            var expiry = cast + SylvieKit.SafeHavenDurationMs;
            var available = Math.Max(0, Math.Min(expiry, Pull.EndTime) - cast);
            var mine = closed.Where(entry => entry.CastIndex == index).ToArray();

            placements.Add(new SafeHavenPlacement(
                cast,
                mine.Select(entry => entry.TargetId).Distinct().Count(),
                mine.Sum(entry => (long)entry.Window.Duration),
                available,
                expiry > Pull.EndTime));
        }

        var carried = closed.Where(entry => entry.CastIndex < 0).ToArray();

        return new Computed(
            placements,
            carried.Sum(entry => (long)entry.Window.Duration),
            carried.Select(entry => entry.TargetId).Distinct().Count());
    }

    private sealed record Computed(
        IReadOnlyList<SafeHavenPlacement> Placements,
        long CarriedInAllyMs,
        int CarriedInAllies);
}

public sealed record SafeHavenPlacement(
    int Timestamp,
    int Allies,
    long AllyMs,
    int AvailableMs,
    bool Truncated)
{
    public double HeldShare => Allies > 0 && AvailableMs > 0
        ? Math.Min(1d, AllyMs / (double)((long)Allies * AvailableMs))
        : 0;
}
