using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Core.Events;

using FSLID = FellowshipAnalyzer.Core.Common.Spells.FSLID;

namespace FellowshipAnalyzer.Heroes.Aeona.Normalizers;

/// <summary>
/// Links the heals and Oblivion's Embrace shields that Oblivion and Entropy's Claim log under their own
/// ability to the damage hit that produced them.
/// </summary>
/// <remarks>
/// <para>
/// Oblivion's heals carry the Oblivion ability whether a direct hit or an Erasure tick produced them, and
/// Entropy's Claim's heals carry the Entropy's Claim ability whether its own tick or an Entropic Burst tick
/// produced them. A hit's heals, and the Oblivion's Embrace shields their overheal grows, precede the hit's
/// damage event, so each is linked to the next damage event of its ability family from the player.
/// Oblivion's family is Oblivion, its damage effect, and Erasure; Entropy's Claim's is its dot and
/// Entropic Burst.
/// </para>
/// <para>
/// Oblivion's Embrace stacks: the game removes the ally's shield and applies one holding the remainder
/// plus the new absorb. The application is linked to that removal under <see cref="Replaced"/>.
/// </para>
/// </remarks>
public sealed class HitLinkNormalizer : IEventNormalizer
{
    /// <summary>Links a damage event to the heals its hit produced.</summary>
    public const string Heals = nameof(Heals);

    /// <summary>Links a damage event to the Oblivion's Embrace applications its hit's overheal produced.</summary>
    public const string Shields = nameof(Shields);

    /// <summary>Links an Oblivion's Embrace application to the removal of the shield it replaced on the same ally.</summary>
    public const string Replaced = nameof(Replaced);

    /// <summary>Milliseconds before a damage event within which a heal or shield can be linked to it.</summary>
    public const int LinkWindowMs = 50;

    private static readonly FSLID[] OblivionHits = [Spells.Oblivion.FSLID, Spells.OblivionDamage.FSLID, Spells.Erasure.FSLID];

    private static readonly FSLID[] EntropyClaimHits = [Spells.EntropyClaimDot.FSLID, Spells.EntropicBurst.FSLID];

    /// <inheritdoc/>
    public int Priority => 100;

    /// <inheritdoc/>
    public List<Event> Normalize(List<Event> events, int playerId)
    {
        var oblivionHeals = new List<HealEvent>();
        var entropyClaimHeals = new List<HealEvent>();
        var shields = new List<ApplyBuffEvent>();
        var removals = new Dictionary<int, RemoveBuffEvent>();

        foreach (var e in events)
        {
            switch (e)
            {
                case HealEvent heal when heal.SourceId == playerId && IdOf(heal) == Spells.Oblivion.FSLID:
                    oblivionHeals.Add(heal);
                    break;
                case HealEvent heal when heal.SourceId == playerId && IdOf(heal) == Spells.EntropyClaim.FSLID:
                    entropyClaimHeals.Add(heal);
                    break;
                case RemoveBuffEvent removal when removal.SourceId == playerId && IdOf(removal) == Spells.OblivionAbsorbAbsorb.FSLID:
                    removals[removal.TargetId] = removal;
                    break;
                case ApplyBuffEvent apply when apply.SourceId == playerId && IdOf(apply) == Spells.OblivionAbsorbAbsorb.FSLID:
                    if (removals.Remove(apply.TargetId, out var removed) && removed.Timestamp == apply.Timestamp)
                        apply.AddRelatedEvent(Replaced, removed);
                    shields.Add(apply);
                    break;
                case DamageEvent damage when damage.SourceId == playerId && OblivionHits.Contains(IdOf(damage)):
                    Link(damage, Heals, oblivionHeals);
                    Link(damage, Shields, shields);
                    break;
                case DamageEvent damage when damage.SourceId == playerId && EntropyClaimHits.Contains(IdOf(damage)):
                    Link(damage, Heals, entropyClaimHeals);
                    break;
            }
        }

        return events;
    }

    private static void Link<TEvent>(DamageEvent damage, string relation, List<TEvent> pending) where TEvent : Event
    {
        foreach (var e in pending)
        {
            if (damage.Timestamp - e.Timestamp <= LinkWindowMs) damage.AddRelatedEvent(relation, e);
        }

        pending.Clear();
    }

    private static FSLID IdOf(IAbilityEvent e) =>
        e.Ability is { FSLID.Value: not 0 } ability ? ability.FSLID : e.AbilityGameId;
}
