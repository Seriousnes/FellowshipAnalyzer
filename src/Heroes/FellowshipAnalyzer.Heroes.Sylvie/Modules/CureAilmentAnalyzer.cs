using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>
/// What Cure Ailment took off the party this pull, read from the fight-lifetime
/// <see cref="DispelTracker"/> and clipped to the pull. Dispels the party never got to are not events,
/// so this counts what was removed and never guesses at what was missed.
/// <para>
/// A dispel from a weapon ability rather than from Sylvie's kit is counted separately, because the two
/// have nothing to do with each other's cooldowns.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<DispelTracker>]
public sealed partial class CureAilmentAnalyzer : Analyzer
{
    private IReadOnlyList<DispelRecord> Result => field ??= [.. DispelTracker.Between(Pull.StartTime, Pull.EndTime)];

    /// <summary>Every dispel the player performed this pull, in order.</summary>
    public IReadOnlyList<DispelRecord> Dispels => Result;

    /// <summary>Auras Cure Ailment removed this pull.</summary>
    public int CureAilmentDispels => Result.Count(dispel => dispel.SpellId == Spells.CureAilment.FSLID);

    /// <summary>Auras removed this pull by something other than Cure Ailment.</summary>
    public int OtherDispels => Result.Count - CureAilmentDispels;

    /// <summary>Distinct allies Cure Ailment cleaned up this pull.</summary>
    public int TargetsCleansed => Result
        .Where(dispel => dispel.SpellId == Spells.CureAilment.FSLID)
        .Select(dispel => dispel.Target)
        .Distinct()
        .Count();

    /// <summary>Which auras Cure Ailment removed this pull and how often, most-removed first.</summary>
    public IReadOnlyList<RemovedAura> RemovedAuras => field ??=
        DispelTracker.RemovedAurasBetween(Pull.StartTime, Pull.EndTime, Spells.CureAilment.FSLID);
}
