using System.Collections.Frozen;

namespace FellowshipAnalyzer.Core.Analysis.Deaths;

/// <summary>
/// What a player could have done about one enemy ability. Every flag is game knowledge that no part of
/// a combat log carries, so each is curated by hand in <see cref="EnemyAbilities"/> rather than derived.
/// </summary>
public sealed record EnemyAbility
{
    /// <summary>The enemy ability's spell id, as the raw <see cref="Common.Spells.FSLID"/> value the log reports.</summary>
    public required int AbilityId { get; init; }

    /// <summary>
    /// The ability's name at the time it was curated. Read this as the curator's note: display always
    /// uses the name the report's master data carries, so a stale entry here cannot mislabel a hit.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>Whether moving out of the ability's telegraph takes the player out of the hit entirely.</summary>
    public bool Avoidable { get; init; }

    /// <summary>Whether the cast that produces the ability can be interrupted.</summary>
    public bool Interruptible { get; init; }

    /// <summary>Whether the hit is one a defensive is expected to be held for.</summary>
    public bool ShouldMitigate { get; init; }

    /// <summary>The flags this entry carries, in a fixed order, for a view that renders one tag per flag.</summary>
    public IReadOnlyList<EnemyAbilityTag> Tags
    {
        get
        {
            var tags = new List<EnemyAbilityTag>(3);
            if (Avoidable) tags.Add(EnemyAbilityTag.Avoidable);
            if (Interruptible) tags.Add(EnemyAbilityTag.Interruptible);
            if (ShouldMitigate) tags.Add(EnemyAbilityTag.ShouldMitigate);
            return tags;
        }
    }
}

/// <summary>One curated property of an enemy ability, as a view renders it.</summary>
public enum EnemyAbilityTag
{
    /// <summary>The hit can be moved out of.</summary>
    Avoidable,

    /// <summary>The cast can be interrupted.</summary>
    Interruptible,

    /// <summary>The hit is one to hold a defensive for.</summary>
    ShouldMitigate,
}

/// <summary>
/// The curated table of enemy abilities, keyed by the raw <see cref="Common.Spells.FSLID"/> value. Enemy
/// abilities use the same plain-ability and effect id ranges hero abilities do, so a key here is the
/// same number a damage event's <c>Ability.Id</c> carries.
/// <para>
/// The table starts empty. Whether a hit was avoidable, interruptible, or one to hold a defensive for is
/// game knowledge: neither the combat log nor the game-data dumps record it. Until an entry is added the
/// deaths view renders no tags, and every other figure it shows is unaffected.
/// </para>
/// <para>
/// To add one: open a report where the ability killed someone and read the id and name off the death
/// card, then confirm the ability against <c>external/fs_tc_uploads/s3/spell_data.json</c>, whose
/// <c>Abilities</c> and <c>Effects</c> blocks map that id to the ability's <c>DevName</c>, and against
/// <c>mob_data.json</c>, which lists each creature's abilities by name. Classify from play, add the
/// entry to <see cref="All"/>, and give <see cref="EnemyAbility.Name"/> the name the dump reports.
/// </para>
/// </summary>
public static class EnemyAbilities
{
    /// <summary>Every curated enemy ability, keyed by spell id.</summary>
    public static FrozenDictionary<int, EnemyAbility> All { get; } = BuildTable().ToFrozenDictionary(entry => entry.AbilityId);

    /// <summary>The curated entry for <paramref name="abilityId"/>, or <c>null</c> when the ability has not been classified.</summary>
    public static EnemyAbility? MaybeGet(int abilityId) => All.GetValueOrDefault(abilityId);

    private static EnemyAbility[] BuildTable() => [];
}
