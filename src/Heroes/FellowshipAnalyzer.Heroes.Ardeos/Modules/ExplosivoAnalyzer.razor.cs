using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Apocalypse cooldown reduction from the Explosivo legendary (power 615). Each Fire Ball cast reduces the
/// cooldown of Apocalypse by 8 seconds.
/// </summary>
[ActiveWhen<HasExplosivo>]
public sealed partial class ExplosivoAnalyzer : Analyzer
{
    /// <summary>Apocalypse cooldown reduction per Fire Ball cast, in milliseconds.</summary>
    public const int ApocalypseReductionPerFireBallMs = 8000;

    public override StatisticCategory StatisticCategory => StatisticCategory.Items;

    /// <summary>Apocalypse cooldown progress from Fire Ball casts, in milliseconds.</summary>
    public CooldownReductionResult CooldownReduction { get; private set; } = new();

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.FireBall))]
    private void OnFireBallCast(CastEvent e) =>
        CooldownReduction += Owner.SpellUsable!.ReduceCooldown(Spells.Apocalypse.Id, ApocalypseReductionPerFireBallMs, e.Timestamp);
}

/// <summary>Active when the selected combatant has the Explosivo legendary equipped.</summary>
public sealed class HasExplosivo : IModuleActivePredicate
{
    public static bool IsActive(ParseContext context) => context.SelectedCombatant.HasLegendary(Legendaries.Explosivo.Id);
}
