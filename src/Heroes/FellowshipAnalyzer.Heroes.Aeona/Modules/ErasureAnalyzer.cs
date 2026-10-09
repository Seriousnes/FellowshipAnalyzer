using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// What Erasure produced over the report: its ticks' damage, and the healing and Oblivion's Embrace
/// shielding linked to them, beside the same figures for Oblivion's direct hits.
/// </summary>
/// <remarks>
/// <para>
/// Registered dungeon-lifetime, so every figure spans the whole report, including between pulls.
/// </para>
/// <para>
/// Erasure's heals carry the Oblivion ability. A tick's heals and shields are the ones
/// <see cref="Normalizers.HitLinkNormalizer"/> linked to that tick's damage.
/// </para>
/// </remarks>
[RequiresTalent(AeonaTalents.Erasure)]
public sealed partial class ErasureAnalyzer : Analyzer
{
    private long _shieldApplied;
    private long _directShieldApplied;

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Talents;

    /// <summary>Whether the build has Oblivion's Embrace.</summary>
    public bool OblivionsEmbraceTalented => Owner.SelectedCombatant.HasTalent(AeonaTalents.OblivionsEmbrace);

    /// <summary>Erasure ticks over the report.</summary>
    public int Ticks { get; private set; }

    /// <summary>Damage Erasure's ticks dealt.</summary>
    public long Damage { get; private set; }

    /// <summary>Effective healing Erasure's ticks did.</summary>
    public long EffectiveHealing { get; private set; }

    /// <summary>Overheal from Erasure's ticks.</summary>
    public long Overheal { get; private set; }

    /// <summary>Absorb Erasure's ticks added to shields. Null without Oblivion's Embrace.</summary>
    public long? ShieldApplied => OblivionsEmbraceTalented ? _shieldApplied : null;

    /// <summary>Damage Oblivion's direct hits dealt.</summary>
    public long DirectDamage { get; private set; }

    /// <summary>Effective healing Oblivion's direct hits did.</summary>
    public long DirectEffectiveHealing { get; private set; }

    /// <summary>Absorb Oblivion's direct hits added to shields. Null without Oblivion's Embrace.</summary>
    public long? DirectShieldApplied => OblivionsEmbraceTalented ? _directShieldApplied : null;

    /// <summary>Erasure's share (0-1) of Oblivion's damage, Erasure's included.</summary>
    public double DamageShare => Share(Damage, DirectDamage);

    /// <summary>Erasure's share (0-1) of Oblivion's effective healing, Erasure's included.</summary>
    public double HealingShare => Share(EffectiveHealing, DirectEffectiveHealing);

    /// <summary>Erasure's share (0-1) of Oblivion's shield absorb, Erasure's included. Null without Oblivion's Embrace.</summary>
    public double? ShieldShare => OblivionsEmbraceTalented ? Share(_shieldApplied, _directShieldApplied) : null;

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.Erasure))]
    private void OnTick(DamageEvent e)
    {
        var hit = HitYield.Of(e);
        Ticks++;
        Damage += e.Amount;
        EffectiveHealing += hit.EffectiveHealing;
        Overheal += hit.Overheal;
        _shieldApplied += hit.ShieldApplied;
    }

    [On<DamageEvent>(By = Actor.Player, Spells = [nameof(Spells.Oblivion), nameof(Spells.OblivionDamage)])]
    private void OnDirectHit(DamageEvent e)
    {
        var hit = HitYield.Of(e);
        DirectDamage += e.Amount;
        DirectEffectiveHealing += hit.EffectiveHealing;
        _directShieldApplied += hit.ShieldApplied;
    }

    private static double Share(long part, long rest) => part + rest > 0 ? (double)part / (part + rest) : 0;
}
