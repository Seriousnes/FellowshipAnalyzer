using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Events;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests that each new charge recharge of <see cref="SpellUsable"/> takes its duration from the state at its
/// start, and that the recharge already running keeps its own.
/// </summary>
public sealed partial class SpellUsableTests
{
    /// <summary>
    /// SpellG has two charges and Duration-scaled Haste. Both charges are spent at t=1000 and t=1100 with no
    /// Haste, so the first recharge runs 10 s to t=11000. Haste rises during it and leaves that recharge at
    /// 10 s. The second recharge starts at t=11000 and runs <c>10000 / (1 + haste)</c>.
    /// </summary>
    [Fact]
    public async Task DurationHaste_SecondRecharge_TakesTheHasteAtItsStart()
    {
        var (parser, _, probe) = await Run(
            [CreateCast(1000, SpellG), CreateCast(1100, SpellG), CreateHasteBuff(3000), CreateTrigger(30_000)]);

        var haste = parser.GetModule<Haste>()!.Current;
        Assert.True(haste >= BuffHaste, $"expected the haste buff to apply, got {haste}");

        var secondRecharge = (int)(CdSecondsG * 1000 / (1 + haste));

        var restore = LastUpdate(probe, SpellG, UpdateSpellUsableType.RestoreCharge);
        Assert.Equal(11_000, restore.Timestamp);
        Assert.Equal(11_000 + secondRecharge, restore.ExpectedRechargeTimestamp);
        Assert.Equal(secondRecharge, restore.ExpectedRechargeDuration);

        Assert.Equal(11_000 + secondRecharge, LastUpdate(probe, SpellG, UpdateSpellUsableType.EndCooldown).Timestamp);
    }

    /// <summary>
    /// A charge restored early by <see cref="SpellUsable.EndCooldown"/> starts the next recharge at the Haste
    /// current then: with both charges of SpellG spent and Haste raised at t=3000, an end at t=5000 starts
    /// a recharge of <c>10000 / (1 + haste)</c>.
    /// </summary>
    [Fact]
    public async Task DurationHaste_EndCooldownPartialRestore_TakesTheCurrentHaste()
    {
        var remaining = -1;
        var haste = 0.0;

        await Run(
            [CreateCast(1000, SpellG), CreateCast(1100, SpellG), CreateHasteBuff(3000), CreateTrigger(5000)],
            onApplyBuff: (owner, e) =>
            {
                if (e.Ability?.FSLID.Value != TriggerId) return;
                var spellUsable = owner.GetModule<SpellUsable>()!;
                haste = owner.GetModule<Haste>()!.Current;
                spellUsable.EndCooldown(SpellG, e.Timestamp);
                remaining = spellUsable.CooldownRemaining(SpellG, e.Timestamp);
            });

        Assert.Equal((int)(CdSecondsG * 1000 / (1 + haste)), remaining);
    }
}
