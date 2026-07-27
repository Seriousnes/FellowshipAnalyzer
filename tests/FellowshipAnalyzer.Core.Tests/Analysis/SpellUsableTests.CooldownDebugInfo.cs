using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed partial class SpellUsableTests
{
    private static IReadOnlyList<int> RestoreTimestamps(UpdateProbeModule probe) =>
        probe.Updates
            .Where(u => u.Ability.FSLID == SpellB && u.UpdateType == UpdateSpellUsableType.RestoreCharge)
            .Select(u => u.Timestamp)
            .ToList();

    private static IReadOnlyList<DebugAnnotation> NoChargeAnnotations(TestCombatLogParser parser) =>
        parser.GetModule<DebugAnnotations>()!
            .GetAll()
            .SelectMany(m => m.AnnotatedEvents)
            .SelectMany(a => a.Annotations)
            .Where(a => a.Summary.Contains("no charges available", StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static IEnumerable<Event> Fillers(int start, int end, int interval)
    {
        for (var ts = start; ts <= end; ts += interval)
            yield return new ApplyBuffEvent
            {
                Timestamp = ts,
                SourceId = PlayerId,
                TargetId = PlayerId,
                Ability = new Ability { FSLID = 9999, Name = "Filler" },
            };
    }

    [Fact]
    public async Task SustainableCadence_ProducesNoRestoresAndNoDebugInfo()
    {
        List<Event> events = [CreateCast(0, SpellB), CreateCast(25_000, SpellB), CreateCast(50_000, SpellB)];
        events.AddRange(Fillers(2_500, 60_000, 2_500));
        events.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        var (parser, _, probe) = await Run(events);

        Assert.Empty(RestoreTimestamps(probe));
        Assert.Empty(NoChargeAnnotations(parser));
    }

    [Fact]
    public async Task NoChargesCast_RecordsCooldownDebugInfo()
    {
        int[] castTimes = [0, 10_000, 15_000];
        var (parser, _, _) = await Run([.. castTimes.Select(t => (Event)CreateCast(t, SpellB))]);

        Assert.NotEmpty(NoChargeAnnotations(parser));
    }

    [Fact]
    public async Task NoChargesCast_ReconcilesAsMissedNaturalCdExpiration()
    {
        int[] castTimes = [0, 10_000, 15_000];
        var (_, _, probe) = await Run([.. castTimes.Select(t => (Event)CreateCast(t, SpellB))]);

        Assert.Contains(15_000, RestoreTimestamps(probe));
    }

    [Fact]
    public async Task WithinLagMargin_DoesNotRecordCooldownDebugInfo()
    {
        int[] castTimes = [0, 10_000, 19_900];
        var (parser, _, _) = await Run([.. castTimes.Select(t => (Event)CreateCast(t, SpellB))]);

        Assert.Empty(NoChargeAnnotations(parser));
    }
}
