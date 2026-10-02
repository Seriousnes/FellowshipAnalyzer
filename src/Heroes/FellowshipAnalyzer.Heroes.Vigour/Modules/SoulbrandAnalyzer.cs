using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Vigour;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Vigour.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<Combatants>]
public sealed partial class SoulbrandAnalyzer : Analyzer
{
    public int CastCount { get; private set; }

    public int CoveredMs => Result.CoveredMs;

    public double Coverage => Pull.Duration > 0 ? Math.Min(1d, Result.CoveredMs / (double)Pull.Duration) : 0;

    public double AverageEnemies => Pull.Duration > 0 ? Result.EnemyMs / (double)Pull.Duration : 0;

    private Computed Result => field ??= Compute();

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Soulbrand))]
    private void OnCast(CastEvent e)
    {
        if (e.Fake) return;

        CastCount++;
    }

    private Computed Compute()
    {
        var windows = Combatants.Units.Values
            .OfType<Enemy>()
            .Select(enemy => enemy.GetAuraWindows(Spells.SoulbrandDot, Pull.StartTime, Pull.EndTime, PlayerId).ToList())
            .Where(list => list.Count > 0)
            .ToList();

        var enemyMs = windows.Sum(list => (long)AuraWindowLedger.ActiveMs(list));
        var covered = AuraWindowLedger.ActiveMs([.. windows.SelectMany(list => list)]);
        return new Computed(covered, enemyMs);
    }

    private sealed record Computed(int CoveredMs, long EnemyMs);
}
