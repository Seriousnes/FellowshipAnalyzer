using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Helena;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Helena.Modules;

/// <summary>
/// Applies Helena's Veteran of War passive to <see cref="SpellUsable"/> for the whole dungeon: every source
/// cast, between pulls included, takes its flat reduction off each ability it names, doubled while
/// Siegebreaker is active and doubled again under a Punishing Strikes stack. What each cast generated is left
/// on <see cref="LastCast"/> for <see cref="VeteranOfWarAnalyzer"/> to report per pull.
/// </summary>
/// <remarks>
/// Every reduction in <see cref="Combos"/> is twice the figure the export rows state. Across 26 Season 3
/// logs the tooltip figures leave 29% of Shockwave casts and 11% of Shield Throw casts before the model's
/// recharge, while doubling every figure puts both abilities' earliest casts at the recharge, as Power
/// Strike's already are, for every player, outside Siegebreaker as well as inside it, and before a run's
/// first Siegebreaker. Hold the Line's figure cannot be told apart from a full reset, since the targets
/// rarely hold more than 20 seconds of cooldown when it is cast.
/// </remarks>
[Dependency<SpellUsable>]
public sealed partial class VeteranOfWar : Analyzer
{
    private bool _ultimateActive;
    private int _punishingStrikesStacks;

    /// <summary>The logged reduction of every combo, as a multiple of its export row: twice the row.</summary>
    private const double ComboScaler = 2.0;

    public static List<CooldownCombo> Combos { get; } =
    [
        new(Spells.MeasuredStrike.FSLID, Spells.ShieldSlam.FSLID, Reduction(SpellTuning.VeteranOfWar.Warmaster_CooldownReductionCombo_LightMelee_MeleeCleave)),
        new(Spells.MeasuredStrike.FSLID, Spells.ShieldThrow.FSLID, Reduction(SpellTuning.VeteranOfWar.Warmaster_CooldownReductionCombo_LightMelee_BouncyProjectile)),
        new(Spells.PowerStrike.FSLID, Spells.ShieldSlam.FSLID, Reduction(SpellTuning.VeteranOfWar.Warmaster_CooldownReductionCombo_HeavyMelee_MeleeCleave)),
        new(Spells.PowerStrike.FSLID, Spells.ShieldThrow.FSLID, Reduction(SpellTuning.VeteranOfWar.Warmaster_CooldownReductionCombo_HeavyMelee_BouncyProjectile)),
        new(Spells.ShieldSlam.FSLID, Spells.Shockwave.FSLID, Reduction(SpellTuning.VeteranOfWar.Warmaster_CooldownReductionCombo_MeleeCleave_MeleeAoeSlow)),
        new(Spells.ShieldThrow.FSLID, Spells.Shockwave.FSLID, Reduction(SpellTuning.VeteranOfWar.Warmaster_CooldownReductionCombo_BouncyProjectile_MeleeAoeSlow)),
        new(Spells.Shockwave.FSLID, Spells.ShieldsUp.FSLID, Reduction(SpellTuning.VeteranOfWar.Warmaster_CooldownReductionCombo_MeleeAoeSlow_DefenceBuff)),
        new(Spells.HoldTheLine.FSLID, Spells.ShieldSlam.FSLID, Reduction(SpellTuning.VeteranOfWar.Warmaster_CooldownReductionCombo_MoveToTargetStun_MeleeCleave)),
        new(Spells.HoldTheLine.FSLID, Spells.ShieldThrow.FSLID, Reduction(SpellTuning.VeteranOfWar.Warmaster_CooldownReductionCombo_MoveToTargetStun_BouncyProjectile)),
        new(Spells.HoldTheLine.FSLID, Spells.Shockwave.FSLID, Reduction(SpellTuning.VeteranOfWar.Warmaster_CooldownReductionCombo_MoveToTargetStun_MeleeAoeSlow)),
        new(Spells.HoldTheLine.FSLID, Spells.ShieldsUp.FSLID, Reduction(SpellTuning.VeteranOfWar.Warmaster_CooldownReductionCombo_MoveToTargetStun_DefenceBuff)),
    ];

    public static readonly double ActiveUltimateScaler = SpellTuning.VeteranOfWar.ActiveUltimate;

    public static readonly double PunishingStrikesScaler =
        SpellTuning.VeteranOfWar.BlockToIncreasedPower_CooldownReducementMultiplier;

    public static readonly int PunishingStrikesStacksAtProc = (int)TalentTuning.PunishingStrikes.StacksAtProc;

    private static int Reduction(double exportRowSeconds) => (int)(exportRowSeconds * ComboScaler * 1000);

