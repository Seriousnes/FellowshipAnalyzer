using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Insights;
using FellowshipAnalyzer.Core.Insights.Aeona;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;
using FellowshipAnalyzer.Heroes.Aeona.Guides;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using static FellowshipAnalyzer.Core.Insights.Digest;

using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;
using AeonaLegendaryItems = FellowshipAnalyzer.Core.Common.Spells.Aeona.Legendaries;
using AeonaTalentTree = FellowshipAnalyzer.Core.Common.Spells.Aeona.Talents;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Insights;

/// <summary>
/// Builds the insight message of each Aeona guide from a parser after <see cref="CombatLogParser.Analyze"/>.
/// </summary>
public static class AeonaDigestBuilder
{
    /// <summary>Guide insights' view of <paramref name="parser"/>.</summary>
    public static HeroInsights Insights(AeonaCombatLogParser parser) => new(
        new Dictionary<string, Type>
        {
            [AeonaGuides.EntropyClaim] = typeof(EntropyClaimGuide),
            [AeonaGuides.Oblivion] = typeof(OblivionGuide),
            [AeonaGuides.StaggerCleanse] = typeof(StaggerCleanseGuide),
            [AeonaGuides.ContinuumShift] = typeof(ContinuumShiftGuide),
            [AeonaGuides.KindRewind] = typeof(KindRewindGuide),
        },
        [.. Spells.Kit, .. AeonaTalentTree.All],
        () => Messages(parser));

    /// <summary>UTF-8 JSON message of each guide with a row, keyed by guide name.</summary>
    public static Dictionary<string, byte[]> Messages(AeonaCombatLogParser parser)
    {
        var build = Digest.Build(parser, AeonaTalentTree.All, AeonaLegendaryItems.All);
        var context = AeonaDigestJsonContext.Default;
        var messages = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        Add(messages, EntropyClaim(parser, build), context.InsightMessageEntropyClaimPull);
        Add(messages, Oblivion(parser, build), context.InsightMessageOblivionPull);
        Add(messages, StaggerCleanse(parser, build), context.InsightMessageStaggerCleansePull);
        Add(messages, ContinuumShift(parser, build), context.InsightMessageContinuumShiftPull);
        Add(messages, KindRewind(parser, build), context.InsightMessageKindRewindPull);
        return messages;
    }

    /// <summary>Entropy's Claim casts of every pull, with the guide's focus.</summary>
    public static InsightMessage<EntropyClaimPull>? EntropyClaim(AeonaCombatLogParser parser, DigestBuild build)
    {
        var grades = new List<QualitativePerformance?>();
        var pulls = new List<EntropyClaimPull>();
        foreach (var (pull, entry) in parser.EntropyClaimAnalyzers)
        {
            if (entry is not EntropyClaimAnalyzer { CastCount: > 0 } analyzer)
                continue;
            grades.Add(EntropyClaimGuide.PullPerformance(analyzer));
            pulls.Add(new EntropyClaimPull(
                Kind(pull),
                analyzer.LapsesWithChargeAvailable > 0,
                [.. analyzer.Casts.Select(cast => new EntropyClaimCastFacts(Timing(cast.LeadBeforeExpiryMs), cast.RolledOver))]));
        }
        return Message(AeonaGuides.EntropyClaim, grades, build, pulls, EntropyClaimPatterns);
    }

    /// <summary>Oblivion casts of every pull, with the guide's focus.</summary>
    public static InsightMessage<OblivionPull>? Oblivion(AeonaCombatLogParser parser, DigestBuild build)
    {
        var grades = new List<QualitativePerformance?>();
        var pulls = new List<OblivionPull>();
        foreach (var (pull, entry) in parser.OblivionAnalyzers)
        {
            if (entry is not OblivionAnalyzer { CastsRated: > 0 } analyzer)
                continue;
            var grade = OblivionGuide.PullPerformance(analyzer);
            grades.Add(grade);
            pulls.Add(new OblivionPull(Kind(pull), grade switch
            {
                QualitativePerformance.Perfect => DigestValues.Never,
                QualitativePerformance.Fail => AeonaDigestValues.Often,
                _ => DigestValues.Sometimes,
            }));
        }
        return Message(AeonaGuides.Oblivion, grades, build, pulls, OblivionPatterns);
    }

