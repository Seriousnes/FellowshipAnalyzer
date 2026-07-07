using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;
using FellowshipAnalyzer.Heroes.Rime.Statistics;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/// <summary>
/// Tracks Rime's Winter Orbs (the <see cref="ResourceTypes.Tertiary"/> resource).
/// <para>
/// Fellowship logs never stamp an orb <see cref="ClassResource.Cost"/>, and the actual spend is
/// routed through spell-specific damage/heal events whose resource snapshots oscillate wildly, so
/// the generic <see cref="ResourceTracker"/> spend path — which keys on an explicit cost — never
/// sees a spend and reports <c>Spent = 0</c> while inflating <c>Generated</c> from the noisy
/// snapshots. This tracker instead reconstructs the orb pool from the player's own cast-event
/// snapshots (the reliable, GCD-boundary view of the resource): every observed decrease is a spend
/// and every generator cast at the cap is an overcap. That holds the accounting identity
/// <c>Generated = Spent + Wasted + Current</c> and is build-agnostic — it works whether orbs are
/// spent by Glacial Blast / Ice Comet or by the Icy Talons spenders (Talon Strike / Rising Talons).
/// </para>
/// </summary>
public sealed partial class WinterOrbTracker(ILogger<ResourceTracker> logger) : ResourceTracker(logger)
{
    private const int MaxOrbCount = 5;

    private static readonly int[] OrbGeneratorIds =
    [
        Spells.FrostBolt.Id,
        Spells.ColdSnap.Id,
        Spells.FreezingTorrent.Id,
        Spells.BurstingIce.Id,
    ];

    private bool _seeded;
    private int _orbs;
    private int _generated;
    private int _spent;
    private int _wasted;

    [On<CastEvent>(By = Actor.Player)]
    private void OnPlayerCast(CastEvent e)
    {
        var orb = FindOrbResource(e);
        if (orb is null) return;

        var amount = Math.Clamp(orb.Amount, 0, MaxOrbCount);

        if (!_seeded)
        {
            _orbs = amount;
            _seeded = true;
            return;
        }

        var delta = amount - _orbs;
        if (delta > 0)
            _generated += delta;
        else if (delta < 0)
            _spent += -delta;

        var overcapped = delta == 0 && amount >= MaxOrbCount && IsOrbGenerator(e.Ability.Id);
        _orbs = amount;

        if (overcapped)
        {
            _wasted++;
            _generated++;
        }
    }

    private static ClassResource? FindOrbResource(CastEvent e)
    {
        var resources = e.SourceResources?.Resources;
        if (resources is null) return null;

        foreach (var resource in resources)
            if (resource.Type == ResourceTypes.Tertiary)
                return resource;

        return null;
    }

    private static bool IsOrbGenerator(int abilityId)
    {
        foreach (var id in OrbGeneratorIds)
            if (id == abilityId)
                return true;

        return false;
    }

    public override Type? StatisticsComponentType => typeof(WinterOrbStatistics);

    /// <summary>Maximum Winter Orbs Rime can hold.</summary>
    public int MaxOrbs => MaxOrbCount;

    /// <summary>Total Winter Orbs generated during the encounter, including orbs lost to overcap.</summary>
    public int Generated => _generated;

    /// <summary>Winter Orbs lost by generating while already at the cap.</summary>
    public int Wasted => _wasted;

    /// <summary>Winter Orbs consumed by spenders.</summary>
    public int Spent => _spent;

    /// <summary>Winter Orbs held at the point last observed.</summary>
    public int Current => _orbs;

    /// <summary>Winter Orbs held at the point last observed.</summary>
    public int CurrentOrbs => _orbs;
}
