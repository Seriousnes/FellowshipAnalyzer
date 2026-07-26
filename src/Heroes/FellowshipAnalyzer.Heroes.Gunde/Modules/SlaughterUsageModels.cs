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
/// window, whether Heart Splitter had been used to build Rend since the previous Slaughter, how
/// many enemies its bleed spread to, and the Slaughter bleed damage that cast went on to deal.
/// </summary>
public sealed record SlaughterEvaluation
{
    /// <summary>Absolute timestamp of the Slaughter cast, in milliseconds.</summary>
    public required int Timestamp { get; init; }

    /// <summary>Whether a Rupture Open Wounds window covered the cast.</summary>
    public required bool OpenWoundsActive { get; init; }

    /// <summary>Whether Heart Splitter rebuilt Rend between the previous Slaughter and this one.</summary>
    public required bool HeartSplitterPrimed { get; init; }

    /// <summary>
    /// Distinct enemy units the Slaughter bleed reached, counted per (TargetId, TargetInstance) so
    /// that a pack of identical enemies counts once per enemy rather than once per npc type.
    /// </summary>
    public required int TargetsHit { get; init; }

    /// <summary>
    /// Slaughter bleed damage attributed to this cast: every Slaughter damage-over-time tick between
    /// this cast and the next one. This is the Rend the cast actually cashed out.
    /// </summary>
    public required long PayoffDamage { get; init; }

    /// <summary>Whether the cast met the success bar for its pull shape.</summary>
    public bool WellExecuted { get; init; }
}
