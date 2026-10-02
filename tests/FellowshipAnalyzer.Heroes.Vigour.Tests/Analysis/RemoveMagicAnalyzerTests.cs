using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Vigour.Analysis;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Vigour.Spells;

using static FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis.VigourAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis;

public sealed class RemoveMagicAnalyzerTests
{
    [Fact]
    public async Task OnlyACastThatDispelsKeepsTheCooldownRunning()
    {
        var parser = await Analyze(
            Cast(PullStart, Spells.RemoveMagic, AllyId),
            Cast(PullStart + 1_000, Spells.RemoveMagic, AllyId),
            Dispel(PullStart + 1_000, Spells.RemoveMagic, AllyId),
            Cast(PullStart + 2_000, Spells.Dawnflare));

        var analyzer = parser.RemoveMagic.ShouldNotBeNull();

        analyzer.Casts.ShouldBe(2);
        analyzer.CastsWithDispel.ShouldBe(1);
        analyzer.CastsWithoutDispel.ShouldBe(1);
        analyzer.Dispels.ShouldBe(1);
        CooldownEnds(parser).ShouldBe([PullStart + 1_000, PullStart + 7_000]);
    }

    private static List<int> CooldownEnds(VigourCombatLogParser parser) =>
    [
        .. parser.Events
            .OfType<UpdateSpellUsableEvent>()
            .Where(e => e.Ability.Id == Spells.RemoveMagic.FSLID && e.UpdateType == UpdateSpellUsableType.EndCooldown)
            .Select(e => e.Timestamp)
    ];

    [Fact]
    public async Task AFailedCastIsClearedByTheNextCast()
    {
        var parser = await Analyze(
            Cast(PullStart, Spells.RemoveMagic, AllyId),
            Cast(PullStart + 1_000, Spells.Dawnflare));

        CooldownEnds(parser).ShouldBe([PullStart + 1_000]);
    }
}
