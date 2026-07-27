using System.Runtime.InteropServices;

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class FuryEconomyAnalyzer : Analyzer
{
    public const int FuryCap = 100;

    public const int WildSwingGain = 3;

    public const int FaceBreakerGain = 7;

    public const int HeavyStrikeGain = 12;

    public const int LeapSmashGain = 20;

    public const double ChainLightningGainPerTarget = 1.2;

    private readonly Dictionary<int, (int Casts, int Wasted)> _perAbility = [];

    private int _activeStart = int.MaxValue;
    private int _activeEnd = int.MinValue;

    public int GeneratorCasts { get; private set; }

    public int OvercapCasts { get; private set; }

    public int WastedFury { get; private set; }

    public int PotentialGeneration { get; private set; }

    public int SkullCrusherCasts { get; private set; }
    public int HammerStormCasts { get; private set; }
    public int CullingStrikeCasts { get; private set; }

    public int SpenderCasts => SkullCrusherCasts + HammerStormCasts + CullingStrikeCasts;

    public IReadOnlyList<AbilityWaste> WasteByAbility => field ??=
    [
        .. _perAbility
            .Select(entry => new AbilityWaste(entry.Key, entry.Value.Casts, entry.Value.Wasted))
            .OrderByDescending(waste => waste.WastedFury)
            .ThenBy(waste => waste.SpellId),
    ];

    public int ActiveSpanMs => _activeEnd > _activeStart ? _activeEnd - _activeStart : 0;

    public double WastedFuryPerMinute => ActiveSpanMs <= 0 ? 0 : WastedFury * 60_000d / ActiveSpanMs;

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

public readonly record struct AbilityWaste(int SpellId, int Casts, int WastedFury);
