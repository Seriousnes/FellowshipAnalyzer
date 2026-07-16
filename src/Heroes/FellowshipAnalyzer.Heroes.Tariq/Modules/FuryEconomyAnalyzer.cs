using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

/// <summary>
/// Measures Tariq's Fury economy on every pull, regardless of shape. Fury is a build-and-spend
/// resource (<see cref="ResourceTypes.Primary"/>): Basic/Core abilities generate it and Power
/// abilities spend it. The central measure is overcap - a generator cast made while Fury is
/// already at cap throws away that generation. Whichever build the player runs, the generator and
/// spender sets are the same, so overcap avoidance holds across builds (Schism only changes the
/// split between Skull Crusher and Hammer Storm, both of which remain Fury spenders).
/// <para>
/// Thunder Call windows are tracked alongside: the share of Fury spenders landing inside an
/// empowerment window (<see cref="EmpoweredSpendShare"/>) is read against that window's uptime
/// (<see cref="WindowUptimeShare"/>) as a neutral baseline - a share above uptime means spending
/// was concentrated into the empowered window.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class FuryEconomyAnalyzer : Analyzer
{
    public int GeneratorCasts { get; private set; }
    public int OvercapCasts { get; private set; }
    public int SkullCrusherCasts { get; private set; }
    public int HammerStormCasts { get; private set; }
    public int CullingStrikeCasts { get; private set; }
    public int EmpoweredSpenderCasts { get; private set; }
    public int ThunderCallWindows { get; private set; }

    /// <summary>Total milliseconds a Thunder Call window was open during the pull.</summary>
    public int WindowTimeMs => _closedWindowMs + (_windowOpen && _activeEnd > _windowOpenedAt ? _activeEnd - _windowOpenedAt : 0);

    private int _closedWindowMs;
    private bool _windowOpen;
    private int _windowOpenedAt;

    private int _activeStart = int.MaxValue;
    private int _activeEnd = int.MinValue;

    public int SpenderCasts => SkullCrusherCasts + HammerStormCasts + CullingStrikeCasts;

    /// <summary>Milliseconds between the first and last event this analyzer observed in the pull.</summary>
    public int ActiveSpanMs => _activeEnd > _activeStart ? _activeEnd - _activeStart : 0;

    /// <summary>Fraction of generator casts wasted by generating at Fury cap.</summary>
    public double OvercapRate => GeneratorCasts == 0 ? 0 : (double)OvercapCasts / GeneratorCasts;

    /// <summary>Fraction of Fury spenders cast inside a Thunder Call empowerment window.</summary>
    public double EmpoweredSpendShare => SpenderCasts == 0 ? 0 : (double)EmpoweredSpenderCasts / SpenderCasts;

    /// <summary>
    /// Fraction of the active pull span the Thunder Call window was up - the neutral baseline the
    /// empowered-spend share is read against (share above uptime means spenders were concentrated).
    /// </summary>
    public double WindowUptimeShare => ActiveSpanMs <= 0 ? 0 : Math.Min(1.0, (double)WindowTimeMs / ActiveSpanMs);

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ThunderCallBuff))]
    private void OnThunderCallOpen(ApplyBuffEvent @event)
    {
        Track(@event.Timestamp);
        if (_windowOpen)
            return;

        _windowOpen = true;
        _windowOpenedAt = @event.Timestamp;
        ThunderCallWindows++;
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ThunderCallBuff))]
    private void OnThunderCallClose(RemoveBuffEvent @event)
    {
        Track(@event.Timestamp);
        if (!_windowOpen)
            return;

        _closedWindowMs += Math.Max(0, @event.Timestamp - _windowOpenedAt);
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
        GeneratorCasts++;
        if (IsFuryCapped(@event))
            OvercapCasts++;
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
            SkullCrusherCasts++;
        else if (@event.Ability.Id == Spells.HammerStorm.FSLID)
            HammerStormCasts++;
        else
            CullingStrikeCasts++;

        if (_windowOpen)
            EmpoweredSpenderCasts++;
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
