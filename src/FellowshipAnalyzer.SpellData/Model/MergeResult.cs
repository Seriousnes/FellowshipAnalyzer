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
public record MergeResult(IReadOnlyList<CuratedSpell> Spells, IReadOnlyList<Gap> Gaps)
{
    /// <summary>
    /// Every classified damage school in the game-data export, keyed by FSLID and hero-independent,
    /// so an enemy ability resolves the same way a hero one does.
    /// </summary>
    public IReadOnlyDictionary<int, MagicSchool> Schools { get; init; } = new Dictionary<int, MagicSchool>();
}