    /// <summary>Amend Fate and Restore Continuity casts of every pull, with the guide's focus.</summary>
    public static InsightMessage<StaggerCleansePull>? StaggerCleanse(AeonaCombatLogParser parser, DigestBuild build)
    {
        var echoes = parser.SelectedCombatant.HasTalent(AeonaTalents.EchoesOfDivinity);
        var grades = new List<QualitativePerformance?>();
        var pulls = parser.StaggerCleanseAnalyzers
            .Where(entry => entry.Analyzer.Casts.Count > 0)
            .Select(entry => new StaggerCleansePull(Kind(entry.Pull), [.. entry.Analyzer.Casts.Select(cast =>
            {
                grades.Add(StaggerCleanseGuide.CastPerformance(cast));
                return new CleanseCastFacts(
                    cast.Ability == Spells.AmendFate.FSLID ? Spells.AmendFate.Name : Spells.RestoreContinuity.Name,
                    cast.FreeCastSource switch
                    {
                        FreeCastSource.Uchronia => Spells.Uchronia.Name,
                        FreeCastSource.EpochBreak => Spells.EpochBreak.Name,
                        _ => null,
                    },
                    cast.TankStaggerFraction is { } tank ? tank < OblivionAnalyzer.CleansePriorityStaggerFraction : null,
                    cast.BelowStaggerRemoved,
                    cast.BelowOblivionValue,
                    echoes ? cast.AppliedEchoes : null,
                    echoes ? cast.OverwroteEchoes : null);
            })]))
            .ToList();
        return Message(AeonaGuides.StaggerCleanse, grades, build, pulls, StaggerCleansePatterns);
    }

    /// <summary>Continuum Shift windows of every pull, with the guide's focus.</summary>
    public static InsightMessage<ContinuumShiftPull>? ContinuumShift(AeonaCombatLogParser parser, DigestBuild build)
    {
        var grades = new List<QualitativePerformance?>();
        var pulls = new List<ContinuumShiftPull>();
        foreach (var (pull, entry) in parser.ContinuumShiftAnalyzers)
        {
            if (entry is not ContinuumShiftAnalyzer { Procs: > 0 } analyzer)
                continue;
            pulls.Add(new ContinuumShiftPull(Kind(pull), [.. analyzer.Windows.Select(window =>
            {
                grades.Add(ContinuumShiftGuide.WindowPerformance(window));
                var echoes = window.Spend == ContinuumShiftSpend.EchoesOfRuin;
                var timeShard = window.Spend == ContinuumShiftSpend.TimeShard;
                return new ContinuumShiftFacts(
                    window.Spend switch
                    {
                        ContinuumShiftSpend.TimeShard => Spells.TimeShard.Name,
                        ContinuumShiftSpend.EchoesOfRuin => Spells.EchoesOfRuin.Name,
                        ContinuumShiftSpend.EntropyClaim => Spells.EntropyClaim.Name,
                        ContinuumShiftSpend.Lost => AeonaDigestValues.Nothing,
                        _ => AeonaDigestValues.OpenAtPullEnd,
                    },
                    echoes && window.TankHealthFraction is { } health ? health <= ContinuumShiftAnalyzer.TankDangerHealthFraction : null,
                    echoes && analyzer.EdgeOfRuinTaken ? window.FleetingHourFollowed : null,
                    timeShard ? window.ChronaOvercapped > 0 : null);
            })]));
        }
        return Message(AeonaGuides.ContinuumShift, grades, build, pulls, ContinuumShiftPatterns);
    }

    /// <summary>Time Shards with Kind Rewind of every pull, with the guide's focus.</summary>
    public static InsightMessage<KindRewindPull>? KindRewind(AeonaCombatLogParser parser, DigestBuild build)
    {
        var grades = new List<QualitativePerformance?>();
        var pulls = new List<KindRewindPull>();
        foreach (var (pull, entry) in parser.KindRewindAnalyzers)
        {
            if (entry is not KindRewindAnalyzer { CastCount: > 0 } analyzer)
                continue;
            grades.Add(KindRewindGuide.PullPerformance(analyzer));
            pulls.Add(new KindRewindPull(
                Kind(pull),
                analyzer.CastsWithEchoesOfRuin < analyzer.CastCount));
        }
        return Message(AeonaGuides.KindRewind, grades, build, pulls, KindRewindPatterns);
    }

    /// <summary>
    /// <c>early</c> or <c>late</c> outside <see cref="EntropyClaimAnalyzer.LeadWindowShortestMs"/> to
    /// <see cref="EntropyClaimAnalyzer.LeadWindowLongestMs"/> before the active Entropy's Claim expires,
    /// <c>in window</c> inside it, <c>none active</c> without one.
    /// </summary>
    public static string Timing(int? leadBeforeExpiryMs) => leadBeforeExpiryMs switch
    {
        null => AeonaDigestValues.NoneActive,
        > EntropyClaimAnalyzer.LeadWindowLongestMs => AeonaDigestValues.Early,
        < EntropyClaimAnalyzer.LeadWindowShortestMs => AeonaDigestValues.Late,
        _ => AeonaDigestValues.InWindow,
    };

