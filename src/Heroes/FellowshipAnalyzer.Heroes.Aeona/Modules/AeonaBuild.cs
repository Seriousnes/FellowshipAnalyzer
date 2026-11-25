using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// The equipped legendary and every number it changes. Registered dungeon-lifetime; every analyzer and
/// guide reads its build questions here rather than comparing item ids.
/// </summary>
/// <remarks>
/// The base figures are the codex records: Entropy's Claim (ability 1876) lasts 6 s and casts in 1.5 s;
/// Unfolding Doom (ability 1890) lasts 20 s. Mass Entropy (power 620) adds the charge and duration of the
/// Entropy's Claim <c>Charged</c> tuning rows; Chrono Trigger (power 618) sets Unfolding Doom to 15 s.
/// Lonesome Song (power 619) adds Converging Timelines effect 2820, which
/// <see cref="ConvergingTimelinesTracker"/> joins to the Cooldown Recovery attribute.
/// </remarks>
public sealed class AeonaBuild : Module
{
    private const int BaseEntropyClaimDurationMs = 6_000;
    private const int BaseUnfoldingDoomDurationMs = 20_000;
    private const int ChronoTriggerUnfoldingDoomDurationMs = 15_000;

    private static readonly int MassEntropyDurationBonusMs = (int)(SpellTuning.EntropyClaim.Charged_AdditionalDuration * 1000);
    private static readonly int BaseEntropyClaimCharges = Spells.EntropyClaim.Charges;
    private static readonly int MassEntropyCharges = (int)SpellTuning.EntropyClaim.Charged_MaxCharges;
    private static readonly int EntropyClaimCastMs = (int)((Spells.EntropyClaim.CastDuration ?? 0) * 1000);

    /// <summary>Whether Mass Entropy is equipped.</summary>
    public bool MassEntropy => Owner.SelectedCombatant.HasLegendary(Legendaries.MassEntropy.Id);

    /// <summary>Whether Chrono Trigger is equipped.</summary>
    public bool ChronoTrigger => Owner.SelectedCombatant.HasLegendary(Legendaries.ChronoTrigger.Id);

    /// <summary>Whether Lonesome Song is equipped.</summary>
    public bool LonesomeSong => Owner.SelectedCombatant.HasLegendary(Legendaries.LonesomeSong.Id);

    /// <summary>Maximum charges of Entropy's Claim.</summary>
    public int EntropyClaimCharges => MassEntropy ? MassEntropyCharges : BaseEntropyClaimCharges;

    /// <summary>Duration of one Entropy's Claim application.</summary>
    public int EntropyClaimDurationMs => BaseEntropyClaimDurationMs + (MassEntropy ? MassEntropyDurationBonusMs : 0);

    /// <summary>Entropy's Claim's cast time.</summary>
    public int EntropyClaimCastTimeMs => EntropyClaimCastMs;

    /// <summary>Duration of one Unfolding Doom application.</summary>
    public int UnfoldingDoomDurationMs => ChronoTrigger ? ChronoTriggerUnfoldingDoomDurationMs : BaseUnfoldingDoomDurationMs;
}
