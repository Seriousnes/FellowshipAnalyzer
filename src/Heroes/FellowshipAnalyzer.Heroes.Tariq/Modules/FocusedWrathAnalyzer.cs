using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class FocusedWrathAnalyzer : Analyzer
{
    public const int BuffDurationMs = 15_000;

    private readonly List<TrackedCast> _tracked = [];

    private List<FocusedWrathCast> Evaluated => field ??= Build();

    public IReadOnlyList<FocusedWrathCast> Casts => Evaluated;

    public int CastCount => Evaluated.Count;

    public int UnpairedCasts => Evaluated.Count(cast => cast.LatencyToSpenderMs is null);

    public double? AverageLatencyMs
    {
        get
        {
            var paired = Evaluated.Where(cast => cast.LatencyToSpenderMs is not null).ToList();
            return paired.Count == 0 ? null : paired.Average(cast => cast.LatencyToSpenderMs!.Value);
        }
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.FocusedWrath))]
    private void OnFocusedWrathCast(CastEvent @event) => Track(@event, isFocusedWrath: true);

    [On<CastEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.SkullCrusher),
        nameof(Spells.HammerStorm),
        nameof(Spells.CullingStrike),
    })]
    private void OnSpenderCast(CastEvent @event) => Track(@event, isFocusedWrath: false);

    private void Track(CastEvent @event, bool isFocusedWrath)
    {
        if (@event.Fake)
            return;

        _tracked.Add(new TrackedCast(@event.Timestamp, isFocusedWrath));
    }

    private List<FocusedWrathCast> Build()
    {
        var built = new List<FocusedWrathCast>();
        for (var i = 0; i < _tracked.Count; i++)
        {
            if (!_tracked[i].IsFocusedWrath)
                continue;

            var timestamp = _tracked[i].Timestamp;
            int? latency = null;
            var spenders = 0;

            for (var j = i + 1; j < _tracked.Count; j++)
            {
                var candidate = _tracked[j];
                if (candidate.Timestamp - timestamp > BuffDurationMs)
                    break;

                if (candidate.IsFocusedWrath)
                    continue;

                spenders++;
                latency ??= candidate.Timestamp - timestamp;
            }

            built.Add(new FocusedWrathCast(timestamp, latency, spenders));
        }

        return built;
    }

    private readonly record struct TrackedCast(int Timestamp, bool IsFocusedWrath);
}

public readonly record struct FocusedWrathCast(int Timestamp, int? LatencyToSpenderMs, int SpendersInBuff);
