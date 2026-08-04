using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Utility;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

/// <summary>
/// One Matriarch Macabre window, from the buff landing to its removal, with the imitated casts inside it.
/// </summary>
public sealed record MatriarchMacabreWindow
{
    /// <summary>When the Matriarch Macabre buff landed.</summary>
    public required int OpenedAt { get; init; }

    /// <summary>When the Matriarch Macabre buff came off, or the pull ended.</summary>
    public required int ClosedAt { get; init; }

    /// <summary>How long the buff was active.</summary>
    public int DurationMs => Math.Max(0, ClosedAt - OpenedAt);

    /// <summary>Queen's Fang casts made inside the window.</summary>
    public int QueensFangCasts { get; init; }

    /// <summary>Arachnid Assault casts made inside the window.</summary>
    public int ArachnidAssaultCasts { get; init; }

    /// <summary>
    /// Casts the clones imitated: the Queen's Fang and Arachnid Assault casts made while the buff was active.
    /// </summary>
    public int ImitatedCasts => QueensFangCasts + ArachnidAssaultCasts;

    /// <summary>Whether the window carried at least one cast the clones could imitate.</summary>
    public bool Converted => ImitatedCasts > 0;
}

/// <summary>
/// Measures what each Matriarch Macabre window carried. The clones imitate Queen's Fang and Arachnid Assault
/// specifically, so the window is worth the number of those casts made while it is active and nothing else.
/// Maiden of Death takes no part in this measurement: the two cooldowns are assessed independently, and
/// neither is judged on the presence of the other.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class MatriarchMacabreAnalyzer : Analyzer
{
    /// <summary>
    /// Matriarch Macabre's buff duration in milliseconds (<c>Mara.Constants["Matriarch Macabre"].Duration</c>
    /// in the season 3 game data). The spell registry carries no duration, so the value is held here.
    /// </summary>
    public const int WindowMs = 20_000;

    /// <summary>The damage increase Matriarch Macabre grants while it is active.</summary>
    public const double DamageIncrease = 0.20;

    private readonly List<MacabreSpan> _spans = [];

    private MacabreSpan? _openSpan;

    /// <summary>Every Matriarch Macabre window on this pull.</summary>
    public IReadOnlyList<MatriarchMacabreWindow> Windows => field ??= Build();

    /// <summary>How many Matriarch Macabre windows the pull contained.</summary>
    public int WindowCount => Windows.Count;

    /// <summary>Matriarch Macabre casts made on this pull.</summary>
    public int MatriarchMacabreCasts { get; private set; }

    /// <summary>Windows that carried at least one cast the clones could imitate.</summary>
    public int ConvertedWindows => Windows.Count(window => window.Converted);

    /// <summary>Queen's Fang and Arachnid Assault casts made inside a window.</summary>
    public int ImitatedCasts => Windows.Sum(window => window.ImitatedCasts);

    /// <summary>Queen's Fang casts made inside a window.</summary>
    public int QueensFangCasts => Windows.Sum(window => window.QueensFangCasts);

    /// <summary>Arachnid Assault casts made inside a window.</summary>
    public int ArachnidAssaultCasts => Windows.Sum(window => window.ArachnidAssaultCasts);

    /// <summary>Average number of imitated casts each window carried.</summary>
    public double AverageImitatedCasts =>
        Windows.Count == 0 ? 0d : (double)ImitatedCasts / Windows.Count;

    /// <summary>
    /// The damage Matriarch Macabre's increase accounts for across the pull's windows.
    /// <para>
    /// Measured per hit against Matriarch Macabre's own multiplier. Maiden of Death can be active over
    /// the same hit, and each buff is measured as though the other still applied, so this figure and
    /// Maiden of Death's overlap. Read them separately; never sum them, and never render either as a
    /// share of the pull's damage.
    /// </para>
    /// </summary>
    public long AddedDamage { get; private set; }

    [On<DamageEvent>(By = Actor.Player)]
    private void OnDamageDealt(DamageEvent damageEvent)
    {
        if (_openSpan is null) return;

        AddedDamage += CombatMath.CalculateEffectiveDamage(damageEvent, DamageIncrease);
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.MatriarchMacabre))]
    private void OnMatriarchCast(CastEvent castEvent)
    {
        if (castEvent.Fake)
            return;

        MatriarchMacabreCasts++;
    }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.MatriarchMacabreSelfBuff))]
    private void OnBuffApplied(ApplyBuffEvent buffEvent)
    {
        if (_openSpan is not null)
            return;

        _openSpan = new MacabreSpan(buffEvent.Timestamp);
        _spans.Add(_openSpan);
    }

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.MatriarchMacabreSelfBuff))]
    private void OnBuffRefreshed(RefreshBuffEvent buffEvent)
    {
        if (_openSpan is not null)
            return;

        _openSpan = new MacabreSpan(buffEvent.Timestamp);
        _spans.Add(_openSpan);
    }

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.MatriarchMacabreSelfBuff))]
    private void OnBuffRemoved(RemoveBuffEvent buffEvent)
    {
        if (_openSpan is null)
            return;

        _openSpan.ClosedAt = Math.Max(_openSpan.OpenedAt, buffEvent.Timestamp);
        _openSpan = null;
    }

    [On<CastEvent>(By = Actor.Player, Spells = [nameof(Spells.QueenFang), nameof(Spells.ArachnidAssault)])]
    private void OnImitatedCast(CastEvent castEvent)
    {
        if (castEvent.Fake || _openSpan is not { } span)
            return;

        if (castEvent.Ability.Id == Spells.QueenFang.Id)
            span.QueensFangCasts++;
        else
            span.ArachnidAssaultCasts++;
    }

    private List<MatriarchMacabreWindow> Build()
    {
        var windows = new List<MatriarchMacabreWindow>(_spans.Count);
        foreach (var span in _spans)
        {
            windows.Add(new MatriarchMacabreWindow
            {
                OpenedAt = span.OpenedAt,
                ClosedAt = span.ClosedAt ?? Math.Max(span.OpenedAt, Pull.EndTime),
                QueensFangCasts = span.QueensFangCasts,
                ArachnidAssaultCasts = span.ArachnidAssaultCasts,
            });
        }
        return windows;
    }

    private sealed class MacabreSpan(int openedAt)
    {
        public int OpenedAt { get; } = openedAt;
        public int? ClosedAt { get; set; }
        public int QueensFangCasts { get; set; }
        public int ArachnidAssaultCasts { get; set; }
    }
}