    public static List<int> HoldTheLineTargets { get; } =
    [
        .. Combos.Where(combo => combo.SourceSpellId == Spells.HoldTheLine.FSLID)
            .Select(combo => combo.TargetSpellId),
    ];

    public static List<int> ReductionTargets { get; } =
        [.. Combos.Select(combo => combo.TargetSpellId).Distinct()];

    /// <summary>Whether Siegebreaker is on the player right now.</summary>
    public bool UltimateActive => _ultimateActive;

    /// <summary>The most recent source cast and what it took off each target, or <c>null</c> before the first.</summary>
    public VeteranOfWarCast? LastCast { get; private set; }

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SiegebreakerBuff))]
    private void OnUltimateApplied() => _ultimateActive = true;

    [On<RefreshBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SiegebreakerBuff))]
    private void OnUltimateRefreshed() => _ultimateActive = true;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SiegebreakerBuff))]
    private void OnUltimateRemoved() => _ultimateActive = false;

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.PunishingStrikesBuff))]
    private void OnPunishingStrikesApplied() =>
        _punishingStrikesStacks = PunishingStrikesStacksAtProc;

    [On<ApplyBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.PunishingStrikesBuff))]
    private void OnPunishingStrikesStacked(ApplyBuffStackEvent buffEvent) =>
        _punishingStrikesStacks = buffEvent.Stack;

    [On<RemoveBuffStackEvent>(To = Actor.Player, Spell = nameof(Spells.PunishingStrikesBuff))]
    private void OnPunishingStrikesStackRemoved(RemoveBuffStackEvent buffEvent) =>
        _punishingStrikesStacks = buffEvent.Stack;

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.PunishingStrikesBuff))]
    private void OnPunishingStrikesRemoved() => _punishingStrikesStacks = 0;

    [On<CastEvent>(By = Actor.Player, Spells = [
        nameof(Spells.MeasuredStrike),
        nameof(Spells.PowerStrike),
        nameof(Spells.ShieldSlam),
        nameof(Spells.ShieldThrow),
        nameof(Spells.Shockwave),
        nameof(Spells.HoldTheLine)])]
    private void OnComboSource(CastEvent castEvent)
    {
        var underPunishingStrikes = _punishingStrikesStacks > 0;
        var scaler = _ultimateActive ? ActiveUltimateScaler : 1.0;
        if (underPunishingStrikes) scaler *= PunishingStrikesScaler;

        var targetsAvailable = castEvent.Ability.Id == Spells.HoldTheLine.FSLID
            ? CaptureHoldTheLineAvailability()
            : null;

        var reductions = new Dictionary<int, CooldownReductionResult>();
        foreach (var combo in Combos)
        {
            if (combo.SourceSpellId != castEvent.Ability.Id) continue;

            var requested = (int)Math.Round(combo.ReductionMs * scaler);
            reductions[combo.TargetSpellId] = SpellUsable.ReduceCooldown(combo.TargetSpellId, requested, castEvent.Timestamp);
        }

        LastCast = new VeteranOfWarCast(castEvent, underPunishingStrikes, reductions, targetsAvailable);
    }

    private Dictionary<int, bool> CaptureHoldTheLineAvailability()
    {
        var available = new Dictionary<int, bool>(HoldTheLineTargets.Count);
        foreach (var target in HoldTheLineTargets)
        {
            available[target] = target == Spells.ShieldSlam.FSLID
                ? SpellUsable.IsAvailable(target)
                : !SpellUsable.IsOnCooldown(target);
        }

        return available;
    }
}

/// <summary>
/// One Veteran of War source cast as <see cref="VeteranOfWar"/> applied it.
/// </summary>
/// <param name="Cast">The cast itself.</param>
/// <param name="UnderPunishingStrikes">Whether a Punishing Strikes stack doubled the cast's reductions.</param>
/// <param name="Reductions">Milliseconds of cooldown progress the cast generated against each target it names, and how much of that shortened a running cooldown.</param>
/// <param name="TargetsAvailable">
/// For a Hold the Line cast, whether each of its targets was available before the cast's reductions were
/// applied: a charge in hand for Shield Slam, nothing recharging for the others. <c>null</c> for any other source.
/// </param>
public sealed record VeteranOfWarCast(
    CastEvent Cast,
    bool UnderPunishingStrikes,
    Dictionary<int, CooldownReductionResult> Reductions,
    Dictionary<int, bool>? TargetsAvailable);

public sealed record CooldownCombo(int SourceSpellId, int TargetSpellId, int ReductionMs);
