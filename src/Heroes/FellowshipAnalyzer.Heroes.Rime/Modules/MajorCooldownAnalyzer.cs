using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Rime;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

/// <summary>Rime's three cooldown-gated majors plus the Spirit ultimate.</summary>
public enum RimeMajorCooldown
{
    IceBlitz,
    WintersBlessing,
    FlightOfTheNavir,
    WrathOfWinter,
}

/// <summary>How one cooldown-gated major was used across a pull.</summary>
/// <param name="Casts">Times the ability was cast during the pull.</param>
/// <param name="HeldMs">Milliseconds the ability sat off cooldown before it was recast.</param>
/// <param name="BuffUptimeMs">Milliseconds its buff stood on the player.</param>
public readonly record struct MajorCooldownUsage(int Casts, int HeldMs, int BuffUptimeMs);

/// <summary>
/// One major's buff window and what the player fitted into it. Orb-derived members read the Winter
/// Orb snapshot the log stamps on a cast, so they are <c>null</c> on logs that carry no snapshots.
/// </summary>
public sealed class MajorCooldownWindow
{
    internal int GeneratorCastsWithOrbSnapshot;
    internal int GeneratorCastsAtCap;

    public required RimeMajorCooldown Major { get; init; }
    public required int WindowStart { get; init; }

    public int WindowEnd { get; internal set; }

    /// <summary>How long the window actually stood, in milliseconds.</summary>
    public int DurationMs => Math.Max(0, WindowEnd - WindowStart);

    /// <summary>True when the pull ended before the buff came off, so the window is measured to the pull boundary.</summary>
    public bool BoundaryTruncated { get; internal set; }

    /// <summary>Bursting Ice casts that landed while the window stood.</summary>
    public int BurstingIceCastsInside { get; internal set; }

    /// <summary>Winter Orb spender casts that landed while the window stood.</summary>
    public int SpenderCastsInside { get; internal set; }

    /// <summary>Winter Orbs banked at the cast that opened the window; null when the log carries no snapshot.</summary>
    public int? OrbsAtActivation { get; init; }

    /// <summary>
    /// Milliseconds from the window opening to the first single-target Winter Orb spender cast inside
    /// it; null when no such cast landed in the window.
    /// </summary>
    public int? FirstSpenderLatencyMs { get; internal set; }

    /// <summary>
    /// Winter Orb generator casts that went out with a full pool while the window stood. Null when no
    /// generator cast inside the window carried an orb snapshot, since the log then carries no
    /// evidence either way.
    /// </summary>
    public int? OvercapsInside => GeneratorCastsWithOrbSnapshot == 0 ? null : GeneratorCastsAtCap;
}

/// <summary>
/// Tracks Rime's major cooldowns within a pull. Ice Blitz, Winter's Blessing and Flight of the Navir
/// are 60-second cooldowns carrying 20-second buffs, so each one is measured on how long it sat off
/// cooldown before the player used it, how much of the pull its buff stood for, and what landed
/// inside each window. Wrath of Winter is Spirit-gated rather than cooldown-gated, so it carries no
/// held-time or cast-efficiency reading and is judged purely on window quality: the orbs banked when
/// it went off, how quickly the granted instant Glacial Blast was spent, and whether the orbs it
/// hands out every four seconds landed on a full pool.
/// </summary>
/// <remarks>
/// Held time comes from the <see cref="UpdateSpellUsableEvent"/> stream Core's
/// <see cref="SpellUsable"/> fabricates, so the interval before the pull's first cast is never
/// counted; an interval still open when the pull ends runs to <see cref="Pull.EndTime"/>. Windows are
/// anchored on the player's own buff apply/remove pair: a remove with no live window is ignored, a
/// re-apply while a window stands closes the open one at the re-apply, and a window still open at the
/// pull boundary closes there and is marked <see cref="MajorCooldownWindow.BoundaryTruncated"/>.
/// <para>
/// The cast that opens a window supplies its Winter Orb snapshot. Casts and buff applications are
/// separate events and the cast lands first, so each major holds its most recent cast's snapshot and
/// the next buff apply consumes it; an unconsumed snapshot never carries into a later window.
/// </para>
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class MajorCooldownAnalyzer : Analyzer
{
    /// <summary>The Winter Orb pool's capacity; a generator cast at this count wastes its gain.</summary>
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

    /// <summary>Every major buff window opened during the pull, in encounter order.</summary>
    public IReadOnlyList<MajorCooldownWindow> Windows { get { EnsureMaterialized(); return _windows; } }

    /// <summary>The windows one major opened during the pull, in encounter order.</summary>
    public IReadOnlyList<MajorCooldownWindow> WindowsFor(RimeMajorCooldown major) =>
        [.. Windows.Where(window => window.Major == major)];

    /// <summary>
    /// The usage reading for one major. Wrath of Winter is Spirit-gated rather than cooldown-gated,
    /// so its reading always carries a held time of zero.
    /// </summary>
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

    /// <summary>
    /// Adds any buff interval still standing at the pull boundary, so a major whose buff outlived the
    /// pull is credited to <see cref="Pull.EndTime"/> rather than dropped.
    /// </summary>
    private MajorCooldownUsage Usage(MajorTracking tracking)
    {
        var heldMs = tracking.TotalHeldMs +
            (tracking.OffCooldownTimestamp is int offCooldown ? Math.Max(0, Pull.EndTime - offCooldown) : 0);
        var buffUptimeMs = tracking.TotalBuffUptimeMs +
            (tracking.BuffStartTimestamp is int buffStart ? Math.Max(0, Pull.EndTime - buffStart) : 0);

        return new MajorCooldownUsage(tracking.Casts, heldMs, buffUptimeMs);
    }

    /// <summary>Closes any window still open at the pull boundary, once on first read.</summary>
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
