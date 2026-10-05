using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;

using HelenaTalents = FellowshipAnalyzer.Core.Common.Spells.HelenaTalents;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>
/// High Command: every Shields Up cast takes 6 seconds off Hold the Line's cooldown. The reduction is applied
/// to <see cref="SpellUsable"/> at the cast, so Hold the Line's availability elsewhere in the analysis
/// follows the talent.
/// </summary>
[RequiresTalent(HelenaTalents.HighCommand)]
[Dependency<SpellUsable>]
public sealed partial class HighCommandAnalyzer : Analyzer
{
    /// <summary>The Hold the Line cooldown one Shields Up cast removes.</summary>
    public const int HoldTheLineReductionMs = 6_000;

    /// <summary>Shields Up casts across the dungeon.</summary>
    public int ShieldsUpCasts { get; private set; }

    /// <summary>The Hold the Line cooldown reduction Shields Up generated, and how much of it shortened a running cooldown.</summary>
    public CooldownReductionResult CooldownReduction { get; private set; }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.ShieldsUp))]
    private void OnShieldsUp(CastEvent castEvent)
    {
        ShieldsUpCasts++;
        CooldownReduction += SpellUsable.ReduceCooldown(
            Spells.HoldTheLine.FSLID, HoldTheLineReductionMs, castEvent.Timestamp);
    }
}
