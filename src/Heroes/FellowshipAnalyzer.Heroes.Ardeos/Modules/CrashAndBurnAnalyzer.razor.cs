using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

using ArdeosTalents = FellowshipAnalyzer.Core.Common.Spells.ArdeosTalents;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

[RequiresTalent(ArdeosTalents.CrashAndBurn)]
public sealed partial class CrashAndBurnAnalyzer : Analyzer
{
    private const int SEARING_BLAZE_CDR = 50;

    public override StatisticCategory StatisticCategory => StatisticCategory.Talents;

    public CooldownReductionResult CooldownReduction { get; private set; } = new();

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlazeDot))]
    public void OnSearingBlazeDamage() =>
        CooldownReduction += Owner.SpellUsable!.ReduceCooldown(Spells.FireBall.Id, SEARING_BLAZE_CDR);
}
