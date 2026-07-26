using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

/// <summary>
/// Measures how well Mara spread her poisons and bleeds across a multi-target pull. Each enemy that
/// took an effect from the player is counted once per effect by (TargetId, TargetInstance); applies and
/// refreshes both mark a target debuffed and the HashSet dedupes, so a refresh never counts as a new
/// enemy. Counting is monotonic, so enemies that die carrying an effect (which never log a remove) do
/// not drift the metric. Coverage is the Hemorrhage bleed against the pull's enemy roster, because the
/// bleed is the effect that has to be spread by hand.
/// <para>
/// Volatile Poison is counted the other way round. It erupts for area damage as it expires, so laying
/// it again over a standing window forfeits that blast: reapplications are the metric, not uptime.
/// </para>
/// </summary>
/// <remarks>
/// The roster denominator is the pull's own reported enemy count. When the pull reports none, the
/// enemies the player put any aura on inside the pull window stand in, read from the
/// <see cref="Combatants"/> registry so control and utility debuffs widen the estimate beyond the three
/// tracked effects, floored at a reference pack size so coverage stays comparable across pulls.
/// </remarks>
[ForPull(PullKind.Multi)]
[Uses<Combatants>]
public sealed partial class MaraDotSpreadAnalyzer : Analyzer, IMaraDotAnalyzer
{
    private const int UnknownRosterReference = 3;

    private readonly HashSet<(int TargetId, int TargetInstance)> _hemorrhageTargets = [];
    private readonly HashSet<(int TargetId, int TargetInstance)> _seethingPoisonTargets = [];
    private readonly HashSet<(int TargetId, int TargetInstance)> _volatilePoisonTargets = [];

    private int? _enemiesEngaged;

    /// <summary>Distinct enemies that took the Hemorrhage bleed from the player.</summary>
    public int HemorrhageTargets => _hemorrhageTargets.Count;

    /// <summary>Fresh Hemorrhage applications during the pull (reapplications excluded).</summary>
    public int HemorrhageApplications { get; private set; }

    /// <summary>Distinct enemies that took the Seething Poison from the player.</summary>
    public int SeethingPoisonTargets => _seethingPoisonTargets.Count;

    /// <summary>Distinct enemies that took the Volatile Poison from the player.</summary>
    public int VolatilePoisonTargets => _volatilePoisonTargets.Count;

    /// <summary>Fresh Volatile Poison applications during the pull (reapplications excluded).</summary>
    public int VolatilePoisonApplications { get; private set; }

    /// <summary>Volatile Poison reapplications, each one an eruption cancelled before it went off.</summary>
    public int VolatilePoisonRefreshes { get; private set; }

    /// <summary>The pull's reported enemy roster size; zero when the roster was not reported.</summary>
    public int TargetCount => Pull.TargetCount;

    /// <summary>Distinct enemies the player put any aura on inside the pull window.</summary>
    public int EnemiesEngaged => _enemiesEngaged ??= CountEnemiesEngaged();

    /// <summary>The roster coverage is measured against, falling back to the engaged enemies when none was reported.</summary>
    public int RosterSize => TargetCount > 0 ? TargetCount : Math.Max(EnemiesEngaged, UnknownRosterReference);

    /// <summary>Share of the roster (0-1) that took the Hemorrhage bleed.</summary>
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
