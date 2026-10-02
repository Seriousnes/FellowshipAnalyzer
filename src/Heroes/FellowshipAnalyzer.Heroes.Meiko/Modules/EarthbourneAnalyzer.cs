using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Meiko;
using FellowshipAnalyzer.Core.Events;

using MeikoTalents = FellowshipAnalyzer.Core.Common.Spells.MeikoTalents;

namespace FellowshipAnalyzer.Heroes.Meiko.Modules;

[RequiresTalent(MeikoTalents.Earthbourne)]
[Dependency<SpellUsable>]
[After<SpellUsable>]
public sealed partial class EarthbourneAnalyzer : Analyzer
{
    public const double ReductionShare = 0.2;

    public static int ReductionMs => (int)Math.Round((Spells.TwinSoulsBulwark.Cooldown ?? 0) * 1000 * ReductionShare);

    public CooldownReductionResult Reduction { get; private set; }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.ShatterEarth))]
    private void OnShatterEarth(CastEvent castEvent) =>
        Reduction += SpellUsable.ReduceCooldown(Spells.TwinSoulsBulwark.FSLID, ReductionMs, castEvent.Timestamp);
}
