using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// What Entropic Burst produced over the report: its ticks' damage and the healing linked to them, beside
/// the same figures for Entropy's Claim's own ticks.
/// </summary>
/// <remarks>
/// <para>
/// Registered dungeon-lifetime, so every figure spans the whole report, including between pulls.
/// </para>
/// <para>
/// Entropic Burst's heals carry the Entropy's Claim ability. A tick's heals are the ones
/// <see cref="Normalizers.HitLinkNormalizer"/> linked to that tick's damage.
/// </para>
/// </remarks>
[RequiresTalent(AeonaTalents.EntropicBurst)]
public sealed partial class EntropicBurstAnalyzer : Analyzer
{
    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Talents;

    /// <summary>Entropic Burst ticks over the report.</summary>
    public int Ticks { get; private set; }

    /// <summary>Damage Entropic Burst's ticks dealt.</summary>
    public long Damage { get; private set; }

    /// <summary>Effective healing Entropic Burst's ticks did.</summary>
    public long EffectiveHealing { get; private set; }

    /// <summary>Overheal from Entropic Burst's ticks.</summary>
    public long Overheal { get; private set; }

    /// <summary>Damage Entropy's Claim's own ticks dealt.</summary>
    public long EntropyClaimDamage { get; private set; }

    /// <summary>Effective healing Entropy's Claim's own ticks did.</summary>
    public long EntropyClaimEffectiveHealing { get; private set; }

    /// <summary>Entropic Burst's share (0-1) of Entropy's Claim's damage, Entropic Burst's included.</summary>
    public double DamageShare => Share(Damage, EntropyClaimDamage);

    /// <summary>Entropic Burst's share (0-1) of Entropy's Claim's effective healing, Entropic Burst's included.</summary>
    public double HealingShare => Share(EffectiveHealing, EntropyClaimEffectiveHealing);

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.EntropicBurst))]
    private void OnTick(DamageEvent e)
    {
        var hit = HitYield.Of(e);
        Ticks++;
        Damage += e.Amount;
        EffectiveHealing += hit.EffectiveHealing;
        Overheal += hit.Overheal;
    }

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.EntropyClaimDot))]
    private void OnEntropyClaimTick(DamageEvent e)
    {
        EntropyClaimDamage += e.Amount;
        EntropyClaimEffectiveHealing += HitYield.Of(e).EffectiveHealing;
    }

    private static double Share(long part, long rest) => part + rest > 0 ? (double)part / (part + rest) : 0;
}
