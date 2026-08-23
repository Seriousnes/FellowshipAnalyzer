using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using SpellRegistry = FellowshipAnalyzer.Core.Common.Spells.SpellRegistry;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class FocusEconomyAnalyzer : Analyzer
{
    public const int NearCapFocus = 90;

    private readonly List<WindowState> _windows = [];

    public int FocusedShotCasts { get; private set; }

    public int FocusedShotCastsNearCap { get; private set; }

    public int SampledEvents { get; private set; }

    public int SampledAtCap { get; private set; }

    public List<EventHorizonWindow> EventHorizonCasts =>
        field ??=
        [
            .. _windows.Select(window => new EventHorizonWindow(
                window.Timestamp,
                window.FocusAtCast,
                window.Spenders,
                window.FocusSpent)),
        ];

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
