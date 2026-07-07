using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.UI.Guides;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

/// <summary>
/// Scores Mara's resource discipline for a pull: how consistently combo-point-spending finishers
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
/// than purely to dump combo points, so it is counted separately and excluded from the threshold score.
/// </summary>
public abstract partial class MaraResourceDisciplineAnalyzer : Analyzer<MaraResourceReport>
{
    private const double FinisherWeight = 0.6;
    private const double EnergyWeight = 0.4;
    private const double EnergyOvercapFloor = 0.4;
    private const int EnergyOvercapMajorThresholdPercent = 15;

    private static readonly int[] Generators =
        [Spells.Backstab.Id, Spells.WidowBite.Id, Spells.SkitteringBlades.Id];

    private static readonly int[] ScoredFinishers =
        [Spells.QueenFang.Id, Spells.ArachnidAssault.Id];

    private readonly List<MaraFinisherCast> _finishers = [];
    private int _maintenanceFinisherCasts;
    private int _generatorCasts;
    private int _generatorOvercapCasts;
    private int _energyCastsSampled;
    private int _energyCappedCasts;

    /// <summary>The combo-point count at or above which a spending finisher is well-timed for this pull shape.</summary>
    protected abstract int FinisherCpThreshold { get; }

    /// <summary>The pull shape this leaf scores, recorded on the produced report.</summary>
    protected abstract MaraPullShape Shape { get; }

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent castEvent)
    {
        var resources = castEvent.SourceResources?.Resources;
        if (resources is null || resources.Count == 0)
            return;

        var energy = FindResource(resources, ResourceTypes.Primary);
        if (energy is { Max: > 0 })
        {
            _energyCastsSampled++;
            if (energy.Amount >= energy.Max)
                _energyCappedCasts++;
        }

        var comboPoints = FindResource(resources, ResourceTypes.Secondary);
        var abilityId = castEvent.Ability.Id;

        if (Array.IndexOf(Generators, abilityId) >= 0)
        {
            _generatorCasts++;
            if (comboPoints is { Max: > 0 } && comboPoints.Amount >= comboPoints.Max)
                _generatorOvercapCasts++;
            return;
        }

        if (abilityId == Spells.HemorrhagingStrike.Id)
        {
            _maintenanceFinisherCasts++;
            return;
        }

        if (Array.IndexOf(ScoredFinishers, abilityId) >= 0 && comboPoints is not null)
            _finishers.Add(new MaraFinisherCast(
                castEvent.Timestamp, abilityId, comboPoints.Amount, MeetsThreshold: false));
    }

    /// <summary>Per-pull projection of resource discipline for the closing pull.</summary>
    public override MaraResourceReport OnPullEnd()
    {
        var threshold = FinisherCpThreshold;
        var finishers = new List<MaraFinisherCast>(_finishers.Count);
        foreach (var finisher in _finishers)
            finishers.Add(finisher with { MeetsThreshold = finisher.ComboPoints >= threshold });

        var withData = finishers.Count;
        var atThreshold = finishers.Count(f => f.MeetsThreshold);
        var finisherQuality = withData == 0 ? 0.0 : (double)atThreshold / withData;
        var energyCapRate = _energyCastsSampled == 0 ? 0.0 : (double)_energyCappedCasts / _energyCastsSampled;
        var energyDiscipline = 1.0 - Math.Min(1.0, energyCapRate / EnergyOvercapFloor);

        var hasData = withData > 0 || _energyCastsSampled > 0;
        var score = hasData
            ? (int)Math.Round(100 * ((FinisherWeight * finisherQuality) + (EnergyWeight * energyDiscipline)))
            : 0;

        var findings = BuildFindings(finishers, withData, atThreshold, threshold, energyCapRate);
        var summary = BuildSummary(withData, atThreshold, threshold, energyCapRate);
        var scoreCard = new AnalyzerScoreCard(
            "Resource Discipline", score, summary,
            score >= 75 ? "ice" : score >= 50 ? "amber" : "ember");

        return new MaraResourceReport(
            scoreCard,
            Shape,
            threshold,
            withData,
            atThreshold,
            _maintenanceFinisherCasts,
            _generatorCasts,
            _generatorOvercapCasts,
            _energyCastsSampled,
            _energyCappedCasts,
            finishers,
            findings);
    }

    private List<Finding> BuildFindings(
        IReadOnlyList<MaraFinisherCast> finishers, int withData, int atThreshold, int threshold, double energyCapRate)
    {
        var findings = new List<Finding>();
        var shapeLabel = Shape == MaraPullShape.SingleTarget ? "single-target" : "AoE";

        if (withData == 0)
            findings.Add(new Finding("info", "No Queen's Fang or Arachnid Assault finishers were recorded for this pull."));
        else
        {
            findings.Add(new Finding("info",
                $"{atThreshold} of {withData} spending finishers were cast at {threshold}+ combo points."));

            foreach (var finisher in finishers.Where(f => !f.MeetsThreshold).Take(5))
                findings.Add(new Finding("warning",
                    $"{FinisherName(finisher.AbilityId)} cast at {finisher.ComboPoints} combo points (below the {threshold}+ target for {shapeLabel}).",
                    Owner.FormatTimestamp(finisher.Timestamp)));
        }

        if (_energyCappedCasts > 0)
        {
            var pct = (int)Math.Round(energyCapRate * 100);
            findings.Add(new Finding(
                pct >= EnergyOvercapMajorThresholdPercent ? "major" : "warning",
                $"Energy was capped on {_energyCappedCasts} of {_energyCastsSampled} sampled casts ({pct}%); Energy regenerated while capped is wasted. Spend more often to keep Energy flowing."));
        }

        if (_generatorOvercapCasts > 0)
            findings.Add(new Finding("warning",
                $"A combo-point generator was cast {_generatorOvercapCasts} time(s) while already at maximum combo points; that generation was wasted. Spend a finisher before rebuilding."));

        return findings;
    }

    private string BuildSummary(int withData, int atThreshold, int threshold, double energyCapRate)
    {
        if (withData == 0 && _energyCastsSampled == 0)
            return "No resource activity detected in this pull.";

        var finisherPart = withData == 0
            ? "no spending finishers recorded"
            : $"{atThreshold}/{withData} finishers at {threshold}+ combo points";
        var energyPart = _energyCastsSampled == 0
            ? "no Energy samples"
            : $"{(int)Math.Round(energyCapRate * 100)}% of casts at capped Energy";
        return $"{finisherPart}; {energyPart}.";
    }

    private static string FinisherName(int abilityId) =>
        abilityId == Spells.QueenFang.Id ? Spells.QueenFang.Name
        : abilityId == Spells.ArachnidAssault.Id ? Spells.ArachnidAssault.Name
        : "Finisher";

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
    protected override int FinisherCpThreshold => 5;
    protected override MaraPullShape Shape => MaraPullShape.SingleTarget;
}

/// <summary>
/// Resource-discipline analyzer for multi-target (AoE) pulls: spending finishers are expected at
/// 4 or more combo points, with Arachnid Assault as the primary AoE finisher.
/// </summary>
[ForPull(PullKind.Multi)]
public sealed class AoEMaraResourceDiscipline : MaraResourceDisciplineAnalyzer
{
    protected override int FinisherCpThreshold => 4;
    protected override MaraPullShape Shape => MaraPullShape.Aoe;
}
