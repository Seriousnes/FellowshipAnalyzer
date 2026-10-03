using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Meiko;
using FellowshipAnalyzer.Core.Events;

using MeikoTalents = FellowshipAnalyzer.Core.Common.Spells.MeikoTalents;

namespace FellowshipAnalyzer.Heroes.Meiko.Modules;

[RequiresTalent(MeikoTalents.GuardiansFluidity)]
[Dependency<SpellUsable>]
[After<SpellUsable>]
public sealed partial class GuardiansFluidityAnalyzer : Analyzer
{
    public const int ReductionMs = 1_000;

    public CooldownReductionResult Reduction { get; private set; }

    [On<CastEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.RisingEarth),
        nameof(Spells.EarthfistBarrage),
        nameof(Spells.RisingStorm),
        nameof(Spells.LashingStormkick),
        nameof(Spells.DoublePalmStrike),
        nameof(Spells.SpiritedVortex),
    })]
    private void OnFinisher(CastEvent castEvent) =>
        Reduction += SpellUsable.ReduceCooldown(Spells.StoneShieldAlt.FSLID, ReductionMs, castEvent.Timestamp);
}
