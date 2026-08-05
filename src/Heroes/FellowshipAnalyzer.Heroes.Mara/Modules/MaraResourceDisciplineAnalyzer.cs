using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

public sealed record MaraFinisherCast(
    int Timestamp,
    int AbilityId,
    int ComboPoints,
    int Threshold)
{
    public bool MeetsThreshold => ComboPoints >= Threshold;
}

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class MaraResourceDisciplineAnalyzer : Analyzer
{
    public const int QueenFangThreshold = 5;

    public const int ArachnidAssaultThreshold = 4;

    private static readonly int[] Generators =
        [Spells.Backstab.Id, Spells.WidowBite.Id, Spells.SkitteringBlades.Id];

    private readonly List<MaraFinisherCast> _finishers = [];

    public IReadOnlyList<MaraFinisherCast> Finishers => _finishers;

    public int FinishersAtThreshold => _finishers.Count(finisher => finisher.MeetsThreshold);

    public int QueenFangCasts => CastsOf(Spells.QueenFang.Id);

    public int QueenFangAtThreshold => AtThresholdOf(Spells.QueenFang.Id);

    public int ArachnidAssaultCasts => CastsOf(Spells.ArachnidAssault.Id);

    public int ArachnidAssaultAtThreshold => AtThresholdOf(Spells.ArachnidAssault.Id);

    public int MaintenanceFinisherCasts { get; private set; }

    public int GeneratorCasts { get; private set; }

    public int GeneratorOvercapCasts { get; private set; }

    public int EnergyCastsSampled { get; private set; }

    public int EnergyCappedCasts { get; private set; }

    public double FinisherThresholdRate => _finishers.Count == 0 ? 0 : (double)FinishersAtThreshold / _finishers.Count;

    public double EnergyCapRate => EnergyCastsSampled == 0 ? 0 : (double)EnergyCappedCasts / EnergyCastsSampled;

    public static int ThresholdFor(int abilityId) =>
        abilityId == Spells.QueenFang.Id ? QueenFangThreshold
        : abilityId == Spells.ArachnidAssault.Id ? ArachnidAssaultThreshold
        : 0;

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

        if (ThresholdFor(abilityId) is var threshold and > 0 && comboPoints is not null)
            _finishers.Add(new MaraFinisherCast(
                castEvent.Timestamp, abilityId, comboPoints.Amount, threshold));
    }

    private int CastsOf(int abilityId) =>
        _finishers.Count(finisher => finisher.AbilityId == abilityId);

    private int AtThresholdOf(int abilityId) =>
        _finishers.Count(finisher => finisher.AbilityId == abilityId && finisher.MeetsThreshold);

    private static ClassResource? FindResource(List<ClassResource> resources, ResourceTypes type)
    {
        foreach (var resource in resources)
            if (resource.Type == type)
                return resource;
        return null;
    }
}