    private static IEnumerable<DigestPattern> EntropyClaimPatterns(List<EntropyClaimPull> pulls)
    {
        var casts = pulls.SelectMany(pull => pull.Casts.Select(cast => (pull.Kind, Cast: cast))).ToList();
        yield return Pattern("lapses with a charge ready", pulls.Select(pull => (pull.Kind, pull.LapsedWithChargeReady)));
        yield return Pattern("early casts", casts.Select(entry => (entry.Kind, entry.Cast.Timing == Timing(EntropyClaimAnalyzer.LeadWindowLongestMs + 1))));
        yield return Pattern("late casts", casts.Select(entry => (entry.Kind, entry.Cast.Timing == Timing(EntropyClaimAnalyzer.LeadWindowShortestMs - 1))));
        yield return Pattern("casts with none active", casts.Select(entry => (entry.Kind, entry.Cast.Timing == Timing(null))));
    }

    private static IEnumerable<DigestPattern> OblivionPatterns(List<OblivionPull> pulls)
    {
        yield return Pattern("Oblivion cast while a cleanse was due", pulls.Select(pull => (pull.Kind, pull.CastWhileCleanseDue != DigestValues.Never)));
    }

    private static IEnumerable<DigestPattern> StaggerCleansePatterns(List<StaggerCleansePull> pulls)
    {
        var casts = pulls.SelectMany(pull => pull.Casts.Select(cast => (pull.Kind, Cast: cast))).ToList();
        yield return Pattern("low-Stagger cleanses on a target holding less than a full cast",
            casts.Select(entry => (entry.Kind, entry.Cast is { TankUnderCleanseLine: true, TargetHeldLessThanOneCast: true })));
        yield return Pattern("low-Stagger cleanses that healed less than an Oblivion",
            casts.Select(entry => (entry.Kind, entry.Cast is { TankUnderCleanseLine: true, HealedLessThanOblivion: true })));
        if (casts.Any(entry => entry.Cast.AppliedEchoes is not null))
        {
            yield return Pattern("refreshed running Echoes of Divinity", casts.Select(entry => (entry.Kind, entry.Cast.RefreshedRunningEchoes == true)));
            yield return Pattern("Uchronia spent on Amend Fate",
                casts.Where(entry => entry.Cast.Free == Spells.Uchronia.Name).Select(entry => (entry.Kind, entry.Cast.Spell == Spells.AmendFate.Name)));
        }
    }

    private static IEnumerable<DigestPattern> ContinuumShiftPatterns(List<ContinuumShiftPull> pulls)
    {
        var shifts = pulls.SelectMany(pull => pull.Shifts.Select(shift => (pull.Kind, Shift: shift))).ToList();
        yield return Pattern("ended unspent", shifts.Select(entry => (entry.Kind, entry.Shift.SpentOn == AeonaDigestValues.Nothing)));
        yield return Pattern("spent on Entropy's Claim", shifts.Select(entry => (entry.Kind, entry.Shift.SpentOn == Spells.EntropyClaim.Name)));
        yield return Pattern("spent on Echoes of Ruin", shifts.Select(entry => (entry.Kind, entry.Shift.SpentOn == Spells.EchoesOfRuin.Name)));
        yield return Pattern("Echoes of Ruin with the tank out of danger",
            shifts.Select(entry => (entry.Kind, entry.Shift is { TankInDanger: false, FleetingHourFollowed: not true })));
        yield return Pattern("Chrona overcapped",
            shifts.Where(entry => entry.Shift.ChronaOvercapped is not null).Select(entry => (entry.Kind, entry.Shift.ChronaOvercapped == true)));
    }

    private static IEnumerable<DigestPattern> KindRewindPatterns(List<KindRewindPull> pulls)
    {
        yield return Pattern("Time Shards without Echoes of Ruin", pulls.Select(pull => (pull.Kind, pull.TimeShardsWithoutEchoesOfRuin)));
    }

    private static InsightMessage<TPull>? Message<TPull>(string guide, List<QualitativePerformance?> grades, DigestBuild build, List<TPull> pulls, Func<List<TPull>, IEnumerable<DigestPattern>> patterns) =>
        pulls.Count == 0 ? null : new InsightMessage<TPull>(guide, Focus(grades), build, [.. patterns(pulls)], pulls);

    private static void Add<TPull>(Dictionary<string, byte[]> messages, InsightMessage<TPull>? message, JsonTypeInfo<InsightMessage<TPull>> type)
    {
        if (message is not null)
            messages[message.Guide] = JsonSerializer.SerializeToUtf8Bytes(message, type);
    }
}
