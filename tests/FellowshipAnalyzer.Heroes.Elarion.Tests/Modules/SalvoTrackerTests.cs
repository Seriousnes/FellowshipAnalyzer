using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Heroes.Elarion.Modules;

using Shouldly;

using Xunit;

using Marks = FellowshipAnalyzer.Heroes.Elarion.Tests.Modules.LunarlightMarkAnalyzerTests;

namespace FellowshipAnalyzer.Heroes.Elarion.Tests.Modules;

public sealed class SalvoTrackerTests
{
    [Fact]
    public async Task SalvoAndErupt_AreTotalledAsAShareOfThePlayersDamage()
    {
        var (parser, _) = await Marks.AnalyzeAsync(
            Marks.Damage(1_000, Marks.TargetA, Spells.LunarlightMarkDamage.FSLID, amount: 900),
            Marks.Damage(1_100, Marks.TargetA, Spells.LunarlightMarkDamage.FSLID, amount: 600),
            Marks.Damage(1_200, Marks.TargetB, Spells.LunarlightMarkAoeDamage.FSLID, amount: 500),
            Marks.Damage(1_300, Marks.TargetA, Spells.CelestialShotDamage.FSLID, amount: 8_000));

        var tracker = parser.SalvoTracker.ShouldNotBeNull();
        tracker.SalvoHits.ShouldBe(2);
        tracker.EruptHits.ShouldBe(1);
        tracker.TotalHits.ShouldBe(3);
        tracker.SalvoDamage.ShouldBe(1_500);
        tracker.EruptDamage.ShouldBe(500);
        tracker.TotalMarkDamage.ShouldBe(2_000);
        tracker.TotalPlayerDamage.ShouldBe(10_000);
        tracker.DamageShare.ShouldBe(0.2d);
    }

    [Fact]
    public async Task SalvoHits_MakeTheStatisticsCardAvailable()
    {
        var (parser, _) = await Marks.AnalyzeAsync(
            Marks.Damage(1_000, Marks.TargetA, Spells.LunarlightMarkDamage.FSLID, amount: 900));

        var tracker = parser.SalvoTracker.ShouldNotBeNull();
        tracker.Statistic.ShouldNotBeNull();
    }

    [Fact]
    public async Task WithoutSalvoHits_TheStatisticsCardIsWithheld()
    {
        var (parser, _) = await Marks.AnalyzeAsync(
            Marks.Damage(1_000, Marks.TargetA, Spells.CelestialShotDamage.FSLID, amount: 8_000));

        var tracker = parser.SalvoTracker.ShouldNotBeNull();
        tracker.SalvoHits.ShouldBe(0);
        tracker.EruptHits.ShouldBe(0);
        tracker.TotalPlayerDamage.ShouldBe(8_000);
        tracker.DamageShare.ShouldBe(0d);
        tracker.Statistic.ShouldBeNull();
    }
}
