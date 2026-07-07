using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Scores how well Ardeos layered damage-over-time effects before spending Detonate. Detonate's
/// damage scales with the number of active DoTs on the target it hits (and does not consume them),
/// so every Detonate hit is sampled for the count of Ardeos DoTs live on that target at that moment.
/// </summary>
/// <remarks>
/// The analyzer counts whichever of Ardeos's DoTs are present rather than requiring a fixed set, so
/// it measures a build's actual layering instead of penalising builds that drop or add a burn. A hit
/// is "well layered" once the target carries at least the three core burns (Searing Blaze, Engulfing
/// Flames, Fire Ball). Shape-agnostic: single-target and AoE pulls are both about maximising DoTs
/// under each Detonate, so one analyzer serves every pull.
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class DetonateDotCoverageAnalyzer : Analyzer<DetonateDotCoverageReport>
{
    private const int WellLayeredThreshold = 3;

    private readonly Dictionary<(int TargetId, int Instance), HashSet<int>> _activeDots = [];
    private readonly Dictionary<int, int> _distribution = [];
    private int _detonateCasts;
    private int _detonateHits;
    private long _dotSum;
    private int _maxDots;

    [On<ApplyDebuffEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.SearingBlazeDot), nameof(Spells.EngulfingFlamesDot), nameof(Spells.FireBallDot),
        nameof(Spells.FireFrogsDot), nameof(Spells.IncinerateDot), nameof(Spells.ApocalypseDot),
    })]
    private void OnDotApplied(ApplyDebuffEvent @event) => AddDot(@event.TargetId, @event.TargetInstance, @event.Ability.Id);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.SearingBlazeDot), nameof(Spells.EngulfingFlamesDot), nameof(Spells.FireBallDot),
        nameof(Spells.FireFrogsDot), nameof(Spells.IncinerateDot), nameof(Spells.ApocalypseDot),
    })]
    private void OnDotRefreshed(RefreshDebuffEvent @event) => AddDot(@event.TargetId, @event.TargetInstance, @event.Ability.Id);

    [On<RemoveDebuffEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.SearingBlazeDot), nameof(Spells.EngulfingFlamesDot), nameof(Spells.FireBallDot),
        nameof(Spells.FireFrogsDot), nameof(Spells.IncinerateDot), nameof(Spells.ApocalypseDot),
    })]
    private void OnDotRemoved(RemoveDebuffEvent @event) => RemoveDot(@event.TargetId, @event.TargetInstance, @event.Ability.Id);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Detonate))]
    private void OnDetonateCast(CastEvent @event) => _detonateCasts++;

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.Detonate))]
    private void OnDetonateHit(DamageEvent @event)
    {
        var count = _activeDots.TryGetValue((@event.TargetId, @event.TargetInstance ?? 0), out var dots) ? dots.Count : 0;
        _detonateHits++;
        _dotSum += count;
        _distribution[count] = _distribution.TryGetValue(count, out var existing) ? existing + 1 : 1;
        if (count > _maxDots)
            _maxDots = count;
    }

    private void AddDot(int targetId, int? instance, int dotId)
    {
        var key = (targetId, instance ?? 0);
        if (!_activeDots.TryGetValue(key, out var dots))
            _activeDots[key] = dots = [];
        dots.Add(dotId);
    }

    private void RemoveDot(int targetId, int? instance, int dotId)
    {
        if (_activeDots.TryGetValue((targetId, instance ?? 0), out var dots))
            dots.Remove(dotId);
    }

    public override DetonateDotCoverageReport OnPullEnd()
    {
        var distribution = new int[_maxDots + 1];
        var wellLayered = 0;
        var underLayered = 0;
        foreach (var (count, hits) in _distribution)
        {
            distribution[count] = hits;
            if (count >= WellLayeredThreshold)
                wellLayered += hits;
            if (count <= 1)
                underLayered += hits;
        }

        var average = _detonateHits == 0 ? 0d : (double)_dotSum / _detonateHits;
        var score = _detonateHits == 0 ? 0 : (int)Math.Round((double)wellLayered / _detonateHits * 100);
        var findings = BuildFindings(average, wellLayered, underLayered);

        var summary = _detonateHits == 0
            ? "No Detonate casts detected in this pull."
            : $"Detonate hit targets carrying {average:0.0} Ardeos DoTs on average; {score}% of hits were on well-layered targets (3+ DoTs).";

        var scoreCard = new AnalyzerScoreCard(
            "Detonate DoT Coverage",
            score,
            summary,
            score >= 75 ? "ice" : score >= 50 ? "amber" : "ember");

        return new DetonateDotCoverageReport(
            scoreCard,
            _detonateCasts,
            _detonateHits,
            average,
            _maxDots,
            wellLayered,
            underLayered,
            distribution,
            findings);
    }

    private List<ArdeosFinding> BuildFindings(double average, int wellLayered, int underLayered)
    {
        var findings = new List<ArdeosFinding>();
        if (_detonateHits == 0)
        {
            findings.Add(new ArdeosFinding("info", "No Detonate casts were detected in this pull."));
            return findings;
        }

        findings.Add(new ArdeosFinding("info",
            $"Detonate hit {_detonateHits} times across {_detonateCasts} casts, averaging {average:0.00} active Ardeos DoTs per hit (best observed: {_maxDots})."));

        var underShare = (double)underLayered / _detonateHits;
        if (underShare >= 0.15)
        {
            findings.Add(new ArdeosFinding("warning",
                $"{underLayered} of {_detonateHits} Detonate hits ({underShare:P0}) landed on targets with one DoT or none — layer your burns before spending Detonate."));
        }
        else if (wellLayered == _detonateHits)
        {
            findings.Add(new ArdeosFinding("info",
                "Every Detonate hit landed on a well-layered target — strong DoT upkeep before Detonate."));
        }

        return findings;
    }
}
