using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using SpellRegistry = FellowshipAnalyzer.Core.Common.Spells.SpellRegistry;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Measures Elarion's Focus economy on every pull, regardless of shape. Focus
/// (<see cref="ResourceTypes.Primary"/>) regenerates on its own and caps at the value the log
/// reports, so the two ways to throw it away are sitting at the cap while regeneration continues
/// and casting <see cref="Spells.FocusedShot"/> - the one Focus generator - with too little room
/// for the Focus it grants. Fellowship logs carry no resource-change events for Elarion, so both
/// measures are read from the Focus snapshot the player's own cast events carry. That snapshot is
/// taken before the cast's own cost is deducted, and it is absent on some events, in which case
/// the cast contributes to neither census.
/// <para>
/// <see cref="Spells.EventHorizon"/> halves every Focus cost for the duration of its buff, so the
/// bank carried into the cast decides how much the window can convert. Each cast records the Focus
/// snapshot it started from and the spending that followed while the window was open: the window
/// opens on the cast, closes on the <see cref="Spells.EventHorizonBuff"/> removal, and runs to the
/// end of the pull when no removal is observed. Focus spent inside a window is approximated from
/// the registry cost of each spender, halved per cast, since the discounted cost is not logged.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class FocusEconomyAnalyzer : Analyzer
{
    /// <summary>
    /// The Focus level at and above which a Focused Shot cast counts as near-cap: its 20 Focus
    /// cannot fit under the cap, so most of that generation is lost.
    /// </summary>
    public const int NearCapFocus = 90;

    private readonly List<WindowState> _windows = [];
    private List<EventHorizonWindow>? _projectedWindows;

    /// <summary>Focused Shot casts made during the pull.</summary>
    public int FocusedShotCasts { get; private set; }

    /// <summary>Focused Shot casts started at <see cref="NearCapFocus"/> Focus or more.</summary>
    public int FocusedShotCastsNearCap { get; private set; }

    /// <summary>Player cast events during the pull that carried a Focus snapshot.</summary>
    public int SampledEvents { get; private set; }

    /// <summary>Sampled cast events whose Focus snapshot already sat at the cap.</summary>
    public int SampledAtCap { get; private set; }

    /// <summary>One entry per Event Horizon cast, in cast order.</summary>
    public IReadOnlyList<EventHorizonWindow> EventHorizonCasts =>
        _projectedWindows ??=
        [
            .. _windows.Select(window => new EventHorizonWindow(
                window.Timestamp,
                window.FocusAtCast,
                window.Spenders,
                window.FocusSpent)),
        ];

    /// <summary>Pull length in milliseconds.</summary>
    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    [On<CastEvent>(By = Actor.Player)]
    private void OnPlayerCast(CastEvent e)
    {
        if (FocusSnapshot(e) is not { } focus)
            return;

        SampledEvents++;
        if (focus.Max > 0 && focus.Amount >= focus.Max)
            SampledAtCap++;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.FocusedShot))]
    private void OnFocusedShotCast(CastEvent e)
    {
        FocusedShotCasts++;
        if (FocusSnapshot(e) is { } focus && focus.Amount >= NearCapFocus)
            FocusedShotCastsNearCap++;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.EventHorizon))]
    private void OnEventHorizonCast(CastEvent e)
    {
        if (OpenWindow() is { } previous)
            previous.End = e.Timestamp;

        _windows.Add(new WindowState
        {
            Timestamp = e.Timestamp,
            FocusAtCast = FocusSnapshot(e)?.Amount,
        });
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.EventHorizonBuff))]
    private void OnEventHorizonBuffRemove(RemoveBuffEvent e)
    {
        if (OpenWindow() is { } window)
            window.End = e.Timestamp;
    }

    [On<CastEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.CelestialShot),
        nameof(Spells.Multishot),
        nameof(Spells.HeartseekerBarrage),
        nameof(Spells.HighwindArrow),
        nameof(Spells.StarfallVolley),
        nameof(Spells.LunarlightMark),
    })]
    private void OnSpenderCast(CastEvent e)
    {
        if (OpenWindow() is not { } window)
            return;

        window.Spenders++;
        window.FocusSpent += DiscountedFocusCost(e);
    }

    private WindowState? OpenWindow()
    {
        var last = _windows.Count > 0 ? _windows[^1] : null;
        return last is { End: null } ? last : null;
    }

    private static int DiscountedFocusCost(CastEvent e) =>
        (SpellRegistry.MaybeGet(e.Ability.FSLID)?.FocusCost ?? 0) / 2;

    private static ClassResource? FocusSnapshot(Event e)
    {
        var resources = e.SourceResources?.Resources;
        if (resources is null)
            return null;

        foreach (var resource in resources)
            if (resource.Type == ResourceTypes.Primary)
                return resource;

        return null;
    }

    /// <summary>
    /// One Event Horizon cast and the spending it converted. <paramref name="FocusAtCast"/> is the
    /// Focus banked going in, or <c>null</c> when the cast carried no snapshot.
    /// <paramref name="FocusSpentInWindow"/> is approximate: registry costs halved per cast.
    /// </summary>
    public sealed record EventHorizonWindow(
        int Timestamp,
        int? FocusAtCast,
        int SpendersInWindow,
        int FocusSpentInWindow);

    private sealed class WindowState
    {
        public int Timestamp { get; init; }
        public int? FocusAtCast { get; init; }
        public int? End { get; set; }
        public int Spenders { get; set; }
        public int FocusSpent { get; set; }
    }
}
