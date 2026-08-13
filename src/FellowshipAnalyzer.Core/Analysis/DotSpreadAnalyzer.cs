using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Measures how far each of a set of damage-over-time effects spread across a pack: how many enemies
/// took it, how often it was applied fresh, and how often it was laid again over a window that was
/// still running.
/// <para>
/// Coverage is read against the enemies <see cref="Enemies.Roster"/> names for the pull. When it
/// names none, the roster falls back to the enemies the player engaged at all, which is a truer
/// denominator than the enemies that took the effect, and finally to
/// <see cref="UnknownRosterReference"/> so a pull with no roster at all still reports something
/// bounded.
/// </para>
/// <para>
/// Derive a per-hero analyzer from this, keep <c>[ForPull]</c> and the surface marker interface on
/// the leaf, and declare the effects' own <c>[On&lt;&gt;]</c> handlers there: apply calls
/// <see cref="RecordApplication"/> and refresh calls <see cref="RecordRefresh"/>. Each files the
/// event under whichever effect in <see cref="Dots"/> its ability id names, so an event for an
/// untracked ability is ignored.
/// </para>
/// </summary>
public abstract class DotSpreadAnalyzer : Analyzer
{
    /// <summary>The roster assumed for a pull that reports no target count and no engaged enemies.</summary>
    public const int UnknownRosterReference = 3;

    private readonly Dictionary<int, Spread> _spreads = [];

    /// <summary>The effects this analyzer measures, in the order results are reported.</summary>
    protected abstract IReadOnlyList<Dot> Dots { get; }

    /// <summary>The enemies the pull's roster names, or zero when it names none.</summary>
    public int TargetCount => Owner.Enemies?.Roster(Pull).Count ?? 0;

    private int? _enemiesEngaged;

    /// <summary>Enemies that carried any aura from the player during this pull.</summary>
    public int EnemiesEngaged => _enemiesEngaged ??= CountEnemiesEngaged();

    /// <summary>The denominator <see cref="CoverageOf"/> is read against.</summary>
    public int RosterSize => TargetCount > 0 ? TargetCount : Math.Max(EnemiesEngaged, UnknownRosterReference);

    /// <summary>Enemies that took <paramref name="dot"/> at some point during the pull.</summary>
    public int TargetsOf(Dot dot) => Lookup(dot)?.Targets.Count ?? 0;

    /// <summary>Fresh applications of <paramref name="dot"/> during the pull.</summary>
    public int ApplicationsOf(Dot dot) => Lookup(dot)?.Applications ?? 0;

    /// <summary>
    /// Times <paramref name="dot"/> was laid again over a window that had not ended. What that costs
    /// depends on the effect: an <see cref="StackMethod.Accumulate"/> pool forfeits nothing, where an
    /// effect that pays out as it expires loses the payout.
    /// </summary>
    public int RefreshesOf(Dot dot) => Lookup(dot)?.Refreshes ?? 0;

    /// <summary>Share of <see cref="RosterSize"/> (0-1) that took <paramref name="dot"/>.</summary>
    public double CoverageOf(Dot dot) =>
        RosterSize == 0 ? 0d : Math.Min(1d, TargetsOf(dot) / (double)RosterSize);

    /// <summary>Records a fresh application of whichever effect the event names. Call from apply handlers.</summary>
    protected void RecordApplication<TEvent>(TEvent e) where TEvent : Event, IAbilityEvent, IHasTargetWithInstanceEvent
    {
        if (SpreadFor(e.Ability.Id) is not { } spread) return;

        spread.Targets.Add(AuraWindowLedger.KeyOf(e));
        spread.Applications++;
    }

    /// <summary>Records a re-application of whichever effect the event names. Call from refresh handlers.</summary>
    protected void RecordRefresh<TEvent>(TEvent e) where TEvent : Event, IAbilityEvent, IHasTargetWithInstanceEvent
    {
        if (SpreadFor(e.Ability.Id) is not { } spread) return;

        spread.Targets.Add(AuraWindowLedger.KeyOf(e));
        spread.Refreshes++;
    }

    private Spread? Lookup(Dot dot) => _spreads.GetValueOrDefault(dot.EffectId);

    private Spread? SpreadFor(int abilityId)
    {
        foreach (var dot in Dots)
        {
            if (dot.EffectId != abilityId) continue;

            if (!_spreads.TryGetValue(abilityId, out var spread))
                _spreads[abilityId] = spread = new Spread();
            return spread;
        }
        return null;
    }

    private int CountEnemiesEngaged()
    {
        if (Owner.Combatants is not { } combatants) return 0;

        var engaged = 0;
        foreach (var enemy in combatants.Units.Values.OfType<Enemy>())
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

    private sealed class Spread
    {
        public HashSet<UnitKey> Targets { get; } = [];

        public int Applications { get; set; }

        public int Refreshes { get; set; }
    }
}
