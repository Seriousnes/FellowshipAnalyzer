using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

/// <summary>
/// The target shape a <see cref="MaraResourceDisciplineAnalyzer"/> leaf scores against, mirroring
/// the pull's <see cref="PullKind"/>. Used by the guide to label single-target vs AoE pulls.
/// </summary>
public enum MaraPullShape
{
    SingleTarget,
    Aoe,
}

/// <summary>
/// One combo-point-spending finisher cast (Queen's Fang or Arachnid Assault) with the combo points
/// available when it was pressed and whether that met the pull shape's spend threshold.
/// <see cref="MeetsThreshold"/> is stamped when the pull ends.
/// </summary>
public sealed record MaraFinisherCast(
    int Timestamp,
    int AbilityId,
    int ComboPoints,
    bool MeetsThreshold);

/// <summary>
/// Tracks Mara's resource discipline for a pull: how consistently combo-point-spending finishers
/// (Queen's Fang, Arachnid Assault) are dumped at or above the pull shape's combo-point threshold,
/// and how often Energy or combo points overcap (wasted generation).
/// <para>
/// The metric is build-agnostic: every Mara build shares the same generators (Backstab, Widow's Bite,
/// Skittering Blades) and finishers, so keying on those symbols and on the two class resources
/// (Energy = <see cref="ResourceTypes.Primary"/>, Combo Points = <see cref="ResourceTypes.Secondary"/>)
/// works regardless of talent choice. Resource values are read from each cast's
/// <see cref="Event.SourceResources"/> snapshot, which is the amount available before the cast
/// resolves (a finisher pressed at 6 combo points reports 6, then the next event reports 0).
/// </para>
/// Hemorrhaging Strike is a finisher too, but it is pressed to maintain bleeds and Hemotoxin rather
/// than purely to dump combo points, so it is counted separately and excluded from the threshold rate.
/// </summary>
public abstract partial class MaraResourceDisciplineAnalyzer : Analyzer
{
    private static readonly int[] Generators =
        [Spells.Backstab.Id, Spells.WidowBite.Id, Spells.SkitteringBlades.Id];

    private static readonly int[] ScoredFinishers =
        [Spells.QueenFang.Id, Spells.ArachnidAssault.Id];

    private readonly List<MaraFinisherCast> _finishers = [];

    /// <summary>The combo-point count at or above which a spending finisher is well-timed for this pull shape.</summary>
    public abstract int FinisherCpThreshold { get; }

    /// <summary>The pull shape this leaf scores against.</summary>
    public abstract MaraPullShape Shape { get; }

    /// <summary>Queen's Fang and Arachnid Assault casts that carried a combo-point snapshot, in encounter order.</summary>
    public IReadOnlyList<MaraFinisherCast> Finishers => _finishers;

    /// <summary>Spending finishers cast at or above <see cref="FinisherCpThreshold"/> combo points.</summary>
    public int FinishersAtThreshold { get; private set; }

    /// <summary>Hemorrhaging Strike casts, pressed for bleed and Hemotoxin upkeep rather than to dump combo points.</summary>
    public int MaintenanceFinisherCasts { get; private set; }

    public int GeneratorCasts { get; private set; }

    /// <summary>Generator casts made while already at maximum combo points (wasted generation).</summary>
    public int GeneratorOvercapCasts { get; private set; }

    /// <summary>Casts that carried an Energy snapshot with a known maximum.</summary>
    public int EnergyCastsSampled { get; private set; }

    /// <summary>Sampled casts made while Energy was at cap (Energy regenerated while capped is wasted).</summary>
    public int EnergyCappedCasts { get; private set; }

    /// <summary>Fraction of evaluated spending finishers cast at or above the threshold.</summary>
    public double FinisherThresholdRate => _finishers.Count == 0 ? 0 : (double)FinishersAtThreshold / _finishers.Count;

    /// <summary>Fraction of sampled casts made at capped Energy.</summary>
    public double EnergyCapRate => EnergyCastsSampled == 0 ? 0 : (double)EnergyCappedCasts / EnergyCastsSampled;

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent castEvent)
    {
        var resources = castEvent.SourceResources?.Resources;
        if (resources is null || resources.Count == 0)
            return;

        var energy = FindResource(resources, ResourceTypes.Primary);
        if (energy is { Max: > 0 })
        {
            EnergyCastsSampled++;
            if (energy.Amount >= energy.Max)
                EnergyCappedCasts++;
        }

        var comboPoints = FindResource(resources, ResourceTypes.Secondary);
        var abilityId = castEvent.Ability.Id;

        if (Array.IndexOf(Generators, abilityId) >= 0)
        {
            GeneratorCasts++;
            if (comboPoints is { Max: > 0 } && comboPoints.Amount >= comboPoints.Max)
                GeneratorOvercapCasts++;
            return;
        }

        if (abilityId == Spells.HemorrhagingStrike.Id)
        {
            MaintenanceFinisherCasts++;
            return;
        }

        if (Array.IndexOf(ScoredFinishers, abilityId) >= 0 && comboPoints is not null)
            _finishers.Add(new MaraFinisherCast(
                castEvent.Timestamp, abilityId, comboPoints.Amount, MeetsThreshold: false));
    }

    public override void OnPullEnd()
    {
        var threshold = FinisherCpThreshold;
        for (var i = 0; i < _finishers.Count; i++)
            _finishers[i] = _finishers[i] with { MeetsThreshold = _finishers[i].ComboPoints >= threshold };

        FinishersAtThreshold = _finishers.Count(f => f.MeetsThreshold);
    }

    private static ClassResource? FindResource(List<ClassResource> resources, ResourceTypes type)
    {
        foreach (var resource in resources)
            if (resource.Type == type)
                return resource;
        return null;
    }
}

/// <summary>
/// Resource-discipline analyzer for single-target (boss) pulls: spending finishers are expected at
/// 5 or more combo points, with Queen's Fang as the primary single-target finisher.
/// </summary>
[ForPull(PullKind.Single)]
public sealed class SingleTargetMaraResourceDiscipline : MaraResourceDisciplineAnalyzer
{
    public override int FinisherCpThreshold => 5;
    public override MaraPullShape Shape => MaraPullShape.SingleTarget;
}

/// <summary>
/// Resource-discipline analyzer for multi-target (AoE) pulls: spending finishers are expected at
/// 4 or more combo points, with Arachnid Assault as the primary AoE finisher.
/// </summary>
[ForPull(PullKind.Multi)]
public sealed class AoEMaraResourceDiscipline : MaraResourceDisciplineAnalyzer
{
    public override int FinisherCpThreshold => 4;
    public override MaraPullShape Shape => MaraPullShape.Aoe;
}
