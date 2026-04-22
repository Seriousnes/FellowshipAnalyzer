namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// A static spell definition used in spell registries.
/// Contains identity and display metadata. Gameplay metadata (cooldowns, GCD, etc.) lives in
/// <see cref="Analysis.SpellbookAbility"/>.
/// </summary>
public record Spell(int Id, string Name = "", string Icon = "")
{
    /// <summary>
    /// The combat-log <c>abilityGameID</c> used to match events.
    /// For <see cref="Effect"/> this is <c>1_000_000 + Id</c>; for plain spells it equals <see cref="Id"/>.
    /// </summary>
    public virtual int Guid => Id;

    /// <summary>
    /// Creates a <see cref="Spell"/> or <see cref="Effect"/> from a combat-log <c>abilityGameID</c>.
    /// IDs &ge; 1,000,000 are effects; below that are plain spells.
    /// </summary>
    public static Spell FromGuid(int guid, string name = "", string icon = "") =>
        guid >= 1_000_000
            ? new Effect(guid - 1_000_000, name, icon)
            : new Spell(guid, name, icon);
}

/// <summary>
/// A spell effect — a secondary spell whose combat-log <c>abilityGameID</c> is encoded as
/// <c>1_000_000 + effectId</c>.
/// </summary>
public record Effect(int Id, string Name = "", string Icon = "") : Spell(Id, Name, Icon)
{
    /// <summary>The combat-log <c>abilityGameID</c> (<c>1_000_000 + Id</c>).</summary>
    public override int Guid => 1_000_000 + Id;
}