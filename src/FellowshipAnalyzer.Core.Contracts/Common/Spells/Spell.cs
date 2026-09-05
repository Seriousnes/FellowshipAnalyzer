using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Common.Spells.Rime;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Core.Common.Spells;

/// <summary>
/// A static spell definition: identity, the game's ability category, physical facts (cooldown,
/// range, charges, cast/channel timing), and resource costs. Behaviour metadata (GCD, the tool's
/// analysis grouping, haste scaling) lives on <c>SpellbookAbility</c>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Spell), "ability")]
[JsonDerivedType(typeof(Effect), "effect")]
[JsonDerivedType(typeof(Talent), "talent")]
[JsonDerivedType(typeof(Weapon), "weapon")]
public record Spell : IRimeSpell, IElarionSpell, IArdeosSpell
{
    /// <summary>The FellowshipLogs id (combat-log <c>abilityGameID</c>). Base spells use <see cref="Id"/>; subtypes add their range offset.</summary>
    [JsonIgnore]
    public virtual FSLID FSLID => new(Id);

    /// <summary>The native FellowshipLogs ability id, with the range offset stripped; <see cref="FSLID"/> reapplies it based on this spell's kind.</summary>
    public int Id { get; init; }
    /// <summary>The spell's display name as shown in the log and the UI.</summary>
    public string Name { get; init; } = "";
    /// <summary>The icon asset filename for this spell, as sourced from the game's data.</summary>
    public string Icon { get; init; } = "";

    /// <summary>
    /// The game's ability-category classification from the game-data export (Basic, Core,
    /// Major, Defensive, and so on). Distinct from the tool's internal <c>SpellCategory</c>
    /// analysis grouping. <c>null</c> when the source data does not classify the ability.
    /// </summary>
    public AbilityCategory? AbilityCategory { get; init; }

    /// <summary>
    /// The damage school this spell deals in. <c>null</c> when the school comes straight from
    /// the game-data export and needs no curation; set on an entry the export leaves unclassified, where
    /// it overrides what the merge collected for this <see cref="FSLID"/>.
    /// </summary>
    public MagicSchool? School { get; init; }

    /// <summary>The base cooldown in seconds; <c>null</c> when the spell has no cooldown.</summary>
    public double? Cooldown { get; init; }

    /// <summary>Seconds removed from this ability's remaining cooldown when a target it has debuffed dies, per qualifying window.</summary>
    public double? CooldownReductionOnTargetDeath { get; init; }

    /// <summary>The spell's maximum range in game units; <c>null</c> when the spell is not range-limited.</summary>
    public int? Range { get; init; }
    /// <summary>The radius of the area the spell affects, in game units; <c>null</c> when the spell affects no area.</summary>
    public int? Radius { get; init; }
    /// <summary>The number of charges the spell can bank before a cast starts its cooldown; <c>1</c> for a spell without a charge system.</summary>
    public int Charges { get; init; } = 1;
    /// <summary>The time in seconds to complete a non-channeled cast; <c>null</c> for an instant cast.</summary>
    public double? CastDuration { get; init; }
    /// <summary>The total duration in seconds of a channeled cast; <c>null</c> when the spell does not channel.</summary>
    public double? ChannelDuration { get; init; }
    /// <summary>The time in seconds between ticks while channeling; <c>null</c> when the spell does not channel.</summary>
    public double? ChannelTickInterval { get; init; }

    /// <summary>The cost in Gunde's Spirit resource; <c>null</c> when the spell does not spend Spirit.</summary>
    [JsonIgnore] 
    public int? SpiritCost => Cost(ResourceTypes.Spirit);
    /// <summary>The cost in Rime's Winter Orbs, the hero's <see cref="ResourceTypes.Tertiary"/> resource; <c>null</c> when the spell does not spend orbs.</summary>
    [JsonIgnore] 
    public int? WinterOrbCost => Cost(ResourceTypes.Tertiary);
    /// <summary>The cost in Rime's Anima, the hero's <see cref="ResourceTypes.Primary"/> resource; <c>null</c> when the spell does not spend Anima.</summary>
    [JsonIgnore] 
    public int? AnimaCost => Cost(ResourceTypes.Primary);
    /// <summary>The cost in the hero's Focus, the <see cref="ResourceTypes.Primary"/> resource; <c>null</c> when the spell does not spend Focus.</summary>
    [JsonIgnore] 
    public int? FocusCost => Cost(ResourceTypes.Primary);
    /// <summary>The cost in Ardeos's Embers, the hero's <see cref="ResourceTypes.Primary"/> resource; <c>null</c> when the spell does not spend Embers.</summary>
    [JsonIgnore] 
    public int? EmberCost => Cost(ResourceTypes.Primary);

    /// <summary>Resource costs keyed by abstract <see cref="ResourceTypes"/> slot; empty when the spell spends nothing.</summary>
    public Dictionary<ResourceTypes, int> Costs { get; init; } = [];

    /// <summary>The resource generation the game data states for this spell; <c>null</c> when it states none.</summary>
    public ResourceGeneration? ResourceGeneration { get; init; }

    /// <summary>The cost in the given resource slot, or <c>null</c> when the spell does not spend it.</summary>
    public int? Cost(ResourceTypes type) => Costs.TryGetValue(type, out var value) ? value : null;

    /// <summary>Creates the typed spell for a combat-log <c>abilityGameID</c>, decoding the FSL range via <see cref="FSLID"/>.</summary>
    public static Spell FromFSLID(FSLID fslid, string name = "", string icon = "") => fslid.Kind switch
    {
        SpellKind.Weapon => new Weapon { Id = fslid.NativeId, Name = name, Icon = icon },
        SpellKind.Talent => new Talent { Id = fslid.NativeId, Name = name, Icon = icon },
        SpellKind.Effect => new Effect { Id = fslid.NativeId, Name = name, Icon = icon },
        _ => new Spell { Id = fslid.NativeId, Name = name, Icon = icon },
    };
}

/// <summary>A spell effect: namespaced id <c>1_000_000 + Id</c>.</summary>
public record Effect : Spell
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override FSLID FSLID => FSLID.FromNative(SpellKind.Effect, Id);
}

/// <summary>A talent: namespaced id <c>2_000_000 + Id</c>.</summary>
public record Talent : Spell
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override FSLID FSLID => FSLID.FromNative(SpellKind.Talent, Id);
}

/// <summary>A weapon trait: namespaced id <c>3_000_000 + Id</c>.</summary>
public record Weapon : Spell
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override FSLID FSLID => FSLID.FromNative(SpellKind.Weapon, Id);
}
