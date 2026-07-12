namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>The pull shape a <see cref="SlaughterUsageAnalyzer"/> scored its Slaughters under.</summary>
public enum GundePullShape
{
    /// <summary>Boss pull - single-target priority, scored on Open Wounds timing and Heart Splitter priming.</summary>
    Boss,

    /// <summary>Trash pull - AoE priority, scored on Open Wounds timing and how much of the pack each Slaughter hits.</summary>
    Aoe,
}

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
}
