using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

public enum MaraPullShape
{
    SingleTarget,
    Aoe,
}

public sealed record MaraFinisherCast(
    int Timestamp,
    int AbilityId,
    int ComboPoints,
    bool MeetsThreshold);

public abstract partial class MaraResourceDisciplineAnalyzer : Analyzer
{
    private static readonly int[] Generators =
        [Spells.Backstab.Id, Spells.WidowBite.Id, Spells.SkitteringBlades.Id];

    private static readonly int[] ScoredFinishers =
        [Spells.QueenFang.Id, Spells.ArachnidAssault.Id];

    private readonly List<MaraFinisherCast> _finishers = [];

    private List<MaraFinisherCast> Scored => field ??= StampThresholds();

    public abstract int FinisherCpThreshold { get; }

    public abstract MaraPullShape Shape { get; }

    public IReadOnlyList<MaraFinisherCast> Finishers => Scored;

    public int FinishersAtThreshold => Scored.Count(f => f.MeetsThreshold);

    public int MaintenanceFinisherCasts { get; private set; }

    public int GeneratorCasts { get; private set; }

    public int GeneratorOvercapCasts { get; private set; }

    public int EnergyCastsSampled { get; private set; }

    public int EnergyCappedCasts { get; private set; }

    public double FinisherThresholdRate => _finishers.Count == 0 ? 0 : (double)FinishersAtThreshold / _finishers.Count;

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

    private List<MaraFinisherCast> StampThresholds()
    {
        var threshold = FinisherCpThreshold;
        return [.. _finishers.Select(f => f with { MeetsThreshold = f.ComboPoints >= threshold })];
    }

    private static ClassResource? FindResource(List<ClassResource> resources, ResourceTypes type)
    {
        foreach (var resource in resources)
            if (resource.Type == type)
                return resource;
        return null;
    }
}

[ForPull(PullKind.Single)]
public sealed class SingleTargetMaraResourceDiscipline : MaraResourceDisciplineAnalyzer
{
    public override int FinisherCpThreshold => 5;
    public override MaraPullShape Shape => MaraPullShape.SingleTarget;
}

[ForPull(PullKind.Multi)]
public sealed class AoEMaraResourceDiscipline : MaraResourceDisciplineAnalyzer
{
    public override int FinisherCpThreshold => 4;
    public override MaraPullShape Shape => MaraPullShape.Aoe;
}
