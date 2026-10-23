using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;

using MaraTalents = FellowshipAnalyzer.Core.Common.Spells.MaraTalents;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

/// <param name="GainsDamage">
/// Whether resetting the ability gains damage: a rotational ability, a damage cooldown, or Brooding Shadows,
/// whose stealth turns the next builder into a poison application. The rated counts on
/// <see cref="FinalStratagemCast"/> read only these abilities.
/// </param>
public sealed record StratagemAbilityState(
    int AbilityId,
    int RemainingMs,
    int ChargesAvailable,
    int MaxCharges,
    bool GainsDamage)
{
    public bool WasAvailable => ChargesAvailable > 0;

    public int ChargesRecovered => Math.Max(0, MaxCharges - ChargesAvailable);
}

public sealed record FinalStratagemCast
{
    public required int Timestamp { get; init; }

    public required int AbilityId { get; init; }

    public List<StratagemAbilityState> Abilities { get; init; } = [];

    public IEnumerable<StratagemAbilityState> DamageAbilities => Abilities.Where(state => state.GainsDamage);

    public int AbilitiesReset => DamageAbilities.Count(state => state.RemainingMs > 0 || !state.WasAvailable);

    public int AbilitiesAlreadyAvailable => DamageAbilities.Count(state => state.ChargesRecovered == 0);

    public int CooldownRecoveredMs => DamageAbilities.Sum(state => state.RemainingMs);

    public int ChargesRecovered => DamageAbilities.Sum(state => state.ChargesRecovered);
}

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<SpellUsable>]
[Dependency<Abilities>]
[ActiveWhen<DoesNotHaveMacabreStratagem>]
public sealed partial class FinalStratagemAnalyzer : Analyzer
{
    private readonly List<FinalStratagemCast> _casts = [];

    public List<FinalStratagemCast> Casts => _casts;

    public int CooldownRecoveredMs => _casts.Sum(cast => cast.CooldownRecoveredMs);

    public int ChargesRecovered => _casts.Sum(cast => cast.ChargesRecovered);

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
                maxCharges,
                GainsDamage(ability)));
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

    private static bool GainsDamage(SpellbookAbility ability) =>
        ability.Category is SpellCategory.Rotational or SpellCategory.RotationalAoe or SpellCategory.Cooldowns
        || ability.PrimarySpell.Id == Spells.BroodingShadows.Id;
}

internal class DoesNotHaveMacabreStratagem : IModuleActivePredicate
{
    public static bool IsActive(ParseContext context) => !context.SelectedCombatant.HasTalent(MaraTalents.MacabreStratagem);
}