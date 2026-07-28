using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class FocusedWrathAnalyzer : Analyzer
{
    /// <summary>Buff lifetime from <c>Focused Wrath.Duration</c>.</summary>
    public const int BuffDurationMs = 15_000;

    /// <summary>Stack ceiling from <c>Focused Wrath.MaxStacksLimit</c>.</summary>
    public const int MaxStackLimit = 3;

    /// <summary>Stacks a Focused Wrath cast grants, from <c>Focused Wrath.NumOfStacks</c>.</summary>
    public const int StacksPerCast = 2;

    /// <summary>How close to a buff application a Focused Wrath cast must sit for the application to be attributed to it rather than to a Leap Smash proc.</summary>
    public const int CastAttributionMs = 400;

    /// <summary>Grace after a buff removal within which a spender cast still counts as the one that consumed it; the removal and the consuming cast share a timestamp in practice.</summary>
    public const int ConsumptionGraceMs = 500;

    private static readonly int SkullCrusherId = Spells.SkullCrusher.FSLID;
    private static readonly int HammerStormId = Spells.HammerStorm.FSLID;
    private static readonly int CullingStrikeId = Spells.CullingStrike.FSLID;

    private readonly List<OpenWindow> _windows = [];
    private readonly List<int> _casts = [];
    private readonly List<SpenderCast> _spenders = [];

    private OpenWindow? _open;

    private List<FocusedWrathWindow> Evaluated => field ??= Build();

    public IReadOnlyList<FocusedWrathWindow> Windows => Evaluated;

    public int WindowCount => Evaluated.Count;

    public int ConsumedWindows => Evaluated.Count(window => window.Consumed);

    public int ExpiredWindows => Evaluated.Count(window => !window.Consumed && !window.ClippedByPullEnd);

    public int WindowsFromCast => Evaluated.Count(window => window.FromCast);

    public int WindowsFromProc => Evaluated.Count(window => !window.FromCast);

    public IReadOnlyList<int> CastTimestamps => _casts;

    public int CastCount => _casts.Count;

    /// <summary>Focused Wrath presses whose own buff reached a spender. The cast is the part of the mechanic that is a cooldown you can mistime, so it is counted apart from the Leap Smash procs.</summary>
    public int CastsWithConsumedBuff => Evaluated.Count(window => window.FromCast && window.Consumed);

    /// <summary>Focused Wrath presses whose own buff fell off with no spender to take the discount.</summary>
    public int CastsWithoutSpender =>
        Evaluated.Count(window => window.FromCast && !window.Consumed && !window.ClippedByPullEnd);

    public int MaxStacksReached => Evaluated.Count == 0 ? 0 : Evaluated.Max(window => window.MaxStacks);

    public int SkullCrusherConsumptions => CountConsumers(SkullCrusherId);

    public int HammerStormConsumptions => CountConsumers(HammerStormId);

    public int CullingStrikeConsumptions => CountConsumers(CullingStrikeId);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.FocusedWrath))]
    private void OnFocusedWrathCast(CastEvent @event)
    {
        if (@event.Fake)
            return;

        _casts.Add(@event.Timestamp);
    }

    [On<CastEvent>(By = Actor.Player, Spells = new[]
    {
        nameof(Spells.SkullCrusher),
        nameof(Spells.HammerStorm),
        nameof(Spells.CullingStrike),
    })]
    private void OnSpenderCast(CastEvent @event)
    {
        if (@event.Fake)
            return;

        _spenders.Add(new SpenderCast(@event.Timestamp, @event.Ability.Id));
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.FocusedWrathSelfBuff))]
    private void OnBuffApplied(ApplyBuffEvent @event)
    {
        if (_open is not null)
            Close(@event.Timestamp);

        _open = new OpenWindow(@event.Timestamp);
        _windows.Add(_open);
    }

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.FocusedWrathSelfBuff))]
    private void OnStackGained(ApplyBuffStackEvent @event)
    {
        if (_open is null)
        {
            _open = new OpenWindow(@event.Timestamp);
            _windows.Add(_open);
        }

        _open.MaxStacks = Math.Max(_open.MaxStacks, @event.Stack);
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.FocusedWrathSelfBuff))]
    private void OnBuffRemoved(RemoveBuffEvent @event) => Close(@event.Timestamp);

    private void Close(int timestamp)
    {
        if (_open is null)
            return;

        _open.ClosedAt = timestamp;
        _open = null;
    }

    private int CountConsumers(int spellId)
    {
        var count = 0;
        foreach (var window in Evaluated)
        {
            foreach (var consumer in window.SpenderSpellIds)
            {
                if (consumer == spellId)
                    count++;
            }
        }

        return count;
    }

    private List<FocusedWrathWindow> Build()
    {
        var built = new List<FocusedWrathWindow>(_windows.Count);

        foreach (var window in _windows)
        {
            var closedAt = window.ClosedAt ?? Pull.EndTime;
            var spenders = new List<int>();
            foreach (var spender in _spenders)
            {
                if (spender.Timestamp < window.OpenedAt)
                    continue;
                if (spender.Timestamp > closedAt + ConsumptionGraceMs)
                    break;

                spenders.Add(spender.SpellId);
            }

            built.Add(new FocusedWrathWindow
            {
                OpenedAt = window.OpenedAt,
                ClosedAt = closedAt,
                ClippedByPullEnd = window.ClosedAt is null,
                FromCast = _casts.Any(cast => cast <= window.OpenedAt && window.OpenedAt - cast <= CastAttributionMs),
                MaxStacks = window.MaxStacks,
                SpenderSpellIds = spenders,
            });
        }

        return built;
    }

    private sealed class OpenWindow(int openedAt)
    {
        public int OpenedAt { get; } = openedAt;
        public int? ClosedAt { get; set; }
        public int MaxStacks { get; set; } = 1;
    }

    private readonly record struct SpenderCast(int Timestamp, int SpellId);
}

public sealed record FocusedWrathWindow
{
    public required int OpenedAt { get; init; }

    public required int ClosedAt { get; init; }

    public int DurationMs => ClosedAt - OpenedAt;

    public required bool ClippedByPullEnd { get; init; }

    /// <summary>The window opened on a Focused Wrath cast rather than on a Leap Smash proc (<c>Leap Smash.Talent.ProcCostReductionBuff</c>).</summary>
    public required bool FromCast { get; init; }

    public required int MaxStacks { get; init; }

    public IReadOnlyList<int> SpenderSpellIds { get; init; } = [];

    public int SpenderCasts => SpenderSpellIds.Count;

    public bool Consumed => SpenderSpellIds.Count > 0;

    public int? ConsumedBy => SpenderSpellIds.Count > 0 ? SpenderSpellIds[0] : null;
}
