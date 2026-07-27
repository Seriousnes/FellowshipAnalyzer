using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

[ForPull(PullKind.Multi)]
[Dependency<Combatants>]
public sealed partial class MaraDotSpreadAnalyzer : Analyzer, IMaraDotAnalyzer
{
    private const int UnknownRosterReference = 3;

    private readonly HashSet<(int TargetId, int TargetInstance)> _hemorrhageTargets = [];
    private readonly HashSet<(int TargetId, int TargetInstance)> _seethingPoisonTargets = [];
    private readonly HashSet<(int TargetId, int TargetInstance)> _volatilePoisonTargets = [];

    public int HemorrhageTargets => _hemorrhageTargets.Count;

    public int HemorrhageApplications { get; private set; }

    public int SeethingPoisonTargets => _seethingPoisonTargets.Count;

    public int VolatilePoisonTargets => _volatilePoisonTargets.Count;

    public int VolatilePoisonApplications { get; private set; }

    public int VolatilePoisonRefreshes { get; private set; }

    public int TargetCount => Pull.TargetCount;

    private int? _enemiesEngaged;

    public int EnemiesEngaged => _enemiesEngaged ??= CountEnemiesEngaged();

    public int RosterSize => TargetCount > 0 ? TargetCount : Math.Max(EnemiesEngaged, UnknownRosterReference);

    public double Coverage => RosterSize == 0 ? 0d : Math.Min(1d, HemorrhageTargets / (double)RosterSize);

    [On<ApplyDebuffEvent>(By = Actor.Player, Spells = [
        nameof(Spells.WidowBitePoison),
        nameof(Spells.HemorrhagingStrikeBleed),
        nameof(Spells.SkitteringBladesPoison)])]
    private void OnApplied(ApplyDebuffEvent e) => Record(e, refresh: false);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spells = [
        nameof(Spells.WidowBitePoison),
        nameof(Spells.HemorrhagingStrikeBleed),
        nameof(Spells.SkitteringBladesPoison)])]
    private void OnRefreshed(RefreshDebuffEvent e) => Record(e, refresh: true);

    private void Record(BuffEvent e, bool refresh)
    {
        var key = (e.TargetId, e.TargetInstance ?? 0);

        if (e.Ability.Id == MaraDots.Hemorrhage.EffectId)
        {
            _hemorrhageTargets.Add(key);
            if (!refresh)
                HemorrhageApplications++;
            return;
        }

        if (e.Ability.Id == MaraDots.SeethingPoison.EffectId)
        {
            _seethingPoisonTargets.Add(key);
            return;
        }

        if (e.Ability.Id != MaraDots.VolatilePoison.EffectId) return;

        _volatilePoisonTargets.Add(key);
        if (refresh)
            VolatilePoisonRefreshes++;
        else
            VolatilePoisonApplications++;
    }

    private int CountEnemiesEngaged()
    {
        var engaged = 0;
        foreach (var enemy in Combatants.Units.Values.OfType<Enemy>())
        {
            foreach (var buff in enemy.Buffs)
            {
                if (buff.SourceId != Owner.PlayerId) continue;
                if (buff.Start > Pull.EndTime || (buff.End ?? Pull.EndTime) < Pull.StartTime) continue;

                engaged++;
                break;
            }
        }
        return engaged;
    }
}
