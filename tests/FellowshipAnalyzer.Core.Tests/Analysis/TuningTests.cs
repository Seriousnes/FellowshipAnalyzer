using FellowshipAnalyzer.Core.Game;

using Xunit;

using ArdeosSpellTuning = FellowshipAnalyzer.Core.Common.Spells.Ardeos.SpellTuning;
using GundeTalentTuning = FellowshipAnalyzer.Core.Common.Spells.Gunde.TalentTuning;
using ItemsSpellTuning = FellowshipAnalyzer.Core.Common.Items.SpellTuning;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

/// <summary>
/// The tuning classes are generated from <c>data/spelldb.json</c>, so a spell, talent, legendary or glyph
/// reads each numeric tuning row the export states as a constant.
/// </summary>
public sealed class TuningTests
{
    [Fact]
    public void ItemsSpellTuning_GivesChronoshiftEightCooldownRecoveryWhileActive() =>
        Assert.Equal(8, ItemsSpellTuning.Chronoshift.Channel_WhileActiveAddedCooldownRecovery);

    [Fact]
    public void ArdeosSpellTuning_GivesFireBallAnEightSecondApocalypseCooldownReduction() =>
        Assert.Equal(8, ArdeosSpellTuning.FireBall.CooldownReductionToApocalypse_ReductionInSeconds);

    [Fact]
    public void GundeTalentTuning_GivesCarnageOneSecondOfCooldownReductionPerHit() =>
        Assert.Equal(1, GundeTalentTuning.Carnage.Gunde_TargetedAoeProjectile_Talent_DamageCooldownReduction_CooldownReductionInSecondsPerHit);

    [Fact]
    public void GlyphTuning_GivesTheMonarchFourRanksOfCooldownAcceleration() =>
        Assert.Equal([0.03, 0.05, 0.08, 0.12], GlyphTuning.TheMonarch.CooldownAcceleration.ToArray());
}
