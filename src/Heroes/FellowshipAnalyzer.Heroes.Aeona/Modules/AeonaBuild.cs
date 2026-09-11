using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// The equipped legendary and every number it changes. Registered dungeon-lifetime; every analyzer and
/// guide reads its build questions here rather than comparing item ids.
/// </summary>
/// <remarks>
/// The base figures are the codex records: Entropy's Claim (ability 1876) lasts 6 s and casts in 1.5 s;
/// Unfolding Doom (ability 1890) lasts 20 s; Converging Timelines (effect 3266) grants +100% Cooldown
/// Acceleration. The legendary powers state their changes as text: Mass Entropy (power 620) adds a
/// charge and 2 s; Chrono Trigger (power 618) sets Unfolding Doom to 15 s; Lonesome Song (power 619)
/// raises Oblivion's Converging Timelines to +200%.
/// </remarks>
public sealed class AeonaBuild : Module
{
    private const int BaseEntropyClaimDurationMs = 6_000;
    private const int MassEntropyDurationBonusMs = 2_000;
    private const int BaseEntropyClaimCharges = 1;
    private const int MassEntropyCharges = 2;
    private const int EntropyClaimCastMs = 1_500;
    private const int BaseUnfoldingDoomDurationMs = 20_000;
    private const int ChronoTriggerUnfoldingDoomDurationMs = 15_000;
    private const double ConvergingTimelinesAcceleration = 1.0;
    private const double LonesomeSongAcceleration = 2.0;

    /// <summary>The equipped legendary as the registry names it, or null when none of Aeona's legendaries is equipped.</summary>
    public Item? Legendary => field ??= Resolve();

    /// <summary>Whether Mass Entropy is equipped.</summary>
    public bool MassEntropy => Legendary == Legendaries.MassEntropy;

    /// <summary>Whether Chrono Trigger is equipped.</summary>
    public bool ChronoTrigger => Legendary == Legendaries.ChronoTrigger;

    /// <summary>Whether Lonesome Song is equipped.</summary>
    public bool LonesomeSong => Legendary == Legendaries.LonesomeSong;

    /// <summary>Charges Entropy's Claim holds.</summary>
    public int EntropyClaimCharges => MassEntropy ? MassEntropyCharges : BaseEntropyClaimCharges;

    /// <summary>How long one Entropy's Claim application runs.</summary>
    public int EntropyClaimDurationMs => BaseEntropyClaimDurationMs + (MassEntropy ? MassEntropyDurationBonusMs : 0);

    /// <summary>Entropy's Claim's cast time.</summary>
    public int EntropyClaimCastTimeMs => EntropyClaimCastMs;

    /// <summary>How long one Unfolding Doom application runs.</summary>
    public int UnfoldingDoomDurationMs => ChronoTrigger ? ChronoTriggerUnfoldingDoomDurationMs : BaseUnfoldingDoomDurationMs;

    /// <summary>The Cooldown Acceleration an Oblivion cast grants through Converging Timelines.</summary>
    public double ConvergingTimelinesOnOblivion => LonesomeSong ? LonesomeSongAcceleration : ConvergingTimelinesAcceleration;

    /// <summary>The Cooldown Acceleration an Amend Fate or Restore Continuity cast grants through Converging Timelines.</summary>
    public double ConvergingTimelinesOnCleanse => ConvergingTimelinesAcceleration;

    private Item? Resolve()
    {
        var equipped = Owner.SelectedCombatant.Legendary?.Id;
        if (equipped is null) return null;

        if (equipped == Legendaries.MassEntropy.Id) return Legendaries.MassEntropy;
        if (equipped == Legendaries.ChronoTrigger.Id) return Legendaries.ChronoTrigger;
        if (equipped == Legendaries.LonesomeSong.Id) return Legendaries.LonesomeSong;

        return null;
    }
}
