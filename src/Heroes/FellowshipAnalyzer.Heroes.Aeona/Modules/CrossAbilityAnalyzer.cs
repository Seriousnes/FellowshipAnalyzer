using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.UI;

using FSLID = FellowshipAnalyzer.Core.Common.Spells.FSLID;

namespace FellowshipAnalyzer.Heroes.Aeona.Modules;

/// <summary>One cleanse cast and the mana attributed to it.</summary>
/// <param name="Timestamp">When the cast completed.</param>
/// <param name="Ability">The cleanse cast.</param>
/// <param name="Mana">The mana attributed to the cast.</param>
public sealed record CleanseManaReturn(int Timestamp, FSLID Ability, int Mana);

/// <summary>
/// The figures that belong to no single ability: the Chrona and mana generated above the maximum, the
/// time every Twilight Skybolt charge was available, the mana cleansing returned, and the Unfolding
/// Doom duration overwritten.
/// </summary>
/// <remarks>
/// Registered dungeon-lifetime so the whole report is in one card, and slices itself by pull from
/// <see cref="CombatLogParser.Pulls"/>. Twilight Skybolt and Unfolding Doom are measured per pull, so
/// their analyzers are read off <see cref="Analyzer.Owner"/> rather than taken as dependencies.
/// </remarks>
[Dependency<ChronaTracker>]
[Dependency<StaggerTracker>]
public sealed partial class CrossAbilityAnalyzer : Analyzer
{
    /// <summary>How long after a cleanse cast an arriving mana gain is attributed to it.</summary>
    public const int CleanseReturnWindowMs = 1_000;

    private int? _chronaGenerated;
    private int? _chronaOvercapped;
    private int? _manaGenerated;
    private int? _manaOvercapped;

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Resources;

    /// <summary>Chrona generated across every pull.</summary>
    public int ChronaGenerated => _chronaGenerated ??= SumOverPulls(ResourceTypes.Primary, gain => gain.Usable);

    /// <summary>Chrona generated above the maximum across every pull.</summary>
    public int ChronaOvercapped => _chronaOvercapped ??= SumOverPulls(ResourceTypes.Primary, gain => gain.Overcap);

    /// <summary>Share of the Chrona generated (0-1) above the maximum.</summary>
    public double ChronaOvercapShare => Share(ChronaOvercapped, ChronaGenerated + ChronaOvercapped);

    /// <summary>Mana generated across every pull.</summary>
    public int ManaGenerated => _manaGenerated ??= SumOverPulls(ResourceTypes.Mana, gain => gain.Usable);

    /// <summary>Mana generated above the maximum across every pull.</summary>
    public int ManaOvercapped => _manaOvercapped ??= SumOverPulls(ResourceTypes.Mana, gain => gain.Overcap);

    /// <summary>Share of the mana generated (0-1) above the maximum.</summary>
    public double ManaOvercapShare => Share(ManaOvercapped, ManaGenerated + ManaOvercapped);

    /// <summary>The player's maximum mana.</summary>
    public int ManaMaximum => ChronaTracker.MaxOf(ResourceTypes.Mana);

    /// <summary>Cleanse casts with mana attributed.</summary>
    public IReadOnlyList<CleanseManaReturn> CleanseManaReturns => field ??= MeasureCleanseReturns();

    /// <summary>Mana attributed to cleansing.</summary>
    public int ManaFromCleansing => CleanseManaReturns.Sum(cleanse => cleanse.Mana);

    /// <summary>Whether Twilight Skybolt was cast.</summary>
    public bool SkyboltWasCast => SkyboltAnalyzers.Any(analyzer => analyzer.CastCount > 0);

    /// <summary>Time every Twilight Skybolt charge was available, in milliseconds.</summary>
    public int SkyboltTimeAtMaxChargesMs => SkyboltAnalyzers.Sum(analyzer => analyzer.TimeAtMaxChargesMs);

    /// <summary>Share of combat (0-1) every Twilight Skybolt charge was available.</summary>
    public double SkyboltTimeAtMaxChargesShare => Share(SkyboltTimeAtMaxChargesMs, CombatMs);

    /// <summary>Unfolding Doom duration overwritten, in milliseconds.</summary>
    public int UnfoldingDoomOverwrittenMs => DoomAnalyzers.Sum(analyzer => analyzer.OverlappedMs);

    /// <summary>Unfolding Doom reapplications.</summary>
    public int UnfoldingDoomReapplications => DoomAnalyzers.Sum(analyzer => analyzer.Reapplications.Count);

    /// <summary>Combat time, summed over every pull.</summary>
    public int CombatMs => Owner.Pulls.Sum(pull => pull.Duration);

    private Analysis.AeonaCombatLogParser Parser => (Analysis.AeonaCombatLogParser)Owner;

    private List<TwilightSkyboltAnalyzer> SkyboltAnalyzers =>
        field ??= [.. Parser.TwilightSkyboltAnalyzers.Select(entry => entry.Analyzer)];

    private List<UnfoldingDoomAnalyzer> DoomAnalyzers =>
        field ??= [.. Parser.UnfoldingDoomAnalyzers.Select(entry => entry.Analyzer).OfType<UnfoldingDoomAnalyzer>()];

    private int SumOverPulls(ResourceTypes type, Func<ResourceGain, int> select)
    {
        var total = 0;
        foreach (var pull in Owner.Pulls)
            total += ChronaTracker.GainsBetween(type, pull.StartTime, pull.EndTime).Sum(select);

        return total;
    }

    private List<CleanseManaReturn> MeasureCleanseReturns()
    {
        var returns = new List<CleanseManaReturn>();
        var casts = StaggerTracker.CleanseCastsBetween(Owner.DungeonStartTime, Owner.DungeonEndTime);

        for (var i = 0; i < casts.Count; i++)
        {
            var cast = casts[i];
            var limit = Math.Min(
                cast.Timestamp + CleanseReturnWindowMs,
                i + 1 < casts.Count ? casts[i + 1].Timestamp - 1 : int.MaxValue);

            if (limit < cast.Timestamp) continue;

            var mana = ChronaTracker
                .GainsBetween(ResourceTypes.Mana, cast.Timestamp, limit)
                .Sum(gain => gain.Usable);

            if (mana <= 0) continue;

            returns.Add(new CleanseManaReturn(cast.Timestamp, cast.Ability, mana));
        }

        return returns;
    }

    private static double Share(int part, int whole) => whole > 0 ? (double)part / whole : 0;
}
