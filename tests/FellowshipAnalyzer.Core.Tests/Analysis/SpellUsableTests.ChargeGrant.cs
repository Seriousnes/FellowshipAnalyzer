using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// Tests for <see cref="FellowshipAnalyzer.Core.Analysis.SpellUsable"/> reading the charges an
/// <see cref="FellowshipAnalyzer.Core.Analysis.Abilities"/> charge grant gives, from the first cast of the dungeon.
/// </summary>
public sealed partial class SpellUsableTests
{
    private const int GrantedChargesId = 401;
    private const int GrantingTalentId = 566;

    /// <summary>
    /// The player holds the talent granting two charges, and the first event after the combatant info is a
    /// cast. The first state of the ability already has both charges.
    /// </summary>
    [Fact]
    public async Task ChargeGrant_HeldFromTheFirstCast_GivesTheGrantedMaxCharges()
    {
        var (_, spellUsable, probe) = await Run(
        [
            new CombatantInfoEvent
            {
                Timestamp = 0,
                SourceId = PlayerId,
                Talents = [new TalentInfo { Id = FSLID.FromNative(SpellKind.Talent, GrantingTalentId).Value }],
            },
            CreateCast(0, GrantedChargesId),
        ]);

        var begin = LastUpdate(probe, GrantedChargesId, UpdateSpellUsableType.BeginCooldown);
        Assert.Equal(2, begin.MaxCharges);
        Assert.Equal(1, begin.ChargesAvailable);
        Assert.Equal(1, spellUsable.ChargesAvailable(GrantedChargesId));
        Assert.Equal(2, spellUsable.StateOf(GrantedChargesId)!.MaxCharges);
    }
}
