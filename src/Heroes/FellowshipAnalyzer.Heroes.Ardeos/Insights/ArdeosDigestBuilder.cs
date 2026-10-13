using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Insights;
using FellowshipAnalyzer.Core.Insights.Ardeos;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Guides;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

using static FellowshipAnalyzer.Core.Insights.Digest;

using ArdeosLegendaryItems = FellowshipAnalyzer.Core.Common.Spells.Ardeos.Legendaries;
using ArdeosTalentTree = FellowshipAnalyzer.Core.Common.Spells.Ardeos.Talents;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Ardeos.Spells;

namespace FellowshipAnalyzer.Heroes.Ardeos.Insights;

/// <summary>
/// Builds the insight message of each Ardeos guide from a parser after <see cref="CombatLogParser.Analyze"/>.
/// </summary>
public static class ArdeosDigestBuilder
{
    /// <summary>Guide insights' view of <paramref name="parser"/>.</summary>
    public static HeroInsights Insights(ArdeosCombatLogParser parser) => new(
        new Dictionary<string, Type>
        {
            [ArdeosGuides.WildfireCombo] = typeof(WildfireComboGuide),
            [ArdeosGuides.EngulfingFlamesEconomy] = typeof(EngulfingFlamesEconomyGuide),
            [ArdeosGuides.DetonateEfficiency] = typeof(DetonateEfficiencyGuide),
            [ArdeosGuides.SearingBlaze] = typeof(SearingBlazeGuide),
        },
        [.. Spells.Kit, .. ArdeosTalentTree.All],
        () => Messages(parser));

    /// <summary>UTF-8 JSON message of each guide with a row, keyed by guide name.</summary>
    public static Dictionary<string, byte[]> Messages(ArdeosCombatLogParser parser)
    {
        var build = Digest.Build(parser, ArdeosTalentTree.All, ArdeosLegendaryItems.All);
        var context = ArdeosDigestJsonContext.Default;
        var messages = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        Add(messages, Wildfire(parser, build), context.InsightMessageWildfirePull);
        Add(messages, EngulfingFlames(parser, build), context.InsightMessageEngulfingFlamesPull);
        Add(messages, Detonate(parser, build), context.InsightMessageDetonatePull);
        Add(messages, SearingBlaze(parser, build), context.InsightMessageSearingBlazePull);
        return messages;
    }

    /// <summary>Wildfire windows of every pull, with the guide's focus.</summary>
    public static InsightMessage<WildfirePull>? Wildfire(ArdeosCombatLogParser parser, DigestBuild build)
    {
        var grades = new List<QualitativePerformance?>();
        var pulls = parser.WildfireComboAnalyzers
            .Where(entry => entry.Analyzer.Windows.Count > 0)
            .Select(entry => new WildfirePull(Kind(entry.Pull), [.. entry.Analyzer.Windows.Select(window =>
            {
                grades.Add(WildfireComboGuide.WindowPerformance(window));
                var casts = window.CastsInWindow.Where(cast => cast.Ability.Id != Spells.Wildfire.FSLID).ToList();
                return new WildfireWindowFacts(
                    [.. window.Coverage.Where(dot => !dot.Active).Select(dot => DotName(dot.Dot))],
                    window.DistinctDots >= WildfireComboAnalyzer.DistinctDotSuccessThreshold,
                    window.EngulfingInstances >= WildfireComboAnalyzer.EngulfingInstanceThreshold,
                    window.DetonateSpamFollowed,
                    casts.Any(cast => cast.Timestamp >= window.StartTimestamp && window.Coverage.Any(dot => dot.Dot.Cast?.FSLID == cast.Ability.Id)),
                    window.HasPyromania,
                    window.HasIncinerate,
                    [.. casts.Where(cast => cast.Timestamp < window.StartTimestamp).Select(cast => SpellName(cast.Ability.Id))],
                    [.. casts.Where(cast => cast.Timestamp >= window.StartTimestamp).Select(cast => SpellName(cast.Ability.Id))]);
            })]))
            .ToList();
        return Message(ArdeosGuides.WildfireCombo, grades, build, pulls, WildfirePatterns);
    }

