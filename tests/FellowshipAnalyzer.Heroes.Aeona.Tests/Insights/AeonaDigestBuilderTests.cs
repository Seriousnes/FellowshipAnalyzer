using System.Text;
using System.Text.RegularExpressions;

using FellowshipAnalyzer.Api.Core.Insights;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Insights;
using FellowshipAnalyzer.Core.Insights.Aeona;
using FellowshipAnalyzer.Heroes.Aeona.Insights;

using Shouldly;

using Xunit;

using static FellowshipAnalyzer.Heroes.Aeona.Tests.AeonaLog;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Insights;

public sealed class AeonaDigestBuilderTests
{
    private static readonly int[] Shift = [AeonaTalents.ContinuumShift];

    [Fact]
    public async Task ContinuumShiftOnTimeShard_IsAShiftSpentOnTimeShardWithAStrengthsFocus()
    {
        var parser = await Analyze(TrashPull(),
            Info(Shift),
            ApplyBuff(1_000, Spells.ContinuumShift),
            Activation(1_500, Spells.TimeShard),
            Completion(3_000, Spells.TimeShard),
            Damage(3_100, Spells.TimeShardDamage, 10_000),
            Heal(3_100, Spells.TimeShard, TankId, 4_000, 1_000),
            RemoveBuff(3_100, Spells.ContinuumShift));

        var message = AeonaDigestBuilder.ContinuumShift(parser, Digest.Build(parser, [], []))!;

        message.Focus.ShouldBe(InsightFocus.Strengths);
        var pull = message.Pulls.ShouldHaveSingleItem();
        pull.Kind.ShouldBe("trash pack");
        var shift = pull.Shifts.ShouldHaveSingleItem();
        shift.SpentOn.ShouldBe(Spells.TimeShard.Name);
        shift.ChronaOvercapped.ShouldBe(false);
        shift.TankInDanger.ShouldBeNull();
    }

    [Fact]
    public async Task ContinuumShiftOnEchoesOfRuinWithTheTankInDanger_MarksTheDangerWithAMixedFocus()
    {
        var parser = await Analyze(TrashPull(),
            Info(Shift),
            ApplyBuff(1_000, Spells.ContinuumShift),
            TankStagger(2_900, staggerHitPoints: 1_000, hitPoints: 15_000),
            Completion(3_000, Spells.EchoesOfRuin),
            ApplyDebuff(3_050, Spells.EchoesOfRuinDot, EnemyId),
            ApplyDebuff(3_050, Spells.EchoesOfRuinDot, SecondEnemyId),
            RemoveBuff(3_050, Spells.ContinuumShift));

        var message = AeonaDigestBuilder.ContinuumShift(parser, Digest.Build(parser, [], []))!;

        message.Focus.ShouldBe(InsightFocus.Mixed);
        var shift = message.Pulls.ShouldHaveSingleItem().Shifts.ShouldHaveSingleItem();
        shift.SpentOn.ShouldBe(Spells.EchoesOfRuin.Name);
        shift.TankInDanger.ShouldBe(true);
        shift.ChronaOvercapped.ShouldBeNull();
    }

    [Theory]
    [InlineData(null, "none active")]
    [InlineData(5_000, "early")]
    [InlineData(3_000, "in window")]
    [InlineData(1_000, "late")]
    public void Timing_PlacesTheLeadAgainstTheWindow(int? leadMs, string timing) =>
        AeonaDigestBuilder.Timing(leadMs).ShouldBe(timing);

    [Fact]
    public async Task Messages_HoldNoNumberOrGrade()
    {
        var parser = await Analyze(TrashPull(),
            Info(Shift),
            ApplyBuff(1_000, Spells.ContinuumShift),
            Completion(3_000, Spells.TimeShard),
            RemoveBuff(3_100, Spells.ContinuumShift));

        var messages = AeonaDigestBuilder.Messages(parser);

        messages.ShouldNotBeEmpty();
        foreach (var json in messages.Values.Select(Encoding.UTF8.GetString))
        {
            Regex.IsMatch(Regex.Unescape(json), @"\d").ShouldBeFalse(json);
            foreach (var grade in new[] { "Perfect", "Good", "\"Ok\"", "Bad", "grade" })
                json.ShouldNotContain(grade, Case.Insensitive);
        }
    }

    [Fact]
    public async Task Messages_PassTheServerCheckUnchanged()
    {
        var parser = await Analyze(TrashPull(),
            Info(Shift),
            ApplyBuff(1_000, Spells.ContinuumShift),
            Completion(3_000, Spells.TimeShard),
            RemoveBuff(3_100, Spells.ContinuumShift));

        foreach (var (guide, message) in AeonaDigestBuilder.Messages(parser))
        {
            var (accepted, failure) = InsightRequestValidation.Check(AeonaGuides.Hero, guide, message);

            failure.ShouldBeNull();
            accepted!.User.ShouldBe(Encoding.UTF8.GetString(message));
        }
    }
}
