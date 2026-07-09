using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

/// <summary>
/// Scores Tariq's Fury economy on every pull, regardless of shape. Fury is a build-and-spend
/// resource (<see cref="ResourceTypes.Primary"/>): Basic/Core abilities generate it and Power
/// abilities spend it. The scored axis is overcap — a generator cast made while Fury is already
/// at cap throws away that generation. Whichever build the player runs, the generator and spender
/// sets are the same, so overcap avoidance holds across builds (Schism only changes the split
/// between Skull Crusher and Hammer Storm, both of which remain Fury spenders).
/// <para>
/// Two further build-robust facts are reported without being scored: the share of Fury spenders
/// landing inside a Thunder Call empowerment window (read against that window's uptime as a neutral
/// baseline), and how many Culling Strikes landed in execute range (target below 30% health).
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class FuryEconomyAnalyzer : Analyzer<FuryEconomyReport>
{
    private const double ExecuteHealthThreshold = 0.30;
    private const double ConcentrationTolerance = 0.10;

    private int _generatorCasts;
    private int _overcapCasts;
    private int _skullCrusherCasts;
    private int _hammerStormCasts;
    private int _cullingStrikeCasts;
    private int _empoweredSpenderCasts;

    private int _cullingStrikeHits;
    private int _executeCullingStrikes;

    private int _thunderCallWindows;
    private int _windowTimeMs;
    private bool _windowOpen;
    private int _windowOpenedAt;

    private int _activeStart = int.MaxValue;
    private int _activeEnd = int.MinValue;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ThunderCallBuff))]
    private void OnThunderCallOpen(ApplyBuffEvent @event)
    {
        Track(@event.Timestamp);
        if (_windowOpen)
            return;

        _windowOpen = true;
        _windowOpenedAt = @event.Timestamp;
        _thunderCallWindows++;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ThunderCallBuff))]
    private void OnThunderCallClose(RemoveBuffEvent @event)
    {
        Track(@event.Timestamp);
        if (!_windowOpen)
            return;

        _windowTimeMs += Math.Max(0, @event.Timestamp - _windowOpenedAt);
        _windowOpen = false;
    }

    [On<CastEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.WildSwing),
        nameof(Spells.FaceBreaker),
        nameof(Spells.FaceBreakerAlt),
        nameof(Spells.HeavyStrike),
        nameof(Spells.ChainLightning),
    })]
    private void OnGeneratorCast(CastEvent @event)
    {
        Track(@event.Timestamp);
        _generatorCasts++;
        if (IsFuryCapped(@event))
            _overcapCasts++;
    }

    [On<CastEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.SkullCrusher),
        nameof(Spells.HammerStorm),
        nameof(Spells.CullingStrike),
    })]
    private void OnSpenderCast(CastEvent @event)
    {
        Track(@event.Timestamp);
        if (@event.Ability.Id == Spells.SkullCrusher.FSLID)
            _skullCrusherCasts++;
        else if (@event.Ability.Id == Spells.HammerStorm.FSLID)
            _hammerStormCasts++;
        else
            _cullingStrikeCasts++;

        if (_windowOpen)
            _empoweredSpenderCasts++;
    }

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.CullingStrike))]
    private void OnCullingStrikeDamage(DamageEvent @event)
    {
        var target = @event.TargetResources;
        if (target is null || target.MaxHitPoints <= 0)
            return;

        _cullingStrikeHits++;
        if ((double)target.HitPoints / target.MaxHitPoints < ExecuteHealthThreshold)
            _executeCullingStrikes++;
    }

    /// <summary>Per-pull projection of the accumulated Fury economy for the closing pull.</summary>
    public override FuryEconomyReport OnPullEnd()
    {
        if (_windowOpen && _activeEnd > _windowOpenedAt)
            _windowTimeMs += _activeEnd - _windowOpenedAt;

        var spenderCasts = _skullCrusherCasts + _hammerStormCasts + _cullingStrikeCasts;
        var activeSpan = _activeEnd > _activeStart ? _activeEnd - _activeStart : 0;

        var score = _generatorCasts == 0
            ? 100
            : (int)Math.Round((1 - (double)_overcapCasts / _generatorCasts) * 100);

        var findings = BuildFindings(spenderCasts, activeSpan);
        var summary = _generatorCasts == 0
            ? "No Fury generation detected in this pull."
            : _overcapCasts == 0
                ? $"No Fury wasted to overcap across {_generatorCasts} generator casts."
                : $"{_overcapCasts} of {_generatorCasts} generator casts wasted Fury at cap.";

        var scoreCard = new AnalyzerScoreCard(
            "Fury Economy",
            score,
            summary,
            score >= 75 ? "amber" : "ember");

        return new FuryEconomyReport(
            scoreCard,
            _generatorCasts,
            _overcapCasts,
            spenderCasts,
            _empoweredSpenderCasts,
            _thunderCallWindows,
            _windowTimeMs,
            activeSpan,
            _cullingStrikeCasts,
            _executeCullingStrikes,
            _skullCrusherCasts,
            _hammerStormCasts,
            findings);
    }

    private List<TariqFinding> BuildFindings(int spenderCasts, int activeSpan)
    {
        var findings = new List<TariqFinding>();

        if (_generatorCasts == 0)
        {
            findings.Add(new TariqFinding("info", "No Fury generators were cast in this pull."));
            return findings;
        }

        findings.Add(_overcapCasts == 0
            ? new TariqFinding("info", $"No Fury was wasted to overcap across {_generatorCasts} generator casts.")
            : new TariqFinding("warning",
                $"{_overcapCasts} generator cast{(_overcapCasts == 1 ? " was" : "s were")} made at full Fury — that generation was wasted."));

        if (spenderCasts > 0 && _windowTimeMs > 0 && activeSpan > 0)
        {
            var empoweredShare = (double)_empoweredSpenderCasts / spenderCasts;
            var uptimeShare = Math.Min(1.0, (double)_windowTimeMs / activeSpan);
            var sharePct = (int)Math.Round(empoweredShare * 100);
            var uptimePct = (int)Math.Round(uptimeShare * 100);

            if (empoweredShare < uptimeShare - ConcentrationTolerance)
                findings.Add(new TariqFinding("warning",
                    $"Only {sharePct}% of Fury spenders landed inside a Thunder Call window (up {uptimePct}% of the pull) — pool Fury and dump it in the empowered window."));
            else if (empoweredShare > uptimeShare + ConcentrationTolerance)
                findings.Add(new TariqFinding("info",
                    $"{sharePct}% of Fury spenders landed inside a Thunder Call window (up {uptimePct}% of the pull) — Fury was well concentrated in the empowered window."));
            else
                findings.Add(new TariqFinding("info",
                    $"{sharePct}% of Fury spenders landed inside a Thunder Call window (up {uptimePct}% of the pull)."));
        }

        if (_cullingStrikeHits > 0)
            findings.Add(new TariqFinding("info",
                $"{_executeCullingStrikes} of {_cullingStrikeHits} Culling Strikes hit a target in execute range (below {(int)(ExecuteHealthThreshold * 100)}% health)."));

        return findings;
    }

    private void Track(int timestamp)
    {
        if (timestamp < _activeStart)
            _activeStart = timestamp;
        if (timestamp > _activeEnd)
            _activeEnd = timestamp;
    }

    private static bool IsFuryCapped(Event @event)
    {
        var resources = @event.SourceResources?.Resources;
        if (resources is null)
            return false;

        foreach (var resource in resources)
            if (resource.Type == ResourceTypes.Primary)
                return resource.Max > 0 && resource.Amount >= resource.Max;

        return false;
    }
}
