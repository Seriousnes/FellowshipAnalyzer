using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>
/// Life Petal's two charges and the Nettlebolt loop that refills them. Every Nettlebolt that lands
/// takes a second and a half off Life Petal's recharge and a crit takes three, so the loop only turns
/// while a charge is actually recharging - reduction generated at two charges is thrown away, and the
/// two figures are separated here rather than summed.
/// <para>
/// Charge overcap is the other half: time spent holding both charges is time Life Petal was not out,
/// measured against the game's own two-charge ceiling rather than against another pull's usage.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
public sealed partial class LifePetalAnalyzer : Analyzer
{
    private int _lastChargeSample;
    private int _chargesHeld = SylvieKit.LifePetalCharges;
    private bool _seeded;

    /// <summary>Life Petals cast this pull.</summary>
    public int Casts { get; private set; }

    /// <summary>Nettlebolts that landed this pull, each of which drives the reduction.</summary>
    public int NettleboltHits { get; private set; }

    /// <summary>Nettlebolts that crit this pull, each of which drives double the reduction.</summary>
    public int NettleboltCrits { get; private set; }

    /// <summary>The Life Petal reduction the Nettlebolts generated, and how much of it shortened a running recharge.</summary>
    public CooldownReductionResult CooldownReduction { get; private set; } = new();

    /// <summary>Milliseconds spent holding both charges, where the loop had nothing to shorten.</summary>
    public int ChargeCappedMs { get; private set; }

    /// <summary>Milliseconds of charge readings taken, the denominator <see cref="ChargeCappedMs"/> is a share of.</summary>
    public int MeasuredMs { get; private set; }

    /// <summary>Share (0-1) of measured time spent holding both charges.</summary>
    public double ChargeCappedShare => MeasuredMs > 0 ? ChargeCappedMs / (double)MeasuredMs : 0;

    /// <summary>Nettlebolts that landed while both charges were already banked.</summary>
    public int NettleboltsAtFullCharges { get; private set; }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.LifePetal))]
    private void OnLifePetalCast(CastEvent castEvent)
    {
        Casts++;
        SampleCharges(castEvent.Timestamp);
    }

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.Nettlebolt))]
    private void OnNettleboltLanded(DamageEvent damageEvent)
    {
        NettleboltHits++;

        var crit = damageEvent.HitType == HitType.Crit;
        if (crit) NettleboltCrits++;

        var request = crit ? SylvieKit.NettleboltCritCooldownReductionMs : SylvieKit.NettleboltCooldownReductionMs;
        var atFullCharges = SpellUsable.ChargesAvailable(Spells.LifePetal.FSLID) >= SylvieKit.LifePetalCharges;
        if (atFullCharges) NettleboltsAtFullCharges++;

        CooldownReduction += SpellUsable.ReduceCooldown(Spells.LifePetal.FSLID, request, damageEvent.Timestamp);

        SampleCharges(damageEvent.Timestamp);
    }

    [On<PullEndEvent>]
    private void OnPullEnd(PullEndEvent pullEndEvent) => SampleCharges(pullEndEvent.Timestamp);

    private void SampleCharges(int timestamp)
    {
        if (_seeded)
        {
            var elapsed = timestamp - _lastChargeSample;
            if (elapsed > 0)
            {
                MeasuredMs += elapsed;
                if (_chargesHeld >= SylvieKit.LifePetalCharges) ChargeCappedMs += elapsed;
            }
        }

        _seeded = true;
        _lastChargeSample = timestamp;
        _chargesHeld = SpellUsable.ChargesAvailable(Spells.LifePetal.FSLID);
    }
}
