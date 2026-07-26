using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;

using MaraTalents = FellowshipAnalyzer.Core.Common.Spells.MaraTalents;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

/// <summary>
/// One Assassin's Guile window: the stretch the buff stood, the boosted spenders pressed inside it,
/// and the Malevolence stacks Mara carried into it.
/// </summary>
public sealed record GuileWindow
{
    public required int OpenedAt { get; init; }
    public required int ClosedAt { get; init; }

    public int DurationMs => Math.Max(0, ClosedAt - OpenedAt);

    /// <summary>Queen's Fang and Arachnid Assault casts made inside the window.</summary>
    public int SpenderCasts { get; init; }

    /// <summary>Whether the window was spent on at least one boosted ability.</summary>
    public bool Converted => SpenderCasts > 0;

    /// <summary>Malevolence: Queen's Fang stacks standing when the window opened.</summary>
    public int QueensFangStacksAtOpen { get; init; }

    /// <summary>Malevolence: Arachnid Assault stacks standing when the window opened.</summary>
    public int ArachnidAssaultStacksAtOpen { get; init; }
}

/// <summary>
/// Measures Mara's Assassin's Guile windows. A stealth attack grants the buff for five seconds, during
/// which Queen's Fang and Arachnid Assault hit 40% harder, so each window is scored on the spenders
/// that landed inside it. A refresh while the buff still stands extends the same window rather than
/// opening a second one, and a window whose removal is never logged is capped at
/// <see cref="Analyzer.Pull"/>'s end time.
/// <para>
/// The two Malevolence buffs are tracked alongside, because they decide what a window is worth: casting
/// one spender stacks the other's buff up to <see cref="MaxMalevolenceStacks"/>, and a stack doubles
/// that ability's damage. Each window records both stack counts as it opened, so entering a window with
/// both spenders fully stacked can be told apart from entering one cold. Stack counts come from the
/// buff stream (apply, stack and remove), preferring the count the event carries over an increment.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[RequiresTalent(MaraTalents.AssassinsGuile)]
public sealed partial class GuileWindowAnalyzer : Analyzer
{
    /// <summary>The stack cap on either Malevolence buff.</summary>
    public const int MaxMalevolenceStacks = 2;

    private readonly List<GuileSpan> _spans = [];

    private GuileSpan? _openSpan;
    private int _queensFangStacks;
    private int _arachnidAssaultStacks;

    private IReadOnlyList<GuileWindow>? _windows;

    /// <summary>Every Assassin's Guile window on the pull, in encounter order.</summary>
    public IReadOnlyList<GuileWindow> Windows => _windows ??= Build();

    public int WindowCount => Windows.Count;

    /// <summary>Windows that carried at least one Queen's Fang or Arachnid Assault.</summary>
    public int ConvertedWindows => Windows.Count(window => window.Converted);

    /// <summary>Windows entered with both Malevolence buffs at their stack cap.</summary>
    public int WindowsAtMaxStacks => Windows.Count(window =>
        window.QueensFangStacksAtOpen >= MaxMalevolenceStacks
        && window.ArachnidAssaultStacksAtOpen >= MaxMalevolenceStacks);

    /// <summary>Queen's Fang and Arachnid Assault casts made inside a window.</summary>
    public int SpendersInWindows => Windows.Sum(window => window.SpenderCasts);

    /// <summary>
    /// Whether either Malevolence buff was seen on the pull. False means the talent is not taken (or
    /// never procced), so the stack readings carry no information.
    /// </summary>
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

    /// <summary>An Assassin's Guile window while it is still being accumulated from the event stream.</summary>
    private sealed class GuileSpan(int openedAt)
    {
        public int OpenedAt { get; } = openedAt;
        public int? ClosedAt { get; set; }
        public int SpenderCasts { get; set; }
        public int QueensFangStacksAtOpen { get; init; }
        public int ArachnidAssaultStacksAtOpen { get; init; }
    }
}
