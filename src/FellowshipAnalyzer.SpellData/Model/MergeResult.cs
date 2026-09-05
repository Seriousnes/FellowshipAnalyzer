using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.SpellData.Model;

/// <summary>Classifies the kind of gap detected during a merge pass.</summary>
public enum GapKind
{
    /// <summary>A kit ability carries no name, so no member name can be formed.</summary>
    MissingName,
    /// <summary>A spell was built with no icon from any source.</summary>
    MissingIcon,
    /// <summary>An override tried to add a new member but supplied no <c>id</c>.</summary>
    MissingId,
    /// <summary>An effect belongs to a kit ability but carries no role, so no member name can be formed.</summary>
    UnresolvedEffect,
    /// <summary>An export resource name resolved to no ResourceTypes slot.</summary>
    UnknownResource,
    /// <summary>A scope that is not 'shared'/'items'/a hero name.</summary>
    UnknownScope,
}

/// <summary>A detected gap in the merged spell data.</summary>
public record Gap(string Scope, string Member, GapKind Kind);

/// <summary>The output of <see cref="MergeEngine.Run"/>: selected spells and detected gaps.</summary>
public record MergeResult(List<CuratedSpell> Spells, List<Gap> Gaps)
{
    /// <summary>
    /// Every classified damage school in the game-data export, keyed by FSLID and hero-independent,
    /// so an enemy ability resolves the same way a hero one does.
    /// </summary>
    public Dictionary<int, MagicSchool> Schools { get; init; } = [];

    /// <summary>
    /// The rarity ladder, keyed by tier, holding the name the build stores rather than the one it
    /// prints. Item and gem art files end in that stored name, so a tier resolves its own icon.
    /// </summary>
    public Dictionary<int, string> Rarities { get; init; } = [];

    /// <summary>
    /// Item and gem art the export draws once for every rarity rung, named without directory or
    /// extension. Art absent from this set is drawn per rung and is addressed by a name ending in the
    /// rung's stored name.
    /// </summary>
    public SortedSet<string> ArtSharedAcrossRungs { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Every talent the export slots to a hero, scoped by that hero's key. Talents are held apart from
    /// <see cref="Spells"/> because a talent often repeats the name of an ability or effect in the same
    /// hero's scope.
    /// </summary>
    public List<CuratedSpell> Talents { get; init; } = [];
}
