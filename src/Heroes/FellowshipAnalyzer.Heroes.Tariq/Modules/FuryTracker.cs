using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

/// <summary>
/// Tracks Tariq's Fury (the <see cref="ResourceTypes.Primary"/> resource) as a 0-100 percentage of cap.
/// <para>
/// Fury is 0-100 in game, but the logged pool is stat-scaled: the raw <see cref="ClassResource.Max"/>
/// grows as the player gains stats over a dungeon run, so the raw <see cref="ClassResource.Amount"/> is
/// meaningless on its own and cannot be compared across pulls. Only the amount over max fraction is
/// stable, so this tracker reconstructs Fury as a percentage rather than in raw units.
/// </para>
/// <para>
/// Fellowship logs carry no <c>resourcechange</c> events for Fury and never stamp a Fury
/// <see cref="ClassResource.Cost"/>, so the generic <see cref="ResourceTracker"/> accounting sees no
/// gains and no spends. The player's own <see cref="CastEvent"/> snapshots are the one reliable read:
/// they are taken at GCD boundaries and carry the pool state at each decision point. Every observed
/// rise between casts is counted as generation and every observed fall as a spend, which holds the
/// accounting identity across builds without needing per-ability cost data.
/// </para>
/// </summary>
public sealed partial class FuryTracker : ResourceTracker
{
    private const int FuryCap = 100;

    private readonly List<FurySample> _samples = [];

    private bool _seeded;
    private int _fury;
    private int _generated;
    private int _spent;

    public FuryTracker(ILogger<ResourceTracker> logger) : base(logger)
    {
        DisplayNameOverrides[ResourceTypes.Primary] = "Fury";
        MaxOverrides[ResourceTypes.Primary] = FuryCap;
    }

    [On<CastEvent>(By = Actor.Player)]
    private void OnPlayerCast(CastEvent e)
    {
        var resource = FindFuryResource(e);
        if (resource is null || resource.Max <= 0) return;

        var fury = (int)Math.Clamp(Math.Round(resource.Amount * 100.0 / resource.Max), 0, FuryCap);

        if (_seeded)
        {
            var delta = fury - _fury;
            if (delta > 0)
                _generated += delta;
            else if (delta < 0)
                _spent += -delta;
        }

        _seeded = true;
        _fury = fury;
        _samples.Add(new FurySample(e.Timestamp, fury));
    }

    private static ClassResource? FindFuryResource(CastEvent e)
    {
        var resources = e.SourceResources?.Resources;
        if (resources is null) return null;

        foreach (var resource in resources)
            if (resource.Type == ResourceTypes.Primary)
                return resource;

        return null;
    }

    /// <summary>Fury cap, always 100 because Fury is tracked as a percentage of the stat-scaled pool.</summary>
    public int MaxFury => FuryCap;

    /// <summary>Fury held at the last observed cast, as a percentage of cap.</summary>
    public int Current => _fury;

    /// <summary>
    /// Total Fury gained across the fight, in percentage points. Accumulated from rises between
    /// consecutive cast snapshots; the first observation only seeds the running value.
    /// </summary>
    public int Generated => _generated;

    /// <summary>
    /// Total Fury consumed across the fight, in percentage points. Accumulated from falls between
    /// consecutive cast snapshots; the first observation only seeds the running value.
    /// </summary>
    public int Spent => _spent;

    /// <summary>
    /// Every Fury observation in chronological order, one per player cast that carried a Fury
    /// snapshot, including the seeding observation.
    /// </summary>
    public IReadOnlyList<FurySample> Samples => _samples;
}

/// <summary>A single Fury observation taken from a player cast, as a percentage of cap.</summary>
/// <param name="Timestamp">Fight-relative timestamp of the cast that carried the snapshot.</param>
/// <param name="Fury">Fury held at that cast, 0 to 100.</param>
public readonly record struct FurySample(int Timestamp, int Fury);
