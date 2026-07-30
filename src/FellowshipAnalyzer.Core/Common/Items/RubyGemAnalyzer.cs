using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Core.Analysis.Gems;

/// <summary>
/// Measures what the player's Ruby gem contributed. Three of its five traits leave a trace in the log:
/// Might of the Minotaur, a primary attribute buff whose windows the log records; Unyielding Vitality, a
/// periodic in-combat regeneration with its own heal events; and Blessing of the Conqueror, a boss-battle
/// output multiplier whose windows are marked by the Essence of the Conqueror buff. The remaining traits are
/// flat stamina, primary attribute, and maximum health, which the log records only as already applied.
/// </summary>
public sealed partial class RubyGemAnalyzer : Analyzer, IGemAnalyzer
{
    /// <summary>Raises the player's primary attribute while their health is above a threshold.</summary>
    public static readonly GemTrait MightOfTheMinotaur = new(
        Items.MightOfTheMinotaur, GemRankPower.Rank1,
        Items.MightOfTheMinotaurII, GemRankPower.Rank6);

    /// <summary>Regenerates a share of the player's health on a timer while they are in combat.</summary>
    public static readonly GemTrait UnyieldingVitality = new(
        Items.UnyieldingVitality, GemRankPower.Rank3,
        Items.UnyieldingVitalityII, GemRankPower.Rank8);

    /// <summary>
    /// Raises the player's damage, healing, and absorption while they are in combat with a boss, by 5% at
    /// rank 5 and 15% at rank 10. The bonus is live for as long as the Essence of the Conqueror buff is.
    /// </summary>
    public static readonly GemTrait BlessingOfTheConqueror = new(
        Items.BlessingOfTheConqueror, GemRankPower.Rank5,
        Items.BlessingOfTheConquerorII, GemRankPower.Rank10);

    private long _damage;
    private long _healing;
    private long _damageAtWindowOpen;
    private long _healingAtWindowOpen;
    private bool _conquerorActive;

    /// <inheritdoc/>
    public GemType Gem => GemType.Ruby;

    /// <inheritdoc/>
    public int GemPower => Owner.SelectedCombatant.Ruby;

    /// <summary>Total time Might of the Minotaur was active over the fight, in milliseconds.</summary>
    public int MightOfTheMinotaurUptimeMs =>
        MightOfTheMinotaur.UptimeOn(Owner.SelectedCombatant, GemPower, Owner.FightStartTime, Owner.FightEndTime);

    /// <summary>Might of the Minotaur's share of the fight, as a fraction.</summary>
    public double MightOfTheMinotaurUptime =>
        Owner.FightDurationMs > 0 ? (double)MightOfTheMinotaurUptimeMs / Owner.FightDurationMs : 0;

    /// <summary>Effective healing Unyielding Vitality did over the fight.</summary>
    public long UnyieldingVitalityHealing { get; private set; }

    /// <summary>Healing Unyielding Vitality lost to overheal.</summary>
    public long UnyieldingVitalityOverheal { get; private set; }

    /// <summary>The output bonus the unlocked rank of Blessing of the Conqueror grants, as a fraction.</summary>
    public double ConquerorBonus =>
        BlessingOfTheConqueror.ByRank(GemPower, based: 0.05, upgraded: 0.15, locked: 0);

    /// <summary>Damage the player dealt while Essence of the Conqueror was active, with the bonus already included.</summary>
    public long ConquerorWindowDamage { get; private set; }

    /// <summary>Effective healing the player did while Essence of the Conqueror was active, with the bonus already included.</summary>
    public long ConquerorWindowHealing { get; private set; }

    /// <summary>
    /// The share of <see cref="ConquerorWindowDamage"/> Blessing of the Conqueror was responsible for. The
    /// logged amount already includes the bonus, so the share it added is <c>amount × bonus ÷ (1 + bonus)</c>.
    /// </summary>
    public long ConquerorDamage => ContributedShare(ConquerorWindowDamage);

    /// <summary>The share of <see cref="ConquerorWindowHealing"/> Blessing of the Conqueror was responsible for.</summary>
    public long ConquerorHealing => ContributedShare(ConquerorWindowHealing);

    private long ContributedShare(long amount) =>
        ConquerorBonus <= 0 ? 0 : (long)(amount * (ConquerorBonus / (1 + ConquerorBonus)));

    [On<FightStartEvent>]
    private void OnFightStart(FightStartEvent fightStartEvent)
    {
        if (Owner.SelectedCombatant.Auras.Any(aura => IsEssenceOfTheConqueror(aura.Ability)))
            OpenConquerorWindow();
    }

    [On<HealEvent>(To = Actor.Player)]
    private void OnUnyieldingVitalityHeal(HealEvent healEvent)
    {
        if (healEvent.Ability.FSLID != Items.UnyieldingVitality.FSLID &&
            healEvent.Ability.FSLID != Items.UnyieldingVitalityII.FSLID) return;

        UnyieldingVitalityHealing += healEvent.Amount;
        UnyieldingVitalityOverheal += healEvent.Overheal ?? 0;
    }

    [On<DamageEvent>(By = Actor.Player)]
    private void OnDamage(DamageEvent damageEvent) => _damage += damageEvent.Amount;

    [On<HealEvent>(By = Actor.Player)]
    private void OnHeal(HealEvent healEvent) => _healing += healEvent.Amount;

    [On<ApplyBuffEvent>(To = Actor.Player)]
    private void OnApplyEssence(ApplyBuffEvent buffEvent)
    {
        if (IsEssenceOfTheConqueror(buffEvent.Ability.FSLID))
            OpenConquerorWindow();
    }

    [On<RemoveBuffEvent>(To = Actor.Player)]
    private void OnRemoveEssence(RemoveBuffEvent buffEvent)
    {
        if (_conquerorActive && IsEssenceOfTheConqueror(buffEvent.Ability.FSLID))
            CloseConquerorWindow();
    }

    [On<FightEndEvent>]
    private void OnFightEnd(FightEndEvent fightEndEvent)
    {
        if (_conquerorActive)
            CloseConquerorWindow();
    }

    private void OpenConquerorWindow()
    {
        if (_conquerorActive) return;

        _damageAtWindowOpen = _damage;
        _healingAtWindowOpen = _healing;
        _conquerorActive = true;
    }

    private void CloseConquerorWindow()
    {
        ConquerorWindowDamage += _damage - _damageAtWindowOpen;
        ConquerorWindowHealing += _healing - _healingAtWindowOpen;
        _damageAtWindowOpen = _damage;
        _healingAtWindowOpen = _healing;
        _conquerorActive = false;
    }

    private static bool IsEssenceOfTheConqueror(FSLID fslid) =>
        fslid == Items.EssenceOfTheConqueror.FSLID || fslid == Items.EssenceOfTheConquerorII.FSLID;

    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.Items;
}
