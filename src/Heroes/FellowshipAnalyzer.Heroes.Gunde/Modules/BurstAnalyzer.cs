using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class BurstAnalyzer : Analyzer
{
    public const int BuffDurationMs = 12_000;

    public const int LeadInMs = 3_000;

    private readonly List<RawWindow> _rawWindows = [];
    private readonly List<int> _ruptures = [];
    private readonly List<int> _bloodboundSpirits = [];
    private readonly List<int> _owedInBloods = [];

    private RawWindow? _open;

    private Computed Result => field ??= Compute();

    public IReadOnlyList<BurstWindow> Windows => Result.Windows;

    public int WindowCount => Result.Windows.Count;

    public int FullWindows => Result.FullWindows;

    public int OutOfWindowRuptures => Result.OutOfWindowRuptures;

    public int OutOfWindowBloodboundSpirits => Result.OutOfWindowBloodboundSpirits;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ReignInBloodSelfBuff))]
    private void OnWindowOpened(ApplyBuffEvent @event)
    {
        if (_open is not null) return;

        _open = new RawWindow(@event.Timestamp);
        _rawWindows.Add(_open);
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ReignInBloodSelfBuff))]
    private void OnWindowClosed(RemoveBuffEvent @event)
    {
        if (_open is null) return;

        _open.End = @event.Timestamp;
        _open = null;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Rupture))]
    private void OnRupture(CastEvent @event) => _ruptures.Add(@event.Timestamp);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.BloodboundSpirit))]
    private void OnBloodboundSpirit(CastEvent @event) => _bloodboundSpirits.Add(@event.Timestamp);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.OwedInBlood))]
    private void OnOwedInBlood(CastEvent @event) => _owedInBloods.Add(@event.Timestamp);

    private Computed Compute()
    {
        var ruptureClaimed = new bool[_ruptures.Count];
        var spiritClaimed = new bool[_bloodboundSpirits.Count];
        var owedClaimed = new bool[_owedInBloods.Count];

        var windows = new List<BurstWindow>(_rawWindows.Count);
        var spans = new List<WindowSpan>(_rawWindows.Count);
        var full = 0;
        foreach (var raw in _rawWindows)
        {
            var end = raw.End ?? Math.Min(raw.Start + BuffDurationMs, Pull.EndTime);
            var window = new BurstWindow(
                raw.Start,
                end,
                Claim(_ruptures, ruptureClaimed, raw.Start, end),
                Claim(_bloodboundSpirits, spiritClaimed, raw.Start, end),
                Claim(_owedInBloods, owedClaimed, raw.Start, end));

            if (window.PresentCount == BurstWindow.CooldownCount) full++;
            windows.Add(window);
            spans.Add(new WindowSpan(raw.Start - LeadInMs, end));
        }

        return new Computed(
            windows,
            full,
            CountOutside(_ruptures, spans),
            CountOutside(_bloodboundSpirits, spans));
    }

    private static bool Claim(List<int> casts, bool[] claimed, int start, int end)
    {
        for (var i = 0; i < casts.Count; i++)
        {
            var timestamp = casts[i];
            if (timestamp > end) break;
            if (claimed[i] || timestamp < start - LeadInMs) continue;

            claimed[i] = true;
            return true;
        }

        return false;
    }

    private static int CountOutside(List<int> casts, List<WindowSpan> spans) =>
        casts.Count(timestamp => !spans.Any(span => timestamp >= span.Start && timestamp <= span.End));

    private sealed class RawWindow(int start)
    {
        public int Start { get; } = start;
        public int? End { get; set; }
    }

    private readonly record struct WindowSpan(int Start, int End);

    private sealed record Computed(
        IReadOnlyList<BurstWindow> Windows,
        int FullWindows,
        int OutOfWindowRuptures,
        int OutOfWindowBloodboundSpirits);

    public sealed record BurstWindow(int Start, int End, bool RuptureIn, bool BloodboundSpiritIn, bool OwedInBloodIn)
    {
        public const int CooldownCount = 3;

        public int PresentCount => (RuptureIn ? 1 : 0) + (BloodboundSpiritIn ? 1 : 0) + (OwedInBloodIn ? 1 : 0);
    }
}
