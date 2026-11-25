using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Events;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests that every recharge of a chain of <see cref="SpellUsable"/> charges has the length set by the spend
/// from full charges.
/// </summary>
public sealed partial class SpellUsableTests
{
    /// <summary>
    /// SpellG has two charges and Duration-scaled Haste. Both charges are spent at t=1000 and t=1100 with no
    /// Haste, so the first recharge runs 10 s to t=11000. Haste rises during it and leaves that recharge at
    /// 10 s. The second recharge starts at t=11000 and runs the same 10 s, because the length of the chain
    /// was set when the first charge was spent.
    /// </summary>
    [Fact]
    public async Task DurationHaste_SecondRecharge_KeepsTheLengthSetAtTheFirstSpend()
    {
        var (parser, _, probe) = await Run(
            [CreateCast(1000, SpellG), CreateCast(1100, SpellG), CreateHasteBuff(3000), CreateTrigger(30_000)]);

        Assert.True(parser.GetModule<Haste>()!.Current >= BuffHaste, "expected the haste buff to apply");

        var restore = LastUpdate(probe, SpellG, UpdateSpellUsableType.RestoreCharge);
        Assert.Equal(11_000, restore.Timestamp);
        Assert.Equal(21_000, restore.ExpectedRechargeTimestamp);
        Assert.Equal(10_000, restore.ExpectedRechargeDuration);

        Assert.Equal(21_000, LastUpdate(probe, SpellG, UpdateSpellUsableType.EndCooldown).Timestamp);
    }

    /// <summary>
    /// A charge restored early by <see cref="SpellUsable.EndCooldown"/> starts the next recharge at the length
    /// of the chain: with both charges of SpellG spent before Haste rises at t=3000, an end at t=5000 starts
    /// a recharge of 10 s.
    /// </summary>
    [Fact]
    public async Task DurationHaste_EndCooldownPartialRestore_KeepsTheLengthOfTheChain()
    {
        var remaining = -1;

        await Run(
            [CreateCast(1000, SpellG), CreateCast(1100, SpellG), CreateHasteBuff(3000), CreateTrigger(5000)],
            onApplyBuff: (owner, e) =>
            {
                if (e.Ability?.FSLID.Value != TriggerId) return;
                var spellUsable = owner.GetModule<SpellUsable>()!;
                spellUsable.EndCooldown(SpellG, e.Timestamp);
                remaining = spellUsable.CooldownRemaining(SpellG, e.Timestamp);
            });

        Assert.Equal(CdSecondsG * 1000, remaining);
    }
}
