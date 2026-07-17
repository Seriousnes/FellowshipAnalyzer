using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Serialization;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

using Microsoft.Extensions.DependencyInjection;

using Xunit;
using Xunit.Abstractions;

using CoreAbilities = FellowshipAnalyzer.Core.Analysis.Abilities;

namespace FellowshipAnalyzer.Heroes.Ardeos.Tests.Modules;

public sealed class RollingFlamesDiagnostic(ITestOutputHelper output)
{
    private const int PlayerId = 25;
    private const string ReportPath = @"g:\source\FellowshipAnalyzer\raw-reports\RaMDvgzWXBCnF4QT-f16-s25.json";

    [Fact]
    public async Task Dump()
    {
        var json = File.ReadAllText(ReportPath);
        using var doc = JsonDocument.Parse(json);
        var eventsEl = doc.RootElement.GetProperty("events");

        var jsonOptions = new JsonSerializerOptions
        {
            AllowOutOfOrderMetadataProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
        jsonOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
        var jsonContext = new FellowshipAnalyzerJsonContext(jsonOptions);
        var events = JsonSerializer.Deserialize(eventsEl, jsonContext.ListEvent)!;

        int efId = Spells.EngulfingFlames.FSLID;
        int iwId = Spells.InfernalWave.FSLID;
        int sbDotId = Spells.SearingBlazeDot.FSLID;

        int rawEfCasts = events.OfType<CastEvent>().Count(e => e.SourceId == PlayerId && e.Ability?.Id == efId);
        int rawIwCasts = events.OfType<CastEvent>().Count(e => e.SourceId == PlayerId && e.Ability?.Id == iwId);
        int rawSbDot = events.OfType<DamageEvent>().Count(e => e.SourceId == PlayerId && e.Ability?.Id == sbDotId);
        int rawIwCastsAnySource = events.OfType<CastEvent>().Count(e => e.Ability?.Id == iwId);
        int rawSbDotAnySource = events.OfType<DamageEvent>().Count(e => e.Ability?.Id == sbDotId);

        output.WriteLine($"EF FSLID={efId}  IW FSLID={iwId}  SearingBlazeDot FSLID={sbDotId}");
        output.WriteLine($"RAW(player)  EF casts={rawEfCasts}  IW casts={rawIwCasts}  SearingBlazeDot ticks={rawSbDot}");
        output.WriteLine($"RAW(any src) IW casts={rawIwCastsAnySource}  SearingBlazeDot ticks={rawSbDotAnySource}");
        output.WriteLine($"EF curated: cooldown={Spells.EngulfingFlames.Cooldown}s charges={Spells.EngulfingFlames.Charges}");

        var beginCh = events.OfType<BeginChannelEvent>().Where(e => e.SourceId == PlayerId)
            .GroupBy(e => $"{e.Ability?.Name}#{e.Ability?.Id}").Select(g => $"{g.Key}={g.Count()}");
        var endCh = events.OfType<EndChannelEvent>().Where(e => e.SourceId == PlayerId)
            .GroupBy(e => $"{e.Ability?.Name}#{e.Ability?.Id}").Select(g => $"{g.Key}={g.Count()}");
        output.WriteLine($"RAW BeginChannel (player): {string.Join(", ", beginCh)}");
        output.WriteLine($"RAW EndChannel   (player): {string.Join(", ", endCh)}");

        var rawBegins = events.OfType<BeginChannelEvent>().Where(e => e.SourceId == PlayerId).ToList();
        var rawEnds = events.OfType<EndChannelEvent>().Where(e => e.SourceId == PlayerId).ToList();
        var playerCastTs = events.OfType<CastEvent>().Where(e => e.SourceId == PlayerId).Select(e => e.Timestamp).OrderBy(t => t).ToList();
        output.WriteLine($"RAW begin timestamps: {string.Join(", ", rawBegins.Select(e => e.Timestamp))}");
        output.WriteLine($"RAW end (ts/start/dur): {string.Join(" | ", rawEnds.Select(e => $"{e.Timestamp}/{e.Start}/{e.Duration}"))}");

        var typeCounts = events.Where(e => (e as IHasSourceEvent)?.SourceId == PlayerId)
            .GroupBy(e => e.GetType().Name).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}={g.Count()}");
        output.WriteLine($"Player event types: {string.Join(", ", typeCounts)}");
        output.WriteLine($"BeginChannel IsCancelled count (player): {rawBegins.Count(b => b.IsCancelled)}");

        output.WriteLine("Per begin -> earliest terminating player event (End/Interrupt/BeginCast/Cast) after begin:");
        var interruptTs = events.OfType<InterruptEvent>().Select(e => e.Timestamp).OrderBy(t => t).ToList();
        var endTs = rawEnds.Select(e => e.Timestamp).OrderBy(t => t).ToList();
        var beginCastTs = events.OfType<BeginCastEvent>().Where(e => e.SourceId == PlayerId).Select(e => e.Timestamp).OrderBy(t => t).ToList();
        foreach (var begin in rawBegins)
        {
            var t0 = begin.Timestamp;
            var nEnd = endTs.FirstOrDefault(t => t > t0, -1);
            var nInt = interruptTs.FirstOrDefault(t => t > t0, -1);
            var nBC = beginCastTs.FirstOrDefault(t => t > t0, -1);
            var nCast = playerCastTs.FirstOrDefault(t => t > t0, -1);
            string D(int t) => t < 0 ? "-" : $"{t - t0}ms";
            output.WriteLine($"  begin={t0} cancelled={begin.IsCancelled} end={D(nEnd)} interrupt={D(nInt)} beginCast={D(nBC)} cast={D(nCast)}");
        }

        var buffApplies = events.OfType<ApplyBuffEvent>()
            .Where(e => (e.Ability?.Name ?? "").Contains("chrono", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => $"{e.Ability?.Name}#{e.Ability?.Id}->tgt{e.TargetId}").Select(g => $"{g.Key}={g.Count()}");
        var buffRemoves = events.OfType<RemoveBuffEvent>()
            .Where(e => (e.Ability?.Name ?? "").Contains("chrono", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => $"{e.Ability?.Name}#{e.Ability?.Id}->tgt{e.TargetId}").Select(g => $"{g.Key}={g.Count()}");
        output.WriteLine($"RAW ApplyBuff chrono*: {string.Join(", ", buffApplies)}");
        output.WriteLine($"RAW RemoveBuff chrono*: {string.Join(", ", buffRemoves)}");

        var castsByName = events.OfType<CastEvent>().Where(e => e.SourceId == PlayerId && (e.Ability?.Name ?? "").Contains("chrono", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => $"{e.Ability?.Name}#{e.Ability?.Id}").Select(g => $"{g.Key}={g.Count()}");
        output.WriteLine($"RAW Cast chrono* (player): {string.Join(", ", castsByName)}");

        var start = events.Count > 0 ? events.Min(e => e.Timestamp) : 0;
        var end = events.Count > 0 ? events.Max(e => e.Timestamp) : 0;
        output.WriteLine($"Fight length = {(end - start) / 1000.0:0.0}s  ({events.Count} events)");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddArdeosAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ArdeosCombatLogParser>();
        var fight = new ReportFight(0, "", 16, null, start, end, null, null, null);
        await parser.Analyze(events, PlayerId, fight);

        var rf = parser.GetModule<RollingFlamesAnalyzer>();
        output.WriteLine("");
        if (rf is null)
        {
            output.WriteLine("RollingFlamesAnalyzer is NULL (talent not active?)");
        }
        else
        {
            foreach (var cdr in rf.CooldownReductions)
            {
                output.WriteLine($"{cdr.Spell.Name,-16} generated={cdr.GeneratedMs / 1000.0,8:0.0}s  effective={cdr.EffectiveMs / 1000.0,8:0.0}s  wasted={cdr.WastedMs / 1000.0,8:0.0}s  eff%={cdr.Efficiency * 100:0}");
            }
        }

        var su = parser.GetModule<SpellUsable>()!;
        int efCastsSeen = su.Casts.Count(c => c.Id == efId);
        int iwCastsSeen = su.Casts.Count(c => c.Id == iwId);
        output.WriteLine("");
        output.WriteLine($"SpellUsable.Casts:  EF={efCastsSeen}  IW={iwCastsSeen}  total={su.Casts.Count}");
        output.WriteLine($"EffectiveRate(EF) at end = {su.EffectiveRate(efId):0.###}");
        output.WriteLine($"EF on cooldown at end? {su.IsOnCooldown(efId)}  chargesAvail={su.ChargesAvailable(efId)}");

        var abilities = parser.GetModule<CoreAbilities>()!;
        output.WriteLine($"Abilities.GetExpectedCooldown(EF)={abilities.GetExpectedCooldown(efId)}s  maxCharges={abilities.GetMaxCharges(efId)}");

        var chrono = parser.GetModule<ChronoshiftAnalyzer>();
        if (chrono is null)
        {
            output.WriteLine("ChronoshiftAnalyzer is NULL");
        }
        else
        {
            int completed = chrono.Windows.Count(w => w.EndTimestamp > 0);
            output.WriteLine($"Chronoshift windows opened={chrono.Windows.Count}  completed(end seen)={completed}  unbalanced begins={chrono.Windows.Count - completed}");
        }
    }
}
