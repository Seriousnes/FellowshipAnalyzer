using System.Runtime.InteropServices;

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

/// <summary>
/// Measures Tariq's Fury economy on every pull, regardless of shape. Fury is a build-and-spend
/// resource (<see cref="ResourceTypes.Primary"/>) capped at 100: Basic, Core, and mobility abilities
/// generate a flat amount and Power abilities spend it. Every generator therefore has a known gain, so
/// a cast made near the cap throws away exactly the points that would have crossed 100 - the headline
/// measure here is those wasted points (<see cref="WastedFury"/>), not a binary at-cap check.
/// <para>
/// Fury is read as a percentage of the logged pool rather than in raw units, because the logged maximum
/// is stat-scaled and grows across a dungeon run. Only the amount over max fraction is stable, and it is
/// derived the same way <see cref="FuryTracker"/> derives it so both surfaces agree on a given cast.
/// </para>
/// <para>
/// Whichever build the player runs, the generator and spender sets are the same, so overcap avoidance
/// holds across builds (Schism only changes the split between Skull Crusher and Hammer Storm, both of
/// which remain Fury spenders).
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class FuryEconomyAnalyzer : Analyzer
{
    /// <summary>Fury cap, always 100 because Fury is read as a percentage of the stat-scaled pool.</summary>
    public const int FuryCap = 100;

    /// <summary>Fury a Wild Swing cast generates. Season 3 <c>hero_data</c> Kit constant (ResourceGain 300, x100 scaled).</summary>
    public const int WildSwingGain = 3;

    /// <summary>Fury a Face Breaker cast generates. Season 3 <c>hero_data</c> Kit constant (ResourceGain 700, x100 scaled).</summary>
    public const int FaceBreakerGain = 7;

    /// <summary>Fury a Heavy Strike cast generates. Season 3 <c>hero_data</c> Kit constant (ResourceGain 1200, x100 scaled).</summary>
    public const int HeavyStrikeGain = 12;

    /// <summary>
    /// Fury a Leap Smash cast generates. Season 3 <c>hero_data</c> Kit constant (ResourceGain 2000, x100
    /// scaled). Leap Smash is off-GCD mobility, so it is easy to fire while parked at the cap and throw
    /// away the largest single gain in the kit.
    /// </summary>
    public const int LeapSmashGain = 20;

    /// <summary>
    /// Fury a Chain Lightning cast generates per target it hits. Season 3 <c>hero_data</c> Kit constant
    /// (ResourceGain 120, x100 scaled). The log does not tie the generation to a target count at cast
    /// time, and a single hit is worth barely more than a point, so Chain Lightning is counted as a
    /// generator cast but modeled as wasting nothing and contributes no
    /// <see cref="PotentialGeneration"/>.
    /// </summary>
    public const double ChainLightningGainPerTarget = 1.2;

    private readonly Dictionary<int, (int Casts, int Wasted)> _perAbility = [];

    private List<AbilityWaste>? _wasteByAbility;

    private int _activeStart = int.MaxValue;
    private int _activeEnd = int.MinValue;

    /// <summary>Every Fury generator cast in the pull, including Chain Lightning.</summary>
    public int GeneratorCasts { get; private set; }

    /// <summary>Generator casts that pushed Fury past the cap and so threw points away.</summary>
    public int OvercapCasts { get; private set; }

    /// <summary>Fury points lost to the cap across the pull.</summary>
    public int WastedFury { get; private set; }

    /// <summary>
    /// Fury points the fixed-gain generator casts would have produced against an empty bar. A cast
    /// carrying no Fury snapshot still counts here, because its gain was real even though its waste
    /// cannot be measured, which biases <see cref="WasteRate"/> low rather than inventing waste.
    /// Chain Lightning contributes a <see cref="GeneratorCasts"/> tally but no potential, so
    /// <see cref="WasteRate"/> is measured over the fixed-gain casts alone.
    /// </summary>
    public int PotentialGeneration { get; private set; }

    public int SkullCrusherCasts { get; private set; }
    public int HammerStormCasts { get; private set; }
    public int CullingStrikeCasts { get; private set; }

    public int SpenderCasts => SkullCrusherCasts + HammerStormCasts + CullingStrikeCasts;

    /// <summary>
    /// Casts and wasted points per generating ability, heaviest waster first and spell id breaking a
    /// tie. Face Breaker's two logged ids roll up into one entry, and Chain Lightning appears with its
    /// cast count and no waste.
    /// </summary>
    public IReadOnlyList<AbilityWaste> WasteByAbility => _wasteByAbility ??=
    [
        .. _perAbility
            .Select(entry => new AbilityWaste(entry.Key, entry.Value.Casts, entry.Value.Wasted))
            .OrderByDescending(waste => waste.WastedFury)
            .ThenBy(waste => waste.SpellId),
    ];

    /// <summary>Milliseconds between the first and last event this analyzer observed in the pull.</summary>
    public int ActiveSpanMs => _activeEnd > _activeStart ? _activeEnd - _activeStart : 0;

    /// <summary>Fury points lost to the cap per minute of observed activity.</summary>
    public double WastedFuryPerMinute => ActiveSpanMs <= 0 ? 0 : WastedFury * 60_000d / ActiveSpanMs;

    /// <summary>Share (0-1) of the fixed generation that was thrown away at the cap.</summary>
    public double WasteRate => PotentialGeneration == 0 ? 0 : (double)WastedFury / PotentialGeneration;

    [On<CastEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.WildSwing),
        nameof(Spells.FaceBreaker),
        nameof(Spells.FaceBreakerAlt),
        nameof(Spells.HeavyStrike),
        nameof(Spells.LeapSmash),
    })]
    private void OnGeneratorCast(CastEvent @event)
    {
        Track(@event.Timestamp);
        GeneratorCasts++;

        var gain = GainFor(@event.Ability.Id);
        PotentialGeneration += gain;

        var wasted = FuryPercent(@event) is { } fury ? Math.Max(0, fury + gain - FuryCap) : 0;
        if (wasted > 0)
            OvercapCasts++;

        WastedFury += wasted;
        Accumulate(RollUp(@event.Ability.Id), wasted);
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.ChainLightning))]
    private void OnChainLightningCast(CastEvent @event)
    {
        Track(@event.Timestamp);
        GeneratorCasts++;
        Accumulate(Spells.ChainLightning.FSLID, 0);
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

    private void Accumulate(int spellId, int wasted)
    {
        ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(_perAbility, spellId, out _);
        entry.Casts++;
        entry.Wasted += wasted;
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
        : spellId == Spells.LeapSmash.FSLID ? LeapSmashGain
        : FaceBreakerGain;

    private static int RollUp(int spellId) =>
        spellId == Spells.FaceBreakerAlt.FSLID ? Spells.FaceBreaker.FSLID : spellId;

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

/// <summary>One generating ability's contribution to the pull's Fury waste.</summary>
/// <param name="SpellId">The generator's <c>FSLID</c>, resolvable through <c>SpellRegistry</c>.</param>
/// <param name="Casts">Casts of this ability in the pull.</param>
/// <param name="WastedFury">Fury points those casts lost to the cap.</param>
public readonly record struct AbilityWaste(int SpellId, int Casts, int WastedFury);
