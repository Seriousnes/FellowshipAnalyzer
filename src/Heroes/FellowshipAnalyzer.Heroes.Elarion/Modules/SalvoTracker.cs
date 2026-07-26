using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.Heroes.Elarion.Statistics;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Totals the damage Lunarlight Mark paid out across the whole fight. While an enemy carries the
/// mark, the player's ability hits can trigger Lunarlight Salvo (<see cref="Spells.LunarlightMarkDamage"/>)
/// and its area variant Lunarlight Salvo: Erupt (<see cref="Spells.LunarlightMarkAoeDamage"/>) as bonus
/// damage. Both land as ordinary player damage events, so they are also inside
/// <see cref="TotalPlayerDamage"/> and <see cref="DamageShare"/> reads as a share of the player's own
/// output. The card is withheld when no Salvo landed.
/// </summary>
public sealed partial class SalvoTracker : EventSubscriber
{
    /// <summary>Lunarlight Salvo hits observed during the fight.</summary>
    public int SalvoHits { get; private set; }

    /// <summary>Lunarlight Salvo: Erupt hits observed during the fight.</summary>
    public int EruptHits { get; private set; }

    /// <summary>Damage dealt by Lunarlight Salvo.</summary>
    public long SalvoDamage { get; private set; }

    /// <summary>Damage dealt by Lunarlight Salvo: Erupt.</summary>
    public long EruptDamage { get; private set; }

    /// <summary>All damage the player dealt during the fight, Salvo and Erupt included.</summary>
    public long TotalPlayerDamage { get; private set; }

    /// <summary>Salvo and Erupt hits combined.</summary>
    public int TotalHits => SalvoHits + EruptHits;

    /// <summary>Salvo and Erupt damage combined.</summary>
    public long TotalMarkDamage => SalvoDamage + EruptDamage;

    /// <summary>Share of the player's damage (0-1) that came from the mark.</summary>
    public double DamageShare =>
        TotalPlayerDamage == 0 ? 0d : TotalMarkDamage / (double)TotalPlayerDamage;

    public override Type? StatisticsComponentType => TotalHits > 0 ? typeof(SalvoStatistics) : null;

    public override StatisticCategory StatisticCategory => StatisticCategory.General;

    [On<DamageEvent>(By = Actor.Player)]
    private void OnPlayerDamage(DamageEvent e) => TotalPlayerDamage += e.Amount;

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.LunarlightMarkDamage))]
    private void OnSalvoDamage(DamageEvent e)
    {
        SalvoHits++;
        SalvoDamage += e.Amount;
    }

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.LunarlightMarkAoeDamage))]
    private void OnEruptDamage(DamageEvent e)
    {
        EruptHits++;
        EruptDamage += e.Amount;
    }
}