    /// <summary>Engulfing Flames charges and setups of every pull, with the guide's focus.</summary>
    public static InsightMessage<EngulfingFlamesPull>? EngulfingFlames(ArdeosCombatLogParser parser, DigestBuild build)
    {
        var grades = new List<QualitativePerformance?>();
        var pulls = parser.EngulfingFlamesEconomyAnalyzers
            .Where(entry => entry.Analyzer.Windows.Count > 0)
            .Select(entry => new EngulfingFlamesPull(
                Kind(entry.Pull),
                entry.Analyzer.WastedCharges > 0,
                [.. entry.Analyzer.Windows.Select(window =>
                {
                    var grade = EngulfingFlamesEconomyGuide.WindowPerformance(window);
                    grades.Add(grade);
                    return new EngulfingFlamesSetupFacts(Level(grade), window.ChargesAtReady >= Spells.EngulfingFlames.Charges);
                })]))
            .ToList();
        return Message(ArdeosGuides.EngulfingFlamesEconomy, grades, build, pulls, EngulfingFlamesPatterns);
    }

    /// <summary>Detonate casts of every pull, with the guide's focus.</summary>
    public static InsightMessage<DetonatePull>? Detonate(ArdeosCombatLogParser parser, DigestBuild build)
    {
        var surge = parser.SelectedCombatant.HasTalent(ArdeosTalents.ApocalypticSurge);
        var grades = new List<QualitativePerformance?>();
        var pulls = parser.DetonateEfficiencyAnalyzers
            .Where(entry => entry.Analyzer.Casts.Count > 0)
            .Select(entry => new DetonatePull(
                Kind(entry.Pull),
                surge ? entry.Analyzer.SurgeStacksExpired > 0 : null,
                [.. entry.Analyzer.Casts.Select(cast =>
                {
                    var grade = DetonateEfficiencyGuide.CastPerformance(cast);
                    grades.Add(grade);
                    return new DetonateCastFacts(
                        Level(grade),
                        [.. cast.Coverage.Where(dot => !dot.Active).Select(dot => DotName(dot.Dot))],
                        surge ? cast.Free : null);
                })]))
            .ToList();
        return Message(ArdeosGuides.DetonateEfficiency, grades, build, pulls, DetonatePatterns);
    }

    /// <summary>Searing Blaze uptime and spread of every pull, with the guide's focus.</summary>
    public static InsightMessage<SearingBlazePull>? SearingBlaze(ArdeosCombatLogParser parser, DigestBuild build)
    {
        var grades = new List<QualitativePerformance?>();
        var pulls = new List<SearingBlazePull>();
        foreach (var (pull, analyzer) in parser.SearingBlazeAnalyzers)
        {
            switch (analyzer)
            {
                case SearingBlazeUptimeAnalyzer uptime:
                    var uptimeGrade = SearingBlazeGuide.UptimeTier(SearingBlazeGuide.UptimePercent(uptime));
                    grades.Add(uptimeGrade);
                    pulls.Add(new SearingBlazePull(Kind(pull), uptime.Windows.Count > 0, Level(uptimeGrade), uptime.GapCount > 0, null));
                    break;
                case SearingBlazeSpreadAnalyzer spread:
                    var spreadGrade = SearingBlazeGuide.PercentTier(SearingBlazeGuide.CoverageScore(spread));
                    grades.Add(spreadGrade);
                    pulls.Add(new SearingBlazePull(Kind(pull), spread.DistinctTargets > 0, null, null, Level(spreadGrade)));
                    break;
            }
        }
        return Message(ArdeosGuides.SearingBlaze, grades, build, pulls, SearingBlazePatterns);
    }

