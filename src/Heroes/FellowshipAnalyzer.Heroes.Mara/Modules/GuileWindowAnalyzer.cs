using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;

using MaraTalents = FellowshipAnalyzer.Core.Common.Spells.MaraTalents;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

public sealed record GuileWindow
{
    public required int OpenedAt { get; init; }
    public required int ClosedAt { get; init; }

    public int DurationMs => Math.Max(0, ClosedAt - OpenedAt);

    public int SpenderCasts { get; init; }

    public bool Converted => SpenderCasts > 0;

    public int QueensFangStacksAtOpen { get; init; }

    public int ArachnidAssaultStacksAtOpen { get; init; }
}

[ForPull(PullKind.Single | PullKind.Multi)]
[RequiresTalent(MaraTalents.AssassinsGuile)]
public sealed partial class GuileWindowAnalyzer : Analyzer
{
    public const int MaxMalevolenceStacks = 2;

    private readonly List<GuileSpan> _spans = [];

    private GuileSpan? _openSpan;
    private int _queensFangStacks;
    private int _arachnidAssaultStacks;

    public IReadOnlyList<GuileWindow> Windows => field ??= Build();

    public int WindowCount => Windows.Count;

    public int ConvertedWindows => Windows.Count(window => window.Converted);

    public int WindowsAtMaxStacks => Windows.Count(window =>
        window.QueensFangStacksAtOpen >= MaxMalevolenceStacks
        && window.ArachnidAssaultStacksAtOpen >= MaxMalevolenceStacks);

    public int SpendersInWindows => Windows.Sum(window => window.SpenderCasts);

    public bool MalevolenceSeen { get; private set; }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.AssassinsGuileBuff))]
    private void OnGuileApplied(ApplyBuffEvent buffEvent) => OpenWindow(buffEvent.Timestamp);

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.AssassinsGuileBuff))]
    private void OnGuileRefreshed(RefreshBuffEvent buffEvent) => OpenWindow(buffEvent.Timestamp);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.AssassinsGuileBuff))]
    private void OnGuileRemoved(RemoveBuffEvent buffEvent)
    {
        if (_openSpan is null) return;

        _openSpan.ClosedAt = Math.Max(_openSpan.OpenedAt, buffEvent.Timestamp);
        _openSpan = null;
    }

    [On<CastEvent>(By = Actor.Player, Spells = [nameof(Spells.QueenFang), nameof(Spells.ArachnidAssault)])]
    private void OnSpenderCast(CastEvent castEvent)
    {
        if (castEvent.Fake) return;

        if (_openSpan is { } span)
            span.SpenderCasts++;
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.MalevolenceQueensFang))]
    private void OnQueensFangStackApplied(ApplyBuffEvent buffEvent) =>
        _queensFangStacks = FirstStack();

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.MalevolenceQueensFang))]
    private void OnQueensFangStackGained(ApplyBuffStackEvent buffEvent) =>
        _queensFangStacks = Gained(buffEvent.Stack, _queensFangStacks);

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.MalevolenceQueensFang))]
    private void OnQueensFangStackLost(RemoveBuffStackEvent buffEvent) =>
        _queensFangStacks = Lost(buffEvent.Stack, _queensFangStacks);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.MalevolenceQueensFang))]
    private void OnQueensFangStacksRemoved(RemoveBuffEvent buffEvent) =>
        _queensFangStacks = Cleared();

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.MalevolenceArachnidAssault))]
    private void OnArachnidStackApplied(ApplyBuffEvent buffEvent) =>
        _arachnidAssaultStacks = FirstStack();

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.MalevolenceArachnidAssault))]
    private void OnArachnidStackGained(ApplyBuffStackEvent buffEvent) =>
        _arachnidAssaultStacks = Gained(buffEvent.Stack, _arachnidAssaultStacks);

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.MalevolenceArachnidAssault))]
    private void OnArachnidStackLost(RemoveBuffStackEvent buffEvent) =>
        _arachnidAssaultStacks = Lost(buffEvent.Stack, _arachnidAssaultStacks);

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.MalevolenceArachnidAssault))]
    private void OnArachnidStacksRemoved(RemoveBuffEvent buffEvent) =>
        _arachnidAssaultStacks = Cleared();

    private int FirstStack()
    {
        MalevolenceSeen = true;
        return 1;
    }

    private int Gained(int stack, int current)
    {
        MalevolenceSeen = true;
        return stack > 0 ? stack : current + 1;
    }

    private int Lost(int stack, int current)
    {
        MalevolenceSeen = true;
        return stack > 0 ? stack : Math.Max(0, current - 1);
    }

    private int Cleared()
    {
        MalevolenceSeen = true;
        return 0;
    }

    private void OpenWindow(int timestamp)
    {
        if (_openSpan is not null) return;

        _openSpan = new GuileSpan(timestamp)
        {
            QueensFangStacksAtOpen = _queensFangStacks,
            ArachnidAssaultStacksAtOpen = _arachnidAssaultStacks,
        };
        _spans.Add(_openSpan);
    }

    private List<GuileWindow> Build()
    {
        var windows = new List<GuileWindow>(_spans.Count);
        foreach (var span in _spans)
        {
            windows.Add(new GuileWindow
            {
                OpenedAt = span.OpenedAt,
                ClosedAt = span.ClosedAt ?? Math.Max(span.OpenedAt, Pull.EndTime),
                SpenderCasts = span.SpenderCasts,
                QueensFangStacksAtOpen = span.QueensFangStacksAtOpen,
                ArachnidAssaultStacksAtOpen = span.ArachnidAssaultStacksAtOpen,
            });
        }
        return windows;
    }

    private sealed class GuileSpan(int openedAt)
    {
        public int OpenedAt { get; } = openedAt;
        public int? ClosedAt { get; set; }
        public int SpenderCasts { get; set; }
        public int QueensFangStacksAtOpen { get; init; }
        public int ArachnidAssaultStacksAtOpen { get; init; }
    }
}
