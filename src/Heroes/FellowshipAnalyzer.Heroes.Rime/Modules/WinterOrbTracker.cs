using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

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

    public int MaxOrbs => MaxOrbCount;

    public int Generated => _generated;

    public int Wasted => _wasted;

    public int Spent => _spent;

    public int Current => _orbs;

    public int CurrentOrbs => _orbs;

    public int CappedMs => _cappedMs;

    public List<OvercapIncident> OvercapIncidents => _overcapIncidents;
}
