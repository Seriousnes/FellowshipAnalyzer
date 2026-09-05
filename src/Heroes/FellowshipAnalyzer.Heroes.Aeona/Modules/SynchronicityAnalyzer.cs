using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;
using FellowshipAnalyzer.Core.UI;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;
using FSLID = FellowshipAnalyzer.Core.Common.Spells.FSLID;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>
/// What Synchronicity added over the report: the Chrona its generation half produced below
/// <see cref="Threshold"/>, and the damage its other half produced above it.
/// </summary>
/// <remarks>
/// <para>
/// Registered dungeon-lifetime, so both figures span the whole report and include what arrived between
/// pulls.
/// </para>
/// <para>
/// Fellowship records the Chrona that reached the pool and the damage the target took, both of which
/// already include the talent's increase, so each figure is the increase's share of the recorded amount
/// rather than a further percentage of it.
/// </para>
/// <para>
/// A hit is measured against the Chrona the player held before that hit's own resource snapshot, the
/// same convention <see cref="ChronaGeneratedBelowThreshold"/> applies to a gain.
/// <c>[Before&lt;ChronaTracker&gt;]</c> is what puts this analyzer ahead of the tracker. Before the
/// first Chrona snapshot the amount is zero, which is below <see cref="Threshold"/>, so a hit there
/// contributes nothing.
/// </para>
/// </remarks>
[RequiresTalent(AeonaTalents.Synchronicity)]
[Dependency<Abilities>]
[Dependency<ChronaTracker>]
[Before<ChronaTracker>]
public sealed partial class SynchronicityAnalyzer : Analyzer
{
    private int? _chronaGeneratedBelowThreshold;

    /// <summary>
    /// The share of maximum Chrona below which Synchronicity increases generation. Codex
    /// <c>talent 537</c>, "When you are below 50% Chrona you generate 25% more Chrona".
    /// </summary>
    public const double SynchronicityThresholdShare = 0.5;

    /// <summary>
    /// The generation increase Synchronicity applies below <see cref="SynchronicityThresholdShare"/>.
    /// Codex <c>talent 537</c> and its <c>effect 2733</c>, "Synchronicity: Catch Up - 25% increased
    /// Resource Generation".
    /// </summary>
    public const double SynchronicityGenerationIncrease = 0.25;

    /// <summary>
    /// The damage increase Synchronicity applies above <see cref="SynchronicityThresholdShare"/> to
    /// abilities that do not spend Chrona. Codex <c>talent 537</c> and its <c>effect 2596</c>,
    /// "Synchronicity: Overcharge - 15% increased damage".
    /// </summary>
    public const double SynchronicityDamageIncrease = 0.15;

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Talents;

    /// <summary>
    /// The Chrona amount Synchronicity's two halves divide at, as
    /// <see cref="SynchronicityThresholdShare"/> of the maximum Chrona observed.
    /// </summary>
    public int Threshold =>
        (int)(ChronaTracker.MaxOf(ResourceTypes.Primary) * SynchronicityThresholdShare);

    /// <summary>
    /// Chrona that arrived over the report while the pool held less than <see cref="Threshold"/>.
    /// </summary>
    public int ChronaGeneratedBelowThreshold =>
        _chronaGeneratedBelowThreshold ??= MeasureGenerationBelowThreshold();

    /// <summary>
    /// Damage the player dealt over the report with abilities that do not spend Chrona, while the pool
    /// held more than <see cref="Threshold"/>.
    /// </summary>
    public long DamageAboveThreshold { get; private set; }

    /// <summary>The share of <see cref="ChronaGeneratedBelowThreshold"/> Synchronicity produced.</summary>
    public double EstimatedChrona =>
        ChronaGeneratedBelowThreshold
        * (SynchronicityGenerationIncrease / (1 + SynchronicityGenerationIncrease));

    /// <summary>The share of <see cref="DamageAboveThreshold"/> Synchronicity produced.</summary>
    public double EstimatedDamage =>
        DamageAboveThreshold
        * (SynchronicityDamageIncrease / (1 + SynchronicityDamageIncrease));

    [On<DamageEvent>(By = Actor.Player)]
    private void OnPlayerDamage(DamageEvent e)
    {
        if (SpendsChrona(e.Ability.FSLID)) return;
        if (ChronaTracker.AmountAt(ResourceTypes.Primary, e.Timestamp) <= Threshold) return;

        DamageAboveThreshold += e.Amount;
    }

    /// <summary>
    /// Whether <paramref name="abilityId"/> belongs to an ability that spends Chrona, which is an
    /// ability with a <see cref="ResourceTypes.Primary"/> entry in its generated <c>Costs</c>. Every
    /// Aeona Chrona cost in <c>data/spelldb.json</c> is zero, so the test is the entry's presence rather
    /// than its amount. A damage effect resolves back to its ability through
    /// <see cref="Core.Analysis.Abilities.GetAbility"/>, which keys the spellbook by an entry's
    /// additional spells as well as its primary.
    /// </summary>
    private bool SpendsChrona(FSLID abilityId) =>
        Abilities.GetAbility(abilityId)?.PrimarySpell.Cost(ResourceTypes.Primary) is not null;

    private int MeasureGenerationBelowThreshold()
    {
        var threshold = Threshold;
        var total = 0;

        foreach (var change in ChronaTracker.EventsBetween(
            ResourceTypes.Primary, Owner.DungeonStartTime, Owner.DungeonEndTime))
        {
            if (change.Kind != ResourceEventKind.Gain) continue;
            if (change.CurrentAfter - change.Amount >= threshold) continue;

            total += change.Amount;
        }

        return total;
    }
}
