using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

/// <summary>
/// Measures Fury lost at the cap by the generators whose gain is a known flat amount.
/// <see cref="Spells.LeapSmash"/> is deliberately outside the model: it is a movement cooldown pressed to
/// close a gap, so its Fury is a side effect rather than a resource decision. <see cref="Spells.ChainLightning"/>
/// is outside it too, because its gain scales with targets hit and cannot be evaluated against the cap.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class FuryEconomyAnalyzer : Analyzer
{
    public const int FuryCap = 100;

    /// <summary>Wild Swing's Fury gain, from <c>Wild Swing.ResourceGain</c>.</summary>
    public const int WildSwingGain = 3;

    /// <summary>Face Breaker's Fury gain, from <c>Face Breaker.ResourceGain</c>.</summary>
    public const int FaceBreakerGain = 7;

    /// <summary>Heavy Strike's Fury gain, from <c>Heavy Strike.ResourceGain</c>.</summary>
    public const int HeavyStrikeGain = 12;

    /// <summary>Skull Crusher's Fury cost, from <c>Skull Crusher.Cost</c>.</summary>
    public const int SkullCrusherCost = 25;

    /// <summary>Hammer Storm's Fury cost, from <c>AoeAttack.Cost</c>.</summary>
    public const int HammerStormCost = 50;

    /// <summary>The most Fury a Culling Strike consumes, from <c>Culling Strike.MaxResourceToSpend</c>. It scales its damage with what it spends rather than spending a fixed amount.</summary>
    public const int CullingStrikeMaxSpend = 10;

    private int _activeStart = int.MaxValue;
    private int _activeEnd = int.MinValue;

    /// <summary>Casts of the flat-gain generators the waste model covers.</summary>
    public int GeneratorCasts { get; private set; }

    public int OvercapCasts { get; private set; }

    public int WastedFury { get; private set; }

    public int SkullCrusherCasts { get; private set; }
    public int HammerStormCasts { get; private set; }
    public int CullingStrikeCasts { get; private set; }

    /// <summary>Casts of the three abilities that spend Fury. A plain count: they do not drain it equally, so it is not a measure of Fury spent.</summary>
    public int SpenderCasts => SkullCrusherCasts + HammerStormCasts + CullingStrikeCasts;

    public int ActiveSpanMs => _activeEnd > _activeStart ? _activeEnd - _activeStart : 0;

    public double WastedFuryPerMinute => ActiveSpanMs <= 0 ? 0 : WastedFury * 60_000d / ActiveSpanMs;

    [On<CastEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.WildSwing),
        nameof(Spells.FaceBreaker),
        nameof(Spells.FaceBreakerAlt),
        nameof(Spells.HeavyStrike),
    })]
    private void OnGeneratorCast(CastEvent @event)
    {
        Track(@event.Timestamp);
        GeneratorCasts++;

        var gain = GainFor(@event.Ability.Id);
        var wasted = FuryPercent(@event) is { } fury ? Math.Max(0, fury + gain - FuryCap) : 0;

        if (wasted > 0)
            OvercapCasts++;

        WastedFury += wasted;
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
    }

    private void Track(int timestamp)
    {
        if (timestamp < _activeStart)
            _activeStart = timestamp;
        if (timestamp > _activeEnd)
            _activeEnd = timestamp;
    }

    private static int GainFor(int spellId) =>
        spellId == Spells.WildSwing.FSLID ? WildSwingGain
        : spellId == Spells.HeavyStrike.FSLID ? HeavyStrikeGain
        : FaceBreakerGain;

    private static int? FuryPercent(Event @event)
    {
        var resources = @event.SourceResources?.Resources;
        if (resources is null)
            return null;

        foreach (var resource in resources)
        {
            if (resource.Type != ResourceTypes.Primary)
                continue;

            return resource.Max > 0
                ? (int)Math.Clamp(Math.Round(resource.Amount * 100.0 / resource.Max), 0, FuryCap)
                : null;
        }

        return null;
    }
}
