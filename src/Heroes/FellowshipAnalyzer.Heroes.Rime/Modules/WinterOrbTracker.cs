using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/// <summary>
/// Tracks Rime's Winter Orbs (the <see cref="ResourceTypes.Tertiary"/> resource).
/// <para>
/// Fellowship logs never stamp an orb <see cref="ClassResource.Cost"/>, and the actual spend is
/// routed through spell-specific damage/heal events whose resource snapshots oscillate wildly. The
/// generic <see cref="ResourceTracker"/> spend path keys on an explicit cost, so it never sees a
/// spend: it reports <c>Spent = 0</c> while inflating <c>Generated</c> from the noisy snapshots.
/// This tracker instead reconstructs the orb pool from the player's own cast-event snapshots (the
/// reliable, GCD-boundary view of the resource): every observed decrease is a spend, and every
/// generator cast at the cap is an overcap. That holds the accounting identity
/// <c>Generated = Spent + Wasted + Current</c>, and it stays build-agnostic because it works
/// whether orbs are spent by Glacial Blast / Ice Comet or by the Icy Talons spenders
/// (Talon Strike / Rising Talons).
/// </para>
/// </summary>
public sealed partial class WinterOrbTracker : ResourceTracker
{
    private const int MaxOrbCount = 5;

    private static readonly int[] OrbGeneratorIds =
    [
        Spells.FrostBolt.Id,
        Spells.ColdSnap.Id,
        Spells.FreezingTorrent.Id,
        Spells.BurstingIce.Id,
    ];

    private readonly List<OvercapIncident> _overcapIncidents = [];

    private bool _seeded;
    private int _orbs;
    private int _generated;
    private int _spent;
    private int _wasted;
    private int _cappedMs;
    private int _lastSnapshotTimestamp;

    public WinterOrbTracker(ILogger<ResourceTracker> logger) : base(logger)
    {
        MaxOverrides[ResourceTypes.Tertiary] = MaxOrbCount;
        DisplayNameOverrides[ResourceTypes.Tertiary] = "Winter Orbs";
    }

    /// <summary>A Winter Orb gain that landed on a full pool, and the generator that produced it.</summary>
    public sealed record OvercapIncident(int Timestamp, int AbilityId);

    [On<CastEvent>(By = Actor.Player)]
    private void OnPlayerCast(CastEvent e)
    {
        var orb = FindOrbResource(e);
        if (orb is null) return;

        var amount = Math.Clamp(orb.Amount, 0, MaxOrbCount);

        if (!_seeded)
        {
            _orbs = amount;
            _lastSnapshotTimestamp = e.Timestamp;
            _seeded = true;
            return;
        }

        if (_orbs >= MaxOrbCount && amount >= MaxOrbCount)
            _cappedMs += e.Timestamp - _lastSnapshotTimestamp;

        _lastSnapshotTimestamp = e.Timestamp;

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
            _overcapIncidents.Add(new OvercapIncident(e.Timestamp, e.Ability.Id));
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

    /// <summary>
    /// Milliseconds the pool spent sitting at the cap, summed over every span between consecutive
    /// cast snapshots that both read the maximum. Snapshots only arrive on casts, so a span is
    /// credited in full whenever the reading either side of it is capped.
    /// </summary>
    public int CappedMs => _cappedMs;

    /// <summary>Every Winter Orb gain that was lost to the cap, in chronological order.</summary>
    public IReadOnlyList<OvercapIncident> OvercapIncidents => _overcapIncidents;
}
