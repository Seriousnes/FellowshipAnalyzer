using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
public sealed partial class LifePetalAnalyzer : Analyzer
{
    private int _lastChargeSample;
    private int _chargesHeld = SylvieKit.LifePetalCharges;
    private bool _seeded;

    public int Casts { get; private set; }

    public int NettleboltHits { get; private set; }

    public int NettleboltCrits { get; private set; }

    public CooldownReductionResult CooldownReduction { get; private set; } = new();

    public int ChargeCappedMs { get; private set; }

    public int MeasuredMs { get; private set; }

    public double ChargeCappedShare => MeasuredMs > 0 ? ChargeCappedMs / (double)MeasuredMs : 0;

    public int NettleboltsAtFullCharges { get; private set; }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.LifePetal))]
    private void OnLifePetalCast(CastEvent castEvent)
    {
        Casts++;
        SampleCharges(castEvent.Timestamp);
    }

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.Nettlebolt))]
    private void OnNettleboltHit(DamageEvent damageEvent)
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