    private static IEnumerable<DigestPattern> WildfirePatterns(List<WildfirePull> pulls)
    {
        var windows = pulls.SelectMany(pull => pull.Windows.Select(window => (pull.Kind, Window: window))).ToList();
        foreach (var dot in windows.SelectMany(entry => entry.Window.DotsMissing).Distinct(StringComparer.Ordinal))
            yield return Pattern($"{dot} missing", windows.Select(entry => (entry.Kind, entry.Window.DotsMissing.Contains(dot))));
        yield return Pattern("short of enough DoTs", windows.Select(entry => (entry.Kind, !entry.Window.EnoughDots)));
        yield return Pattern("short of both Engulfing Flames instances", windows.Select(entry => (entry.Kind, !entry.Window.TwoEngulfingFlames)));
        yield return Pattern("incomplete follow-up", windows.Select(entry => (entry.Kind, !entry.Window.FollowUpComplete)));
        yield return Pattern("DoT casts after Wildfire", windows.Select(entry => (entry.Kind, entry.Window.DotCastAfter)));
    }

    private static IEnumerable<DigestPattern> EngulfingFlamesPatterns(List<EngulfingFlamesPull> pulls)
    {
        yield return Pattern("wasted charges", pulls.Select(pull => (pull.Kind, pull.WastedCharges)));
        yield return Pattern("setup short of both charges", pulls.SelectMany(pull => pull.Setups.Select(setup => (pull.Kind, setup.Setup != DigestValues.Full))));
    }

    private static IEnumerable<DigestPattern> DetonatePatterns(List<DetonatePull> pulls)
    {
        var casts = pulls.SelectMany(pull => pull.Casts.Select(cast => (pull.Kind, Cast: cast))).ToList();
        yield return Pattern("short of a full stack", casts.Select(entry => (entry.Kind, entry.Cast.DotStack != DigestValues.Full)));
        foreach (var dot in casts.SelectMany(entry => entry.Cast.DotsMissing).Distinct(StringComparer.Ordinal))
            yield return Pattern($"{dot} missing", casts.Select(entry => (entry.Kind, entry.Cast.DotsMissing.Contains(dot))));
        if (pulls.Any(pull => pull.SurgeStacksExpired is not null))
            yield return Pattern("expired Apocalyptic Surge stacks", pulls.Select(pull => (pull.Kind, pull.SurgeStacksExpired == true)));
    }

    private static IEnumerable<DigestPattern> SearingBlazePatterns(List<SearingBlazePull> pulls)
    {
        var bosses = pulls.Where(pull => pull.Uptime is not null).ToList();
        var packs = pulls.Where(pull => pull.Spread is not null).ToList();
        if (bosses.Count > 0)
        {
            yield return Pattern("uptime short of full", bosses.Select(pull => (pull.Kind, pull.Uptime != DigestValues.Full)));
            yield return Pattern("dropped off", bosses.Select(pull => (pull.Kind, pull.DroppedOff == true)));
        }
        if (packs.Count > 0)
            yield return Pattern("spread short of full", packs.Select(pull => (pull.Kind, pull.Spread != DigestValues.Full)));
    }

    private static InsightMessage<TPull>? Message<TPull>(string guide, List<QualitativePerformance?> grades, DigestBuild build, List<TPull> pulls, Func<List<TPull>, IEnumerable<DigestPattern>> patterns) =>
        pulls.Count == 0 ? null : new InsightMessage<TPull>(guide, Focus(grades), build, [.. patterns(pulls)], pulls);

    private static void Add<TPull>(Dictionary<string, byte[]> messages, InsightMessage<TPull>? message, JsonTypeInfo<InsightMessage<TPull>> type)
    {
        if (message is not null)
            messages[message.Guide] = JsonSerializer.SerializeToUtf8Bytes(message, type);
    }

    private static string DotName(Dot dot) => Named(dot.Cast?.Name ?? dot.Effect.Name);

    private static string SpellName(int id) => Named(SpellRegistry.MaybeGet(id)?.Name);

    private static string Named(string? name) => string.IsNullOrEmpty(name) ? DigestValues.UnknownSpell : name;
}
