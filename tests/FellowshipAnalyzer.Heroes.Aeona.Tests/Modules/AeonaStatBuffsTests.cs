using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;

using Shouldly;

using Xunit;

using static FellowshipAnalyzer.Heroes.Aeona.Tests.AeonaLog;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Modules;

public sealed class AeonaStatBuffsTests
{
    [Fact]
    public async Task FleetingHour_MultipliesCooldownRecoveryByOneAndAHalf_UntilItIsRemoved()
    {
        var active = await Analyze(BossPull(),
            Info([]),
            ApplyBuff(1_000, Spells.FleetingHourSelfBuff));

        active.GetModule<StatTracker>()!.CurrentCooldownRecovery.ShouldBe(1.5, 0.0001);

        var removed = await Analyze(BossPull(),
            Info([]),
            ApplyBuff(1_000, Spells.FleetingHourSelfBuff),
            RemoveBuff(2_000, Spells.FleetingHourSelfBuff));

        removed.GetModule<StatTracker>()!.CurrentCooldownRecovery.ShouldBe(1.0, 0.0001);
    }
}
