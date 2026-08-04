using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

public sealed partial class SalvoTracker : EventSubscriber
{
    public int SalvoHits { get; private set; }

    public int EruptHits { get; private set; }

    public long SalvoDamage { get; private set; }

    public long EruptDamage { get; private set; }

    public long TotalPlayerDamage { get; private set; }

    public int TotalHits => SalvoHits + EruptHits;

    public long TotalMarkDamage => SalvoDamage + EruptDamage;

    public double DamageShare =>
        TotalPlayerDamage == 0 ? 0d : TotalMarkDamage / (double)TotalPlayerDamage;

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
