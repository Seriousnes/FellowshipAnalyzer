using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;

using MaraTalents = FellowshipAnalyzer.Core.Common.Spells.MaraTalents;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

/// <summary>
/// The cooldown state of one hero ability at the instant Final Stratagem was cast.
/// </summary>
/// <param name="AbilityId">The hero ability.</param>
/// <param name="RemainingMs">Milliseconds left on the recharge, zero when a charge was already available.</param>
/// <param name="ChargesAvailable">Charges ready to cast at that instant.</param>
/// <param name="MaxCharges">Charges the ability holds when fully recharged.</param>
public sealed record StratagemAbilityState(
    int AbilityId,
    int RemainingMs,
    int ChargesAvailable,
    int MaxCharges)
{
    /// <summary>Whether at least one charge was already available, so the reset recovered nothing here.</summary>
    public bool WasAvailable => ChargesAvailable > 0;

    /// <summary>Charges the reset handed back.</summary>
    public int ChargesRecovered => Math.Max(0, MaxCharges - ChargesAvailable);
}

/// <summary>
/// One Final Stratagem cast, with the cooldown state of every hero ability at that instant.
/// </summary>
public sealed record FinalStratagemCast
{
    /// <summary>When Final Stratagem was cast.</summary>
    public required int Timestamp { get; init; }

    /// <summary>The ability cast: Final Stratagem, or Macabre Stratagem when the talent replaced it.</summary>
    public required int AbilityId { get; init; }

    /// <summary>Every hero ability's cooldown state at the cast, longest recharge first.</summary>
    public IReadOnlyList<StratagemAbilityState> Abilities { get; init; } = [];

    /// <summary>Hero abilities that had at least one charge recharging when the reset landed.</summary>
    public int AbilitiesReset => Abilities.Count(state => state.RemainingMs > 0 || !state.WasAvailable);

    /// <summary>Hero abilities that were already fully available, so the reset recovered nothing from them.</summary>
    public int AbilitiesAlreadyAvailable => Abilities.Count(state => state.ChargesRecovered == 0);

    /// <summary>Total recharge time the reset removed across every hero ability.</summary>
    public int CooldownRecoveredMs => Abilities.Sum(state => state.RemainingMs);

    /// <summary>Total charges the reset handed back across every hero ability.</summary>
    public int ChargesRecovered => Abilities.Sum(state => state.ChargesRecovered);
}

/// <summary>
/// Handles Final Stratagem casts, resetting all hero ability cooldowns and 
/// recording per-ability cooldown recovery.
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
[Dependency<Abilities>]
[ActiveWhen<DoesNotHaveMacabreStratagem>]
public sealed partial class FinalStratagemAnalyzer : Analyzer
{
    private readonly List<FinalStratagemCast> _casts = [];

    /// <summary>Every Final Stratagem cast on this pull, in order.</summary>
    public IReadOnlyList<FinalStratagemCast> Casts => _casts;

    /// <summary>Total recharge time recovered across every cast.</summary>
    public int CooldownRecoveredMs => _casts.Sum(cast => cast.CooldownRecoveredMs);

    /// <summary>Total charges handed back across every cast.</summary>
    public int ChargesRecovered => _casts.Sum(cast => cast.ChargesRecovered);

    /// <summary>Average number of hero abilities each cast recovered something from.</summary>
    public double AverageAbilitiesReset =>
        _casts.Count == 0 ? 0d : (double)_casts.Sum(cast => cast.AbilitiesReset) / _casts.Count;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.FinalStratagem))]
    private void OnStratagemCast(CastEvent castEvent)
    {
        if (castEvent.Fake)
            return;

        var states = new List<StratagemAbilityState>();

        foreach (var ability in Abilities.GetAbilities())
        {
            if (ability.PrimarySpell.Cooldown is not > 0)
                continue;

            var spellId = ability.PrimarySpell.Id;
            if (spellId == Spells.FinalStratagem.Id)
                continue;

            var maxCharges = Math.Max(1, Abilities.GetMaxCharges(spellId));
            states.Add(new StratagemAbilityState(
                spellId,
                SpellUsable.CooldownRemaining(spellId, castEvent.Timestamp),
                Math.Min(SpellUsable.ChargesAvailable(spellId), maxCharges),
                maxCharges));
        }

        states.Sort(static (left, right) => right.RemainingMs.CompareTo(left.RemainingMs));

        foreach (var state in states)
            SpellUsable.EndCooldown(state.AbilityId, castEvent.Timestamp, restoreAllCharges: true);

        _casts.Add(new FinalStratagemCast
        {
            Timestamp = castEvent.Timestamp,
            AbilityId = castEvent.Ability.Id,
            Abilities = states,
        });
    }
}

internal class DoesNotHaveMacabreStratagem : IModuleActivePredicate
{
    public static bool IsActive(ParseContext context) => !context.SelectedCombatant.HasTalent(MaraTalents.MacabreStratagem);
}