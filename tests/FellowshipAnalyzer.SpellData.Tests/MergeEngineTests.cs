using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Model;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class MergeEngineTests
{
    private static MergeResult Run() => MergeEngine.Run(MergeInputs.Load());

    [Fact]
    public void Rime_BurstingIce_IsSelectedAsAbility()
    {
        var s = Run().Spells.Single(x => x.Scope == "rime" && x.Member == "BurstingIce");
        s.Spell.Id.ShouldBe(1031);
        s.Kind.ShouldBe(SpellKind.Ability);
        s.Spell.Name.ShouldBe("Bursting Ice");
    }

    [Fact]
    public void Xavian_SolarShield_HasACooldownHasteShortens() =>
        Run().Spells.Single(x => x.Scope == "xavian" && x.Member == "SolarShield").Spell.CooldownHaste.ShouldBe(CooldownHasteScaling.Duration);

    [Fact]
    public void Meiko_StoneShield_TakesHasteIntoItsTimeRate() =>
        Run().Spells.Single(x => x.Scope == "meiko" && x.Member == "StoneShieldAlt").Spell.CooldownHaste.ShouldBe(CooldownHasteScaling.TimeRate);

    [Fact]
    public void Xavian_ShiningHalo_HasACooldownHasteLeavesAlone() =>
        Run().Spells.Single(x => x.Scope == "xavian" && x.Member == "ShiningHalo").Spell.CooldownHaste.ShouldBeNull();

    [Fact]
    public void Vigour_LuminousBarrier_TakesSacredBarriersChargesOntoATimeRateCooldown() =>
        Run().Spells.Single(x => x.Scope == "vigour" && x.Member == "LuminousBarrier").Spell.ChargeGrant.ShouldBe(new ChargeGrant
        {
            Source = GrantSource.Talent,
            Id = 566,
            Charges = 2,
            CooldownScaling = new() { TimeRateCooldownRecovery = true, TimeRateHaste = true },
        });

    [Fact]
    public void Rime_FrostBolt_HasNoChargeGrant() =>
        Run().Spells.Single(x => x.Scope == "rime" && x.Member == "FrostBolt").Spell.ChargeGrant.ShouldBeNull();

    [Fact]
    public void Rime_ColdSnap_ScalesItsTimeRateOffCooldownRecoveryAndHaste() =>
        SpellOf("rime", "ColdSnap").CooldownScaling.ShouldBe(new CooldownScaling { TimeRateCooldownRecovery = true, TimeRateHaste = true });

    [Fact]
    public void Gunde_HeartSplitter_ScalesItsDurationOffCooldownReductionAndHaste() =>
        SpellOf("gunde", "HeartSplitter").CooldownScaling.ShouldBe(new CooldownScaling
        {
            DurationCooldownReduction = true,
            DurationHaste = true,
            TimeRateCooldownRecovery = true,
        });

    [Fact]
    public void Mara_Kick_HasNoCooldownScaling()
    {
        var kick = SpellOf("mara", "Kick");
        kick.CooldownScaling.ShouldBeNull();
        kick.Cooldown.ShouldBe(16);
    }

    [Fact]
    public void Items_TwilightSkybolt_ScalesItsTimeRateOffHasteAlone() =>
        SpellOf("items", "TwilightSkybolt").CooldownScaling.ShouldBe(new CooldownScaling { TimeRateHaste = true });

    [Fact]
    public void Items_FatedStrike_ScalesItsDurationOffCooldownReductionAlone() =>
        SpellOf("items", "FatedStrike").CooldownScaling.ShouldBe(new CooldownScaling { DurationCooldownReduction = true });

    [Fact]
    public void Mara_Backstab_HasAOneSecondGlobalCooldownWithoutHaste()
    {
        var backstab = SpellOf("mara", "Backstab");
        backstab.GlobalCooldown.ShouldBe(1);
        backstab.GlobalCooldownScalesWithHaste.ShouldBe(false);
    }

    [Fact]
    public void Tariq_SkullCrusher_HasAOneSecondGlobalCooldownWithHaste()
    {
        var skullCrusher = SpellOf("tariq", "SkullCrusher");
        skullCrusher.GlobalCooldown.ShouldBe(1);
        skullCrusher.GlobalCooldownScalesWithHaste.ShouldBe(true);
    }

    [Fact]
    public void Rime_ColdSnap_HasTheStandardGlobalCooldown()
    {
        var coldSnap = SpellOf("rime", "ColdSnap");
        coldSnap.GlobalCooldown.ShouldBe(1.5);
        coldSnap.GlobalCooldownScalesWithHaste.ShouldBe(true);
    }

    [Fact]
    public void Mara_Kick_HasNoGlobalCooldown()
    {
        var kick = SpellOf("mara", "Kick");
        kick.GlobalCooldown.ShouldBeNull();
        kick.GlobalCooldownScalesWithHaste.ShouldBeNull();
    }

    [Fact]
    public void EveryAbilityWithAGlobalCooldown_StatesItsHasteScaling() =>
        Run().Spells
            .Where(x => x.Spell.GlobalCooldown is not null)
            .ShouldAllBe(x => x.Spell.GlobalCooldownScalesWithHaste != null);

    private static Spell SpellOf(string scope, string member) =>
        Run().Spells.Single(x => x.Scope == scope && x.Member == member).Spell;
    [Fact]
    public void Rime_FreezingTorrent_HasNormalizedScalars()
    {
        var s = Run().Spells.Single(x => x.Scope == "rime" && x.Member == "FreezingTorrent");
        s.Spell.Cooldown.ShouldBe(15);
        s.Spell.Range.ShouldBe(3000);
        s.Spell.ChannelDuration.ShouldBe(2.0);
        s.Spell.ChannelTickInterval.ShouldBe(0.4);
    }

    [Fact]
    public void Gunde_Slaughter_HasItsRadiusInGameUnits()
    {
        var s = Run().Spells.Single(x => x.Scope == "gunde" && x.Member == "Slaughter");
        s.Spell.Range.ShouldBe(700);
        s.Spell.Radius.ShouldBe(700);
    }

    [Fact]
    public void Gunde_GrimCarve_HasARangeThatOutrunsItsRadius()
    {
        var s = Run().Spells.Single(x => x.Scope == "gunde" && x.Member == "GrimCarve");
        s.Spell.Range.ShouldBe(3000);
        s.Spell.Radius.ShouldBe(850);
    }

    [Fact]
    public void Rime_GlacialBlast_HasTertiaryCost() =>
        Run().Spells.Single(x => x.Scope == "rime" && x.Member == "GlacialBlast").Spell.Costs[ResourceTypes.Tertiary].ShouldBe(2);

    [Fact]
    public void LinkedEffect_IsNamedAbilityPlusRole() =>
        Run().Spells.ShouldContain(x => x.Scope == "rime" && x.Kind == SpellKind.Effect && x.Spell.Id == 1396);

    [Fact]
    public void EveryEffectLinkedToAnAbility_ResolvesToAMember() =>
        Run().Gaps.ShouldNotContain(g => g.Kind == GapKind.UnresolvedEffect);

    [Fact]
    public void HeroesPresentInData_AreIncludedInOutput()
    {
        var result = Run();
        result.Spells.ShouldContain(s => s.Scope == "gunde");
        result.Spells.ShouldContain(s => s.Scope == "rime");
    }

    [Fact]
    public void EveryDataHeroScope_MapsToAKnownHeroName() =>
        Run().Gaps.ShouldNotContain(g => g.Kind == GapKind.UnknownScope);
}
