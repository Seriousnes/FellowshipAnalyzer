using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>The pull shape a <see cref="SlaughterUsageReport"/> was scored under.</summary>
public enum GundePullShape
{
    /// <summary>Boss pull — single-target priority, scored on Open Wounds timing and Heart Splitter priming.</summary>
    Boss,

    /// <summary>Trash pull — AoE priority, scored on Open Wounds timing and how much of the pack each Slaughter hits.</summary>
    Aoe,
}

/// <summary>A finding surfaced by the Slaughter usage analyzer.</summary>
public sealed record GundeFinding(
    string Severity,
    string Message,
    int? Timestamp = null);

/// <summary>
/// Per-cast evaluation of a single Slaughter: whether it landed inside a Rupture Open Wounds
/// window, whether Heart Splitter had been used to build Rend since the previous Slaughter, and how
/// many enemies its bleed spread to.
/// </summary>
public sealed class SlaughterEvaluation
{
    public int Timestamp { get; init; }
    public bool OpenWoundsActive { get; init; }
    public bool HeartSplitterPrimed { get; init; }
    public int TargetsHit { get; set; }
    public bool WellExecuted { get; set; }
    public string Outcome { get; set; } = string.Empty;
}

/// <summary>
/// Immutable per-pull projection of <see cref="SlaughterUsageAnalyzer"/> state, produced by
/// <see cref="SlaughterUsageAnalyzer.OnPullEnd"/> for each pull. Both the boss and AoE leaves feed a
/// single cross-pull stream, disambiguated by <see cref="Shape"/>.
/// </summary>
public sealed record SlaughterUsageReport(
    AnalyzerScoreCard ScoreCard,
    GundePullShape Shape,
    int SlaughterCasts,
    int OpenWoundsTimed,
    int HeartSplitterPrimed,
    int WellExecuted,
    IReadOnlyList<SlaughterEvaluation> Slaughters,
    IReadOnlyList<GundeFinding> Findings) : IResult
{
    public SlaughterUsageReport() : this(
        new AnalyzerScoreCard("Slaughter Efficiency", 0, "No Slaughter casts detected.", "amber"),
        GundePullShape.Boss, 0, 0, 0, 0, [], [])
    {
    }
}
