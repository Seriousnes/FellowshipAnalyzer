using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Rime;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public enum RimeMajorCooldown
{
    IceBlitz,
    WintersBlessing,
    FlightOfTheNavir,
    WrathOfWinter,
}

public readonly record struct MajorCooldownUsage(int Casts, int HeldMs, int BuffUptimeMs);

public sealed class MajorCooldownWindow
{
    internal int GeneratorCastsWithOrbSnapshot;
    internal int GeneratorCastsAtCap;

    public required RimeMajorCooldown Major { get; init; }
    public required int WindowStart { get; init; }

    public int WindowEnd { get; internal set; }

    public int DurationMs => Math.Max(0, WindowEnd - WindowStart);

    public bool BoundaryTruncated { get; internal set; }

    public int BurstingIceCastsInside { get; internal set; }

    public int SpenderCastsInside { get; internal set; }

    public int? OrbsAtActivation { get; init; }

    public int? FirstSpenderLatencyMs { get; internal set; }

    public int? OvercapsInside => GeneratorCastsWithOrbSnapshot == 0 ? null : GeneratorCastsAtCap;
}

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class MajorCooldownAnalyzer : Analyzer
{
    public const int MaxWinterOrbs = 5;

    private readonly MajorTracking _iceBlitz = new(RimeMajorCooldown.IceBlitz);
    private readonly MajorTracking _wintersBlessing = new(RimeMajorCooldown.WintersBlessing);
    private readonly MajorTracking _flightOfTheNavir = new(RimeMajorCooldown.FlightOfTheNavir);
    private readonly MajorTracking _wrathOfWinter = new(RimeMajorCooldown.WrathOfWinter);

    private readonly List<MajorCooldownWindow> _windows = [];

    private bool _materialized;

    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    public MajorCooldownUsage IceBlitz => Usage(_iceBlitz);
    public MajorCooldownUsage WintersBlessing => Usage(_wintersBlessing);
    public MajorCooldownUsage FlightOfTheNavir => Usage(_flightOfTheNavir);

    public IReadOnlyList<MajorCooldownWindow> Windows { get { EnsureMaterialized(); return _windows; } }

    public IReadOnlyList<MajorCooldownWindow> WindowsFor(RimeMajorCooldown major) =>
        [.. Windows.Where(window => window.Major == major)];

    public MajorCooldownUsage UsageFor(RimeMajorCooldown major) => Usage(Select(major));

    [On<UpdateSpellUsableEvent>(By = Actor.Player, Spells = [
        nameof(Spells.IceBlitz),
        nameof(Spells.WintersBlessing),
        nameof(Spells.FlightOfTheNavir)])]
    private void OnUpdateSpellUsable(UpdateSpellUsableEvent updateEvent)
    {
        if (SelectByCastId(updateEvent.Ability.Id) is not { } tracking) return;

        if (updateEvent.UpdateType == UpdateSpellUsableType.EndCooldown)
        {
            tracking.OffCooldownTimestamp = updateEvent.Timestamp;
        }
        else if (updateEvent.UpdateType == UpdateSpellUsableType.BeginCooldown &&
                 tracking.OffCooldownTimestamp is int offCooldown)
        {
            tracking.TotalHeldMs += Math.Max(0, updateEvent.Timestamp - offCooldown);
            tracking.OffCooldownTimestamp = null;
        }
    }

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent castEvent)
    {
        if (SelectByCastId(castEvent.Ability.Id) is { } tracking)
        {
            tracking.Casts++;
            tracking.OrbsAtLastCast = ReadWinterOrbs(castEvent);
        }

        TallyIntoOpenWindows(castEvent);
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spells = [
        nameof(Spells.IceBlitzBuff),
        nameof(Spells.WinterBlessingSelfBuff),
        nameof(Spells.FlightOfTheNavirAoeBuff),
        nameof(Spells.WrathOfWinterBuff)])]
    private void OnApplyBuff(ApplyBuffEvent applyBuffEvent)
    {
        if (SelectByBuffId(applyBuffEvent.Ability.Id) is not { } tracking) return;

        CloseBuffInterval(tracking, applyBuffEvent.Timestamp);
        CloseWindow(tracking, applyBuffEvent.Timestamp, boundaryTruncated: false);

        tracking.BuffStartTimestamp = applyBuffEvent.Timestamp;
        tracking.OpenWindow = new MajorCooldownWindow
        {
            Major = tracking.Major,
            WindowStart = applyBuffEvent.Timestamp,
            OrbsAtActivation = tracking.OrbsAtLastCast,
        };
        tracking.OrbsAtLastCast = null;
        _windows.Add(tracking.OpenWindow);
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spells = [
        nameof(Spells.IceBlitzBuff),
        nameof(Spells.WinterBlessingSelfBuff),
        nameof(Spells.FlightOfTheNavirAoeBuff),
        nameof(Spells.WrathOfWinterBuff)])]
    private void OnRemoveBuff(RemoveBuffEvent removeBuffEvent)
    {
        if (SelectByBuffId(removeBuffEvent.Ability.Id) is not { } tracking) return;

        CloseBuffInterval(tracking, removeBuffEvent.Timestamp);
        CloseWindow(tracking, removeBuffEvent.Timestamp, boundaryTruncated: false);
    }

    private void TallyIntoOpenWindows(CastEvent castEvent)
    {
        int abilityId = castEvent.Ability.Id;
        var isBurstingIce = abilityId == Spells.BurstingIce.FSLID;
        var isSpender = IsSpender(abilityId);
        var isSingleTargetSpender = abilityId == Spells.GlacialBlast.FSLID || abilityId == Spells.TalonStrike.FSLID;
        var generatorOrbs = IsGenerator(abilityId) ? ReadWinterOrbs(castEvent) : null;

        foreach (var tracking in Trackings)
        {
            if (tracking.OpenWindow is not { } window) continue;

            if (isBurstingIce)
                window.BurstingIceCastsInside++;

            if (isSpender)
                window.SpenderCastsInside++;

            if (isSingleTargetSpender && window.FirstSpenderLatencyMs is null)
                window.FirstSpenderLatencyMs = Math.Max(0, castEvent.Timestamp - window.WindowStart);

            if (generatorOrbs is not { } orbs) continue;

            window.GeneratorCastsWithOrbSnapshot++;
            if (orbs >= MaxWinterOrbs)
                window.GeneratorCastsAtCap++;
        }
    }

    private MajorCooldownUsage Usage(MajorTracking tracking)
    {
        var heldMs = tracking.TotalHeldMs +
            (tracking.OffCooldownTimestamp is int offCooldown ? Math.Max(0, Pull.EndTime - offCooldown) : 0);
        var buffUptimeMs = tracking.TotalBuffUptimeMs +
            (tracking.BuffStartTimestamp is int buffStart ? Math.Max(0, Pull.EndTime - buffStart) : 0);

        return new MajorCooldownUsage(tracking.Casts, heldMs, buffUptimeMs);
    }

    private void EnsureMaterialized()
    {
        if (_materialized) return;
        _materialized = true;

        foreach (var tracking in Trackings)
            CloseWindow(tracking, Pull.EndTime, boundaryTruncated: true);
    }

    private static void CloseBuffInterval(MajorTracking tracking, int timestamp)
    {
        if (tracking.BuffStartTimestamp is not int start) return;

        tracking.TotalBuffUptimeMs += Math.Max(0, timestamp - start);
        tracking.BuffStartTimestamp = null;
    }

    private static void CloseWindow(MajorTracking tracking, int timestamp, bool boundaryTruncated)
    {
        if (tracking.OpenWindow is not { } window) return;

        window.WindowEnd = Math.Max(window.WindowStart, timestamp);
        window.BoundaryTruncated = boundaryTruncated;
        tracking.OpenWindow = null;
    }

    private static int? ReadWinterOrbs(CastEvent castEvent)
    {
        var resources = castEvent.SourceResources?.Resources;
        if (resources is null) return null;

        foreach (var resource in resources)
        {
            if (resource.Type == ResourceTypes.Tertiary)
                return resource.Amount;
        }

        return null;
    }

    private static bool IsSpender(int abilityId) =>
        abilityId == Spells.GlacialBlast.FSLID ||
        abilityId == Spells.IceComet.FSLID ||
        abilityId == Spells.TalonStrike.FSLID ||
        abilityId == Spells.RisingTalons.FSLID;

    private static bool IsGenerator(int abilityId) =>
        abilityId == Spells.FrostBolt.FSLID ||
        abilityId == Spells.ColdSnap.FSLID ||
        abilityId == Spells.FreezingTorrent.FSLID ||
        abilityId == Spells.BurstingIce.FSLID;

    private IEnumerable<MajorTracking> Trackings
    {
        get
        {
            yield return _iceBlitz;
            yield return _wintersBlessing;
            yield return _flightOfTheNavir;
            yield return _wrathOfWinter;
        }
    }

    private MajorTracking Select(RimeMajorCooldown major) => major switch
    {
        RimeMajorCooldown.IceBlitz => _iceBlitz,
        RimeMajorCooldown.WintersBlessing => _wintersBlessing,
        RimeMajorCooldown.FlightOfTheNavir => _flightOfTheNavir,
        _ => _wrathOfWinter,
    };

    private MajorTracking? SelectByCastId(int abilityId)
    {
        if (abilityId == Spells.IceBlitz.FSLID) return _iceBlitz;
        if (abilityId == Spells.WintersBlessing.FSLID) return _wintersBlessing;
        if (abilityId == Spells.FlightOfTheNavir.FSLID) return _flightOfTheNavir;
        if (abilityId == Spells.WrathOfWinter.FSLID) return _wrathOfWinter;
        return null;
    }

    private MajorTracking? SelectByBuffId(int buffId)
    {
        if (buffId == Spells.IceBlitzBuff.FSLID) return _iceBlitz;
        if (buffId == Spells.WinterBlessingSelfBuff.FSLID) return _wintersBlessing;
        if (buffId == Spells.FlightOfTheNavirAoeBuff.FSLID) return _flightOfTheNavir;
        if (buffId == Spells.WrathOfWinterBuff.FSLID) return _wrathOfWinter;
        return null;
    }

    private sealed class MajorTracking(RimeMajorCooldown major)
    {
        public RimeMajorCooldown Major { get; } = major;
        public int Casts { get; set; }
        public int TotalHeldMs { get; set; }
        public int TotalBuffUptimeMs { get; set; }
        public int? OffCooldownTimestamp { get; set; }
        public int? BuffStartTimestamp { get; set; }
        public int? OrbsAtLastCast { get; set; }
        public MajorCooldownWindow? OpenWindow { get; set; }
    }
}
