using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using MaraTalents = FellowshipAnalyzer.Core.Common.Spells.MaraTalents;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

public sealed record MaraFinisherCast(
    int Timestamp,
    int AbilityId,
    int ComboPoints,
    int Threshold,
    int EnemiesAlive,
    int ArachnidAssaultTargetThreshold)
{
    public bool MeetsThreshold => ComboPoints >= Threshold;

    /// <summary>Whether the pull had an enemy alive at this cast, without which target count is unmeasured.</summary>
    public bool TargetCountMeasured => EnemiesAlive > 0;

    /// <summary>Whether this finisher is the one the enemy count alive at the cast calls for.</summary>
    public bool MatchesTargetCount => TargetCountMeasured &&
        (AbilityId == Spells.ArachnidAssault.Id
            ? EnemiesAlive >= ArachnidAssaultTargetThreshold
            : EnemiesAlive < ArachnidAssaultTargetThreshold);
}

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<Enemies>]
public sealed partial class MaraResourceDisciplineAnalyzer : Analyzer
{
    public const int QueenFangThreshold = 5;

    public const int ArachnidAssaultThreshold = 4;

    public const int ArachnidAssaultTargets = 3;

    public const int ArachnidAssaultTargetsWithFeedTheQueen = 6;

    private static readonly int[] Generators =
        [Spells.Backstab.Id, Spells.WidowBite.Id, Spells.SkitteringBlades.Id];

    private readonly List<MaraFinisherCast> _finishers = [];

    private int? _arachnidAssaultTargetThreshold;

    /// <summary>The enemy count from which Arachnid Assault is the finisher rather than Queen's Fang.</summary>
    public int ArachnidAssaultTargetThreshold =>
        _arachnidAssaultTargetThreshold ??= Owner.SelectedCombatant.HasTalent(MaraTalents.FeedTheQueen)
            ? ArachnidAssaultTargetsWithFeedTheQueen
            : ArachnidAssaultTargets;

    public List<MaraFinisherCast> Finishers => _finishers;

    public int FinishersAtThreshold => _finishers.Count(finisher => finisher.MeetsThreshold);

    public int QueenFangCasts => CastsOf(Spells.QueenFang.Id);

    public int QueenFangAtThreshold => AtThresholdOf(Spells.QueenFang.Id);

    public int ArachnidAssaultCasts => CastsOf(Spells.ArachnidAssault.Id);

    public int ArachnidAssaultAtThreshold => AtThresholdOf(Spells.ArachnidAssault.Id);

    /// <summary>Finishers cast with an enemy alive, which are the ones target count can be read against.</summary>
    public int FinishersWithTargetCount => _finishers.Count(finisher => finisher.TargetCountMeasured);

    public int FinishersMatchingTargetCount => _finishers.Count(finisher => finisher.MatchesTargetCount);

    /// <summary>Queen's Fang casts made at an enemy count Arachnid Assault covers.</summary>
    public int QueenFangAboveTargetThreshold => _finishers.Count(finisher =>
        finisher.AbilityId == Spells.QueenFang.Id && finisher.TargetCountMeasured && !finisher.MatchesTargetCount);

    /// <summary>Arachnid Assault casts made at an enemy count Queen's Fang covers.</summary>
    public int ArachnidAssaultBelowTargetThreshold => _finishers.Count(finisher =>
        finisher.AbilityId == Spells.ArachnidAssault.Id && finisher.TargetCountMeasured && !finisher.MatchesTargetCount);

    public int MaintenanceFinisherCasts { get; private set; }

    public int GeneratorCasts { get; private set; }

    public int GeneratorOvercapCasts { get; private set; }

    public int EnergyCastsSampled { get; private set; }

    public int EnergyCappedCasts { get; private set; }

    public double FinisherThresholdRate => _finishers.Count == 0 ? 0 : (double)FinishersAtThreshold / _finishers.Count;

    public double EnergyCapRate => EnergyCastsSampled == 0 ? 0 : (double)EnergyCappedCasts / EnergyCastsSampled;

    public double TargetCountMatchRate =>
        FinishersWithTargetCount == 0 ? 0 : (double)FinishersMatchingTargetCount / FinishersWithTargetCount;

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
                castEvent.Timestamp,
                abilityId,
                comboPoints.Amount,
                threshold,
                Enemies.AliveAt(Pull, castEvent.Timestamp),
                ArachnidAssaultTargetThreshold));
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
