using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.Resources;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

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

    public int MaxFury => FuryCap;

    public int Current => _fury;

    public int Generated => _generated;

    public int Spent => _spent;

    public IReadOnlyList<FurySample> Samples => _samples;
}

public readonly record struct FurySample(int Timestamp, int Fury);
